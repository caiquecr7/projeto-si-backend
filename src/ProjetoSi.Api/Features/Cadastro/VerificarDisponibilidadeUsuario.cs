using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Cadastro;

public static class VerificarDisponibilidadeUsuario
{
    public sealed record Response(string Username, bool Available);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapGet("/username-availability", Executar)
                .WithName(nameof(VerificarDisponibilidadeUsuario))
                .WithTags("Cadastro");
    }

    public static async Task<Results<Ok<Response>, BadRequest<RespostaOperacao>>> Executar(
        string? username,
        AppDbContext db,
        ILogger<Endpoint> logger,
        CancellationToken cancellationToken)
    {
        if (ValidarNome(username, out var nome) is { } erro)
        {
            return TypedResults.BadRequest(RespostaOperacao.Falha(erro));
        }

        var disponivel = !await db.Usuarios.AnyAsync(usuario => usuario.Nome == nome, cancellationToken);

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} username={Username} disponivel={Disponivel}",
            "backend.register.username_availability",
            "register",
            nome,
            disponivel);

        return TypedResults.Ok(new Response(nome, disponivel));
    }

    internal static string? ValidarNome(string? username, out string nome)
    {
        nome = string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            return "Informe o nome de usuário.";
        }

        nome = NomeUsuario.Normalizar(username);

        if (!NomeUsuario.EhValido(nome))
        {
            return $"O usuário deve ter entre {NomeUsuario.TamanhoMinimo} e {NomeUsuario.TamanhoMaximo} caracteres, usando letras, números ou . _ @ + -";
        }

        return null;
    }
}
