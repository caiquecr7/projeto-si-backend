using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Cadastro;

public static class CadastrarUsuario
{
    public const int TamanhoMinimoSenha = 8;
    public const int TamanhoMaximoSenha = 64;

    public sealed record Request(
        string? Username,
        string? Password,
        IReadOnlyList<AmostraDigitacaoRequest>? Samples);

    public sealed record Response(bool Success, string Message, int SamplesRegistered, double CalculatedAverageHoldMs);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapPost("/register", Executar)
                .WithName(nameof(CadastrarUsuario))
                .WithTags("Cadastro");
    }

    public static async Task<Results<Created<Response>, BadRequest<RespostaOperacao>, Conflict<RespostaOperacao>>> Executar(
        Request request,
        AppDbContext db,
        VerificadorSenha verificadorSenha,
        IVerificadorBiometrico verificadorBiometrico,
        IOptions<BiometriaOptions> biometria,
        TimeProvider relogio,
        ILogger<Endpoint> logger,
        CancellationToken cancellationToken)
    {
        var quantidadeEsperada = biometria.Value.AmostrasCadastro;

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} usernameInformado={UsernameInformado} tamanhoSenha={TamanhoSenha} amostrasRecebidas={AmostrasRecebidas} amostrasEsperadas={AmostrasEsperadas}",
            "backend.register.iniciado",
            "register",
            !string.IsNullOrWhiteSpace(request.Username),
            request.Password?.Length ?? 0,
            request.Samples?.Count ?? 0,
            quantidadeEsperada);

        if (Validar(request, quantidadeEsperada, out var vetores) is { } erro)
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} motivo={Motivo}",
                "backend.register.validacao_falhou",
                "register",
                erro);
            return TypedResults.BadRequest(RespostaOperacao.Falha(erro));
        }

        var nome = NomeUsuario.Normalizar(request.Username!);

        if (await db.Usuarios.AnyAsync(usuario => usuario.Nome == nome, cancellationToken))
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} username={Username} motivo={Motivo}",
                "backend.register.usuario_duplicado",
                "register",
                nome,
                "username_indisponivel");
            return Conflito();
        }

        var agora = relogio.GetUtcNow();
        var usuario = Usuario.Criar(nome, verificadorSenha.GerarHash(request.Password!), agora);
        usuario.ConcluirCadastroBiometrico(agora);
        usuario.RegistrarSucesso();

        db.Usuarios.Add(usuario);
        db.Amostras.AddRange(request.Samples!.Zip(vetores, (amostra, vetor) =>
            Amostra.DeCadastro(usuario.Id, vetor, amostra.Repetition, amostra.TotalDurationMs, agora)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} username={Username} motivo={Motivo}",
                "backend.register.usuario_duplicado",
                "register",
                nome,
                "unique_violation");
            return Conflito();
        }

        var mediaHold = Math.Round(vetores.Average(vetor => vetor.MediaHoldMs), 1);

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} amostras={Amostras} dimensaoVetor={DimensaoVetor} mediaHoldMs={MediaHoldMs}",
            "backend.register.vetores_extraidos",
            "register",
            usuario.Id,
            usuario.Nome,
            vetores.Count,
            vetores[0].Valores.Length,
            mediaHold);

        var modeloId = await verificadorBiometrico.TreinarModeloInicialAsync(usuario, vetores, cancellationToken);

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} modelId={ModelId} samplesRegistered={SamplesRegistered} mediaHoldMs={MediaHoldMs}",
            "backend.register.concluido",
            "register",
            usuario.Id,
            usuario.Nome,
            modeloId,
            vetores.Count,
            mediaHold);

        return TypedResults.Created((string?)null, new Response(
            true,
            $"Usuário '{nome}' cadastrado com {vetores.Count} amostras biométricas.",
            vetores.Count,
            mediaHold));
    }

    internal static string? Validar(
        Request request,
        int quantidadeEsperada,
        out IReadOnlyList<VetorCaracteristicas> vetores)
    {
        vetores = [];

        var senha = request.Password;

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(senha))
        {
            return "Campos obrigatórios ausentes: username e password.";
        }

        if (!NomeUsuario.EhValido(NomeUsuario.Normalizar(request.Username)))
        {
            return $"O usuário deve ter entre {NomeUsuario.TamanhoMinimo} e {NomeUsuario.TamanhoMaximo} caracteres, usando letras, números ou . _ @ + -";
        }

        if (senha.Length is < TamanhoMinimoSenha or > TamanhoMaximoSenha)
        {
            return $"A senha deve ter entre {TamanhoMinimoSenha} e {TamanhoMaximoSenha} caracteres.";
        }

        if (request.Samples is null || request.Samples.Count != quantidadeEsperada)
        {
            return $"Número de amostras inválido: recebidas {request.Samples?.Count ?? 0}, necessárias {quantidadeEsperada}.";
        }

        var textosDigitados = request.Samples.Select(amostra => amostra.TextEntered).Distinct().ToArray();

        if (textosDigitados is not [{ Length: > 0 }])
        {
            return "Todas as amostras devem conter o mesmo texto digitado.";
        }

        if (!string.Equals(textosDigitados[0], senha, StringComparison.Ordinal))
        {
            return "As amostras não correspondem à senha informada.";
        }

        var extraidos = new List<VetorCaracteristicas>(request.Samples.Count);

        foreach (var amostra in request.Samples)
        {
            if (!double.IsFinite(amostra.TotalDurationMs) || amostra.TotalDurationMs < 0d)
            {
                return $"A amostra {amostra.Repetition} possui duração total inválida.";
            }

            if (!VetorCaracteristicas.TentarExtrair(amostra.Features, senha, out var vetor))
            {
                return $"A amostra {amostra.Repetition} possui features inválidas ou incompatíveis com a senha.";
            }

            extraidos.Add(vetor);
        }

        vetores = extraidos;
        return null;
    }

    private static Conflict<RespostaOperacao> Conflito() =>
        TypedResults.Conflict(RespostaOperacao.Falha("Nome de usuário indisponível."));
}
