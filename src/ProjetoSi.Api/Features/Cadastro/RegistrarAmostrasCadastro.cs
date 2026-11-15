using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Cadastro;

public static class RegistrarAmostrasCadastro
{
    public sealed record Request(string? Username, IReadOnlyList<AmostraDigitacaoRequest>? Samples);

    public sealed record Response(bool Success, string Message, int SamplesRegistered, double CalculatedAverageHoldMs);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapPost("/enrollment", Executar)
                .WithName(nameof(RegistrarAmostrasCadastro))
                .WithTags("Cadastro");
    }

    public static async Task<Results<Ok<Response>, BadRequest<RespostaOperacao>, JsonHttpResult<RespostaOperacao>, Conflict<RespostaOperacao>>> Executar(
        Request request,
        AppDbContext db,
        VerificadorSenha verificadorSenha,
        IVerificadorBiometrico verificadorBiometrico,
        IOptions<BiometriaOptions> biometria,
        IOptions<SegurancaLoginOptions> seguranca,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        var quantidadeEsperada = biometria.Value.AmostrasCadastro;

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Invalida("Identificador de usuário não informado.");
        }

        if (request.Samples is null || request.Samples.Count != quantidadeEsperada)
        {
            return Invalida($"Número de amostras inválido: recebidas {request.Samples?.Count ?? 0}, necessárias {quantidadeEsperada}.");
        }

        var textosDigitados = request.Samples.Select(amostra => amostra.TextEntered).Distinct().ToArray();

        if (textosDigitados is not [{ Length: > 0 } senhaDigitada])
        {
            return Invalida("Todas as amostras devem conter o mesmo texto digitado.");
        }

        var vetores = new List<VetorCaracteristicas>(request.Samples.Count);

        foreach (var amostra in request.Samples)
        {
            if (!double.IsFinite(amostra.TotalDurationMs) || amostra.TotalDurationMs < 0d)
            {
                return Invalida($"A amostra {amostra.Repetition} possui duração total inválida.");
            }

            if (!VetorCaracteristicas.TentarExtrair(amostra.Features, senhaDigitada, out var vetor))
            {
                return Invalida($"A amostra {amostra.Repetition} possui features inválidas ou incompatíveis com a senha.");
            }

            vetores.Add(vetor);
        }

        var nome = NomeUsuario.Normalizar(request.Username);
        var usuario = await db.Usuarios.SingleOrDefaultAsync(item => item.Nome == nome, cancellationToken);
        var agora = relogio.GetUtcNow();

        if (usuario is { PossuiBiometria: true })
        {
            return TypedResults.Conflict(RespostaOperacao.Falha("A coleta de amostras já foi realizada para a senha atual."));
        }

        if (usuario?.EstaBloqueado(agora) == true)
        {
            return NaoAutorizada("Conta temporariamente bloqueada por excesso de tentativas.");
        }

        if (!verificadorSenha.Verificar(usuario, senhaDigitada))
        {
            if (usuario is not null)
            {
                usuario.RegistrarFalha(agora, seguranca.Value.MaximoTentativas, seguranca.Value.DuracaoBloqueio);
                await db.SaveChangesAsync(cancellationToken);
            }

            return NaoAutorizada("Usuário ou senha das amostras inválidos.");
        }

        db.Amostras.AddRange(request.Samples.Zip(vetores, (amostra, vetor) =>
            Amostra.DeCadastro(usuario.Id, vetor, amostra.Repetition, amostra.TotalDurationMs, agora)));
        usuario.ConcluirCadastroBiometrico(agora);
        usuario.RegistrarSucesso();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(RespostaOperacao.Falha("A coleta de amostras já está sendo processada."));
        }

        await verificadorBiometrico.TreinarModeloInicialAsync(usuario, vetores, cancellationToken);

        var mediaHold = Math.Round(vetores.Average(vetor => vetor.MediaHoldMs), 1);

        return TypedResults.Ok(new Response(
            true,
            $"Lote com {vetores.Count} amostras biométricas registrado para '{usuario.Nome}'.",
            vetores.Count,
            mediaHold));
    }

    private static BadRequest<RespostaOperacao> Invalida(string mensagem) =>
        TypedResults.BadRequest(RespostaOperacao.Falha(mensagem));

    private static JsonHttpResult<RespostaOperacao> NaoAutorizada(string mensagem) =>
        TypedResults.Json(RespostaOperacao.Falha(mensagem), statusCode: StatusCodes.Status401Unauthorized);
}
