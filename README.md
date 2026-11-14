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
| POST | `/api/enrollment` | `Features/Cadastro/RegistrarAmostrasCadastro.cs` |
| POST | `/api/authenticate` | `Features/Login/Autenticar.cs` |
| GET | `/api/health` | `Features/Saude/VerificarSaude.cs` |

OpenAPI em `/openapi/v1.json` (Development).

## Decisões de modelagem

- **Senha**: só o hash (PBKDF2-HMAC-SHA512, 100k iterações, salt aleatório por usuário embutido no hash, via `PasswordHasher` do ASP.NET Identity).
- **Amostras como vetor posicional** (`double precision[]`), calculado no servidor a partir de `rawTimings`, na ordem CMU: `H1, DD1.2, UD1.2, H2, ..., Hn` (tamanho `3n - 2`).
  As chaves de `features` (`H.t`, `DD.five.Shift.r`, ...) contêm os caracteres da senha e colidem quando a senha repete letras, por isso não são persistidas. `textEntered` e `key`/`code` também nunca são gravados.
- **Amostras da senha vigente**: `coletada_em >= usuarios.data_mudanca_senha`.
- **`modelos`** junta Modelos + Versão do rascunho: cada versão tem seu próprio `caminho_arquivo`; versão ativa = maior `versao` do usuário.
- **Verificador biométrico** atrás de `IVerificadorBiometrico`. A implementação padrão é Manhattan escalonado (Killourhy & Maxion) sobre as amostras de cadastro; o modelo treinado (RF/k-NN/MLP) entra trocando o registro no `Program.cs`.
- **Bloqueio**: 5 falhas (senha errada ou impostor) bloqueiam por 15 min. Configurável em `SegurancaLogin`.

## Configuração de produção

`Jwt__Chave` (mínimo 32 caracteres) e `ConnectionStrings__Postgres` precisam vir de variáveis de ambiente; sem elas a API não inicia.
