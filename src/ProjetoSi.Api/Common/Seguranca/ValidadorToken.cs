using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Common.Seguranca;

public sealed record ResultadoValidacaoToken(bool Valido, Usuario? Usuario);

public sealed class ValidadorToken(
    IOptions<JwtOptions> opcoes,
    AppDbContext db,
    ILogger<ValidadorToken> logger)
{
    private readonly JsonWebTokenHandler _manipulador = new();
    private readonly JwtOptions _configuracao = opcoes.Value;

    public async Task<ResultadoValidacaoToken> ValidarAsync(string token, CancellationToken cancellationToken)
    {
        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _configuracao.Emissor,
            ValidateAudience = true,
            ValidAudience = _configuracao.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuracao.Chave)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var resultado = await _manipulador.ValidateTokenAsync(token, parametros);
            if (!resultado.IsValid)
            {
                return new ResultadoValidacaoToken(false, null);
            }

            var subjetivo = resultado.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(subjetivo, out var usuarioId))
            {
                return new ResultadoValidacaoToken(false, null);
            }

            var usuario = await db.Usuarios.SingleOrDefaultAsync(item => item.Id == usuarioId, cancellationToken);

            if (usuario is null)
            {
                return new ResultadoValidacaoToken(false, null);
            }

            var senhaEmitida = resultado.ClaimsIdentity.FindFirst("pwd")?.Value;

            if (!long.TryParse(senhaEmitida, out var senhaEmitidaMs))
            {
                return new ResultadoValidacaoToken(false, null);
            }

            return senhaEmitidaMs == usuario.DataMudancaSenha.ToUnixTimeMilliseconds()
                ? new ResultadoValidacaoToken(true, usuario)
                : new ResultadoValidacaoToken(false, null);
        }
        catch (Exception excecao)
        {
            logger.LogDebug(
                excecao,
                "evento={Evento}",
                "backend.token.validacao_falhou");
            return new ResultadoValidacaoToken(false, null);
        }
    }
}
