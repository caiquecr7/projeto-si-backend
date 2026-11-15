using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Common;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Common.Endpoints;
using ProjetoSi.Api.Common.Seguranca;
using ProjetoSi.Api.Domain;
using ProjetoSi.Api.Infrastructure.Persistence;

namespace ProjetoSi.Api.Features.Login;

public static class Autenticar
{
    public sealed record Request(string? Username, string? PasswordAttempt, AmostraDigitacaoRequest? Sample);

    public sealed record Response(
        DecisaoBiometrica Decision,
        double ConfidenceScore,
        double ThresholdScore,
        double InferenceTimeMs,
        string Message,
        int UserAverageHoldTimeMs,
        int AttemptAverageHoldTimeMs,
        string? AccessToken,
        DateTimeOffset? AccessTokenExpiresAt);

    public sealed class Endpoint : IEndpoint
    {
        public void Mapear(IEndpointRouteBuilder rotas) =>
            rotas.MapPost("/authenticate", Executar)
                .WithName(nameof(Autenticar))
                .WithTags("Login");
    }

    public static async Task<Results<Ok<Response>, BadRequest<RespostaOperacao>, Conflict<RespostaOperacao>>> Executar(
        Request request,
        AppDbContext db,
        VerificadorSenha verificadorSenha,
        IVerificadorBiometrico verificadorBiometrico,
        GeradorToken geradorToken,
        IOptions<BiometriaOptions> biometria,
        IOptions<SegurancaLoginOptions> seguranca,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.PasswordAttempt))
        {
            return TypedResults.BadRequest(RespostaOperacao.Falha("Campos obrigatórios ausentes: username e passwordAttempt."));
        }

        if (request.Sample is not { } amostra || !double.IsFinite(amostra.TotalDurationMs) || amostra.TotalDurationMs < 0d)
        {
            return TypedResults.BadRequest(RespostaOperacao.Falha("A amostra de digitação é obrigatória e deve conter duração total válida."));
        }

        if (!string.IsNullOrEmpty(amostra.TextEntered) && !string.Equals(amostra.TextEntered, request.PasswordAttempt, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest(RespostaOperacao.Falha("O texto informado na amostra não coincide com passwordAttempt."));
        }

        if (!VetorCaracteristicas.TentarExtrair(amostra.Features, request.PasswordAttempt, out var tentativa))
        {
            return TypedResults.BadRequest(RespostaOperacao.Falha("As features da amostra de digitação são inválidas ou incompatíveis com a senha informada."));
        }

        var limiar = biometria.Value.LimiarConfianca;
        var regras = seguranca.Value;
        var agora = relogio.GetUtcNow();
        var nome = NomeUsuario.Normalizar(request.Username);
        var usuario = await db.Usuarios.SingleOrDefaultAsync(item => item.Nome == nome, cancellationToken);

        if (usuario?.EstaBloqueado(agora) == true)
        {
            return TypedResults.Ok(CredenciaisInvalidas("Conta temporariamente bloqueada por excesso de tentativas.", limiar, tentativa));
        }

        if (!verificadorSenha.Verificar(usuario, request.PasswordAttempt))
        {
            if (usuario is not null)
            {
                usuario.RegistrarFalha(agora, regras.MaximoTentativas, regras.DuracaoBloqueio);
                await db.SaveChangesAsync(cancellationToken);
            }

            return TypedResults.Ok(CredenciaisInvalidas("Usuário ou senha incorretos.", limiar, tentativa));
        }

        if (!usuario.PossuiBiometria)
        {
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(CredenciaisInvalidas("A coleta de amostras de digitação ainda não foi concluída.", limiar, tentativa));
        }

        var inicioInferencia = relogio.GetTimestamp();
        var resultado = await verificadorBiometrico.VerificarAsync(usuario, tentativa, cancellationToken);
        var tempoInferencia = relogio.GetElapsedTime(inicioInferencia);

        if (resultado.DeveGravarParaRetreino)
        {
            db.Amostras.Add(Amostra.DeLogin(usuario.Id, tentativa, amostra.TotalDurationMs, resultado, agora));
        }

        TokenAcesso? token = null;

        if (resultado.Decisao == DecisaoBiometrica.Genuine)
        {
            usuario.RegistrarSucesso();
            token = geradorToken.Gerar(usuario);
        }
        else
        {
            usuario.RegistrarFalha(agora, regras.MaximoTentativas, regras.DuracaoBloqueio);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict(RespostaOperacao.Falha("Outra tentativa de login está em andamento. Tente novamente."));
        }

        return TypedResults.Ok(new Response(
            resultado.Decisao,
            Math.Round(resultado.Confianca, 3),
            resultado.Limiar,
            Math.Round(tempoInferencia.TotalMilliseconds, 2),
            MensagemPara(resultado.Decisao),
            (int)Math.Round(resultado.MediaHoldPerfilMs),
            (int)Math.Round(tentativa.MediaHoldMs),
            token?.Token,
            token?.ExpiraEm));
    }

    private static string MensagemPara(DecisaoBiometrica decisao) => decisao == DecisaoBiometrica.Genuine
        ? "Padrão biométrico compatível com o perfil legítimo cadastrado."
        : "Acesso bloqueado: o ritmo de digitação diverge do perfil legítimo.";

    private static Response CredenciaisInvalidas(string mensagem, double limiar, VetorCaracteristicas tentativa) => new(
        DecisaoBiometrica.InvalidCredentials,
        0,
        limiar,
        0,
        mensagem,
        0,
        (int)Math.Round(tentativa.MediaHoldMs),
        null,
        null);
}
