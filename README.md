# projeto_si_backend

API de cadastro, coleta de amostras e login biométrico por dinâmica de digitação.
.NET 10, Minimal API, Vertical Slice, PostgreSQL (EF Core + Npgsql).

## Executar

```bash
docker compose up -d postgres
dotnet run --project src/ProjetoSi.Api
```

A API sobe em `http://localhost:5000` (mesmo alvo do `proxy.conf.json` do frontend) e aplica as migrações ao iniciar em Development.
Tudo em container: `docker compose up --build`.

Testes: `dotnet test`.

## Endpoints (contrato do `mock_api` do frontend)

| Método | Rota | Slice |
|---|---|---|
| POST | `/api/register` | `Features/Cadastro/CadastrarUsuario.cs` |
| POST | `/api/authenticate` | `Features/Login/Autenticar.cs` |
| GET | `/api/health` | `Features/Saude/VerificarSaude.cs` |

O `POST /api/register` é o endpoint único de cadastro: recebe `username`, `password` e as 5 `samples` de digitação, cria o usuário, persiste as amostras e treina o modelo inicial.

OpenAPI em `/openapi/v1.json` (Development).

## Decisões de modelagem

- **Senha**: só o hash (PBKDF2-HMAC-SHA512, 100k iterações, salt aleatório por usuário embutido no hash, via `PasswordHasher` do ASP.NET Identity).
- **Amostras mascaradas como vetor posicional** (`double precision[]`), montado a partir das `features` enviadas pelo frontend após validar nomes, limites e ordem exata esperada por `computeSample(senha)`: `H.0, DD.0.1, UD.0.1, H.1, ..., H.{n-1}` (tamanho `3n - 2`).
  As chaves nominais de `features` (`H.t.0`, `DD.five.Shift.r.0`, ...) são mascaradas antes da persistência (gravando apenas os valores posicionais em `vetor`, sem caracteres nem padrões de repetição), assim como `textEntered` e `rawTimings` nunca são gravados.
- **Persistência seletiva no login**: o backend persiste amostras de `Login` somente quando a ML API retorna `shouldRecordForRetraining: true`.
- **Amostras da senha vigente**: `coletada_em >= usuarios.data_mudanca_senha`.
- **`modelos`** junta Modelos + Versão do rascunho: cada versão tem seu próprio `caminho_arquivo`; versão ativa = maior `versao` do usuário.
- **Verificador biométrico** atrás de `IVerificadorBiometrico` (`VerificadorBiometricoMlApi`), delegando o treinamento inicial (`/api/enrollment`) e a inferência biométrica (`/api/authenticate`) para a `projeto_si_ml_api` via HTTP (`Biometria:MlApiUrl`).
- **Bloqueio**: 5 falhas (senha errada ou impostor) bloqueiam por 15 min. Configurável em `SegurancaLogin`.

## Configuração de produção

`Jwt__Chave` (mínimo 32 caracteres) e `ConnectionStrings__Postgres` precisam vir de variáveis de ambiente; sem elas a API não inicia.
