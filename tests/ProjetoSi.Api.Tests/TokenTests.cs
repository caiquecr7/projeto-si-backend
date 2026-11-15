using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Tests;

public class TokenTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Token_inclui_data_de_mudanca_de_senha_para_invalidacao()
    {
        var opcoes = Options.Create(new JwtOptions
        {
            Emissor = "projeto-si-api",
            Audiencia = "projeto-si-frontend",
            Chave = new string('x', 32),
            MinutosExpiracao = 60
        });
        var usuario = Usuario.Criar("ana", "hash", Agora);
        var token = new GeradorToken(opcoes, TimeProvider.System).Gerar(usuario);

        var jwt = new JsonWebToken(token.Token);

        Assert.Equal(
            usuario.DataMudancaSenha.ToUnixTimeMilliseconds().ToString(),
            jwt.GetClaim("pwd").Value);
    }
}
