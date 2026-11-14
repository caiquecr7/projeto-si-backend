using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Cadastro;

public static class CadastrarUsuario
{
    public const int TamanhoMinimoSenha = 8;
    public const int TamanhoMaximoSenha = 64;

    public sealed record Request(string? Username, string? TargetPassword, string? ConfirmPassword);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapPost("/register", Executar)
                .WithName(nameof(CadastrarUsuario))
                .WithTags("Cadastro");
    }

    public static async Task<Results<Created<RespostaOperacao>, BadRequest<RespostaOperacao>, Conflict<RespostaOperacao>>> Executar(
        Request request,
        AppDbContext db,
        VerificadorSenha verificadorSenha,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        if (Validar(request) is { } erro)
        {
            return TypedResults.BadRequest(RespostaOperacao.Falha(erro));
        }

        var nome = NomeUsuario.Normalizar(request.Username!);

        if (await db.Usuarios.AnyAsync(usuario => usuario.Nome == nome, cancellationToken))
        {
            return Conflito();
        }

        var usuario = Usuario.Criar(nome, verificadorSenha.GerarHash(request.TargetPassword!), relogio.GetUtcNow());
        db.Usuarios.Add(usuario);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflito();
        }

        return TypedResults.Created((string?)null, RespostaOperacao.Sucesso($"Usuário '{nome}' cadastrado com sucesso."));
    }

    private static Conflict<RespostaOperacao> Conflito() =>
        TypedResults.Conflict(RespostaOperacao.Falha("Nome de usuário indisponível."));

    private static string? Validar(Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.TargetPassword))
        {
            return "Campos obrigatórios ausentes: username e targetPassword.";
        }

        if (!NomeUsuario.EhValido(NomeUsuario.Normalizar(request.Username)))
        {
            return $"O usuário deve ter entre {NomeUsuario.TamanhoMinimo} e {NomeUsuario.TamanhoMaximo} caracteres, usando letras, números ou . _ @ + -";
        }

        if (request.TargetPassword.Length is < TamanhoMinimoSenha or > TamanhoMaximoSenha)
        {
            return $"A senha deve ter entre {TamanhoMinimoSenha} e {TamanhoMaximoSenha} caracteres.";
        }

        if (request.TargetPassword != request.ConfirmPassword)
        {
            return "A confirmação de senha não confere.";
        }

        return null;
    }
}
