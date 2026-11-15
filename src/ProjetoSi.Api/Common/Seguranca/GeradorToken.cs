using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Common.Seguranca;

public sealed record TokenAcesso(string Token, DateTimeOffset ExpiraEm);

public sealed class GeradorToken(IOptions<JwtOptions> opcoes, TimeProvider relogio)
{
    private readonly JsonWebTokenHandler manipulador = new();

    public TokenAcesso Gerar(Usuario usuario)
    {
        var configuracao = opcoes.Value;
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.AddMinutes(configuracao.MinutosExpiracao);
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuracao.Chave));

        var token = manipulador.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuracao.Emissor,
            Audience = configuracao.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [JwtRegisteredClaimNames.UniqueName] = usuario.Nome,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["pwd"] = usuario.DataMudancaSenha.ToUnixTimeMilliseconds()
            },
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)
        });

        return new TokenAcesso(token, expiraEm);
    }
}
