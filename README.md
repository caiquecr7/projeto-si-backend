# projeto-si-backend

API de cadastro, coleta de amostras, troca de senha e login biométrico por dinâmica de digitação.
**.NET 10**, Minimal API, Vertical Slice, PostgreSQL (EF Core + Npgsql) e integração HTTP com a `projeto_si_ml_api`.

O backend é a porta de entrada do sistema: ele concentra credenciais, JWT, bloqueio por tentativas e a validação/mascaramento das features. A classificação biométrica é delegada à ML API, que usa o algoritmo **`M2005_IDB`**.

---

## Algoritmo biométrico utilizado

O backend **não treina nem classifica** localmente. Ele envia as 5 amostras do enrollment ou o vetor da tentativa de login para a ML API, que executa o **`M2005_IDB`** (*Improved Double Parallel*, Pisani et al., IJCNN 2015):

- **Submodelo deslizante:** janela FIFO de 50 amostras genuínas recentes, com média, mediana e desvio-padrão por dígrafo.
- **Submodelo crescente incremental (Welford):** média e desvio-padrão em memória `O(1)` sobre todo o histórico genuíno.
- **Escore final:** média aritmética dos dois submodelos, comparada ao limiar calibrado por usuário (`min` / `min_non_outlier`, teto `0.7`).
- **Treinamento One-Class:** somente amostras genuínas; o backend só persiste amostras de login quando a ML API retorna `shouldRecordForRetraining: true`.

A escolha do `M2005_IDB` foi baseada nos 52 experimentos do `projeto_si_experimentos`: BACC médio `0.8445`, com CMU `BACC = 0.8909`, `FRR = 15.24%` e `FAR = 6.58%`.

---

## Pré-requisitos

| Ferramenta | Versão |
|---|---|
| .NET SDK | 10.0 |
| PostgreSQL | 17 |
| Docker + Docker Compose | Opcional, para execução em container |

A ML API deve estar acessível em `Biometria:MlApiUrl` (padrão local: `http://localhost:8000`).

---

## Executar

### Opção A — Stack completa com Docker Compose (recomendada)

Na raiz deste submódulo:

```bash
docker compose up --build
```

Sobem os serviços `postgres`, `ml-api` e `api` (e o frontend, se o profile `frontend` for habilitado). A API fica publicada em `http://localhost:5001` (`API_PORT`).

```bash
# Subir também o frontend
docker compose --profile frontend up --build

# Acompanhar logs
docker compose logs -f api

# Encerrar
docker compose down
```

### Opção B — Execução local

1. Suba apenas o PostgreSQL (ou use uma instância local):

```bash
docker compose up -d postgres
```

2. Execute a API:

```bash
dotnet run --project src/ProjetoSi.Api
```

A API sobe em `http://localhost:5000` (mesmo alvo do `proxy.conf.json` do frontend) e aplica as migrações ao iniciar em `Development`.
Tudo em container: `docker compose up --build`.

Testes: `dotnet test`.

---

## Configuração

Principais chaves de `appsettings.json` / variáveis de ambiente:

| Chave | Padrão (Development) | Descrição |
|---|---|---|
| `ConnectionStrings__Postgres` | `Host=localhost;Port=5432;Database=projeto_si;Username=projeto_si;Password=projeto_si_dev` | Banco compartilhado com a ML API. |
| `Jwt__Chave` | chave de desenvolvimento | Mínimo de 32 caracteres. Em produção deve vir de variável de ambiente. |
| `Jwt__MinutosExpiracao` | `60` | Validade do JWT emitido após decisão `GENUINE`. |
| `Biometria__MlApiUrl` | `http://localhost:8000` | URL da ML API. No Compose, `http://ml-api:8000`. |
| `Biometria__AmostrasCadastro` | `5` | Quantidade exata de amostras do cadastro/troca de senha. |
| `Biometria__LimiarConfianca` | `0.35` | Limiar de fallback quando a ML API não retorna um threshold. |
| `Banco__AplicarMigracoesNaInicializacao` | `true` em Development | Aplica migrações do EF Core automaticamente. |
| `SegurancaLogin__MaximoTentativas` | `5` | Falhas (senha ou impostor) antes do bloqueio. |
| `SegurancaLogin__MinutosBloqueio` | `15` | Duração do bloqueio. |
| `DEBUG_LOGS` | `true` no Compose / `false` em produção | Habilita logs estruturados de depuração. |
| `ASPNETCORE_ENVIRONMENT` | `Development` no Compose | Define comportamento de migração/OpenAPI. |

Em produção, `Jwt__Chave` (mínimo 32 caracteres) e `ConnectionStrings__Postgres` precisam vir de variáveis de ambiente; sem a chave JWT a API não inicia e, sem a conexão, as operações de banco falham.

---

## Endpoints

| Método | Rota | Slice | Descrição |
|---|---|---|---|
| GET | `/api/username-availability?username=...` | `Features/Cadastro/VerificarDisponibilidadeUsuario.cs` | Pré-checagem de disponibilidade do username antes da coleta. A unicidade continua garantida por `POST /api/register` (`409` em corrida). |
| POST | `/api/register` | `Features/Cadastro/CadastrarUsuario.cs` | Endpoint único de cadastro: recebe `username`, `password` e as 5 `samples`, cria o usuário, persiste as amostras e treina o modelo inicial. Em falha de treinamento, remove usuário e amostras. |
| POST | `/api/change-password` | `Features/Senha/AlterarSenha.cs` | Recebe `username`, `currentPassword`, `newPassword` e 5 `samples` da nova senha. Valida a senha atual, atualiza o hash e `data_mudanca_senha`, invalida a biometria anterior, treina o novo modelo de forma síncrona e só conclui após sucesso. Em falha, restaura senha/credenciais e remove amostras/modelos novos. |
| POST | `/api/authenticate` | `Features/Login/Autenticar.cs` | Valida `username`, `passwordAttempt` e a amostra, delega a decisão biométrica à ML API, persiste a amostra apenas se `shouldRecordForRetraining` for `true` e emite JWT somente em `GENUINE`. |
| GET | `/api/session` | `Features/Sessao/ValidarSessao.cs` | Valida o JWT (`Authorization: Bearer ...`) e compara a claim `pwd` com a `data_mudanca_senha` atual. Tokens emitidos antes de uma troca de senha passam a ser recusados com `401`. |
| GET | `/api/health` | `Features/Saude/VerificarSaude.cs` | Healthcheck da API. |

OpenAPI em `/openapi/v1.json` (somente em `Development`).

### Exemplo de cadastro

```json
{
  "username": "thiago_dias",
  "password": ".tie5Roanl",
  "samples": [
    {
      "repetition": 1,
      "features": {
        "H.period.0": 0.1491,
        "DD.period.t.0": 0.3979,
        "UD.period.t.0": 0.2488,
        "H.t.0": 0.1069,
        "...": "..."
      },
      "textEntered": ".tie5Roanl",
      "totalDurationMs": 2310.45
    }
  ]
}
```

As chaves de `features` incluem o sufixo sequencial gerado por `computeSample` (`H.t.0`, `DD.t.e.0`, ...). O exemplo acima é abreviado: o backend exige exatamente 5 amostras. `rawTimings` pode ser enviado pelo frontend, mas é ignorado pelo backend. A senha textual **não** é persistida: apenas o hash PBKDF2 é armazenado; as features são convertidas para o vetor posicional `double[]` (`3n - 2`).

### Exemplo de autenticação

```json
{
  "username": "thiago_dias",
  "passwordAttempt": ".tie5Roanl",
  "sample": {
    "repetition": 1,
    "features": { "H.period.0": 0.1510, "DD.period.t.0": 0.3902, "UD.period.t.0": 0.2392, "...": "..." },
    "textEntered": ".tie5Roanl",
    "totalDurationMs": 2280.17
  }
}
```

Resposta `200` (resumida):

```json
{
  "decision": "GENUINE",
  "confidenceScore": 0.931,
  "thresholdScore": 0.68,
  "inferenceTimeMs": 4.82,
  "message": "Padrão biométrico compatível com o perfil legítimo cadastrado.",
  "userAverageHoldTimeMs": 112,
  "attemptAverageHoldTimeMs": 109,
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2026-11-16T18:00:00+00:00"
}
```

Senha textual incorreta, conta bloqueada ou biometria não concluída retornam `200` com `decision: "INVALID_CREDENTIALS"` (sem token); uma tentativa biometricamente divergente retorna `decision: "IMPOSTOR"`.

---

## Decisões de modelagem

- **Senha**: só o hash (PBKDF2-HMAC-SHA512, salt aleatório por usuário embutido no hash, via `PasswordHasher` do ASP.NET Identity).
- **Amostras mascaradas como vetor posicional** (`double precision[]`), montado a partir das `features` enviadas pelo frontend após validar nomes, limites e ordem exata esperada por `computeSample(senha)`: `H.0, DD.0.1, UD.0.1, H.1, ..., H.{n-1}` (tamanho `3n - 2`).
  As chaves nominais de `features` (`H.t.0`, `DD.five.Shift.r.0`, ...) são mascaradas antes da persistência (gravando apenas os valores posicionais em `vetor`, sem caracteres nem padrões de repetição), assim como `textEntered` e `rawTimings` nunca são gravados.
- **Validação de features:** cada amostra precisa ter exatamente as chaves `H`/`DD`/`UD` esperadas para a senha, na ordem cronológica correta, com `H, DD ∈ (0, 5]` segundos e `UD ∈ [-5, 5]` segundos.
- **Persistência seletiva no login**: o backend persiste amostras de `Login` somente quando a ML API retorna `shouldRecordForRetraining: true` (amostra `GENUINE` com confiança acima da banda de incerteza).
- **Amostras da senha vigente**: `coletada_em >= usuarios.data_mudanca_senha`.
- **`modelos`** armazena cada versão treinada com seu próprio `caminho_arquivo`; a ML API escolhe a versão ativa considerando apenas `criado_em >= data_mudanca_senha`, respeitando `active_version.json`, e permite rollback via `/api/models/rollback`.
- **Verificador biométrico** atrás de `IVerificadorBiometrico` (`VerificadorBiometricoMlApi`), delegando o treinamento inicial (`/api/enrollment`) e a inferência biométrica (`/api/authenticate`) para a `projeto_si_ml_api` via HTTP (`Biometria:MlApiUrl`).
- **JWT após troca de senha**: a claim `pwd` é comparada com `data_mudanca_senha` em `GET /api/session`; tokens antigos deixam de valer imediatamente.
- **Bloqueio**: 5 falhas (senha errada ou impostor) bloqueiam por 15 min. Configurável em `SegurancaLogin`.
- **Logs**: logs estruturados em JSON no `stdout` com `X-Correlation-ID`, sem senhas, caracteres digitados, chaves nominais de features ou tokens.

---

## Testes

```bash
dotnet test
```

A suíte cobre domínio, validação/extração de vetores, endpoints de cadastro/login/troca de senha, validação de JWT e integração HTTP com a ML API (com mocks).
