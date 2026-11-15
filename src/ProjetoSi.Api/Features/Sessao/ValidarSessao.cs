using Microsoft.AspNetCore.Http.HttpResults;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;

namespace ProjetoSi.Api.Features.Sessao;

public static class ValidarSessao
{
    public sealed record Response(string Username);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapGet("/session", Executar)
                .WithName(nameof(ValidarSessao))
                .WithTags("Sessao");
    }

    public static async Task<Results<Ok<Response>, JsonHttpResult<RespostaOperacao>>> Executar(
        HttpRequest requisicao,
        ValidadorToken validadorToken,
        CancellationToken cancellationToken)
    {
        var autorizacao = requisicao.Headers.Authorization.ToString();

        if (!autorizacao.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return NaoAutorizada();
        }

        var token = autorizacao["Bearer ".Length..].Trim();
        var resultado = await validadorToken.ValidarAsync(token, cancellationToken);

        if (!resultado.Valido || resultado.Usuario is null)
        {
            return NaoAutorizada();
        }

        return TypedResults.Ok(new Response(resultado.Usuario.Nome));
    }

    private static JsonHttpResult<RespostaOperacao> NaoAutorizada() =>
        TypedResults.Json(
            RespostaOperacao.Falha("Sessão inválida ou expirada."),
            statusCode: StatusCodes.Status401Unauthorized);
}
