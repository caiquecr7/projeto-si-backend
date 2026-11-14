using Microsoft.AspNetCore.Http.HttpResults;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Saude;

public static class VerificarSaude
{
    private const string NomeServico = "Keystroke Dynamics Biometrics API";

    public sealed record Response(string Status, string Service, DateTimeOffset Timestamp);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapGet("/health", Executar)
                .WithName(nameof(VerificarSaude))
                .WithTags("Saúde");
    }

    public static async Task<Results<Ok<Response>, JsonHttpResult<Response>>> Executar(
        AppDbContext db,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        if (await db.Database.CanConnectAsync(cancellationToken))
        {
            return TypedResults.Ok(new Response("healthy", NomeServico, relogio.GetUtcNow()));
        }

        return TypedResults.Json(new Response("unhealthy", NomeServico, relogio.GetUtcNow()), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
