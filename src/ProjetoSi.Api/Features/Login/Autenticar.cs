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
        ILogger<Endpoint> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} username={Username} tamanhoSenha={TamanhoSenha} possuiAmostra={PossuiAmostra} duracaoTotalMs={DuracaoTotalMs}",
            "backend.login.iniciado",
            "login",
            request.Username,
            request.PasswordAttempt?.Length ?? 0,
            request.Sample is not null,
            request.Sample?.TotalDurationMs);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.PasswordAttempt))
        {
            logger.LogDebug("evento={Evento} fluxo={Fluxo} motivo={Motivo}", "backend.login.validacao_falhou", "login", "credenciais_ausentes");
            return TypedResults.BadRequest(RespostaOperacao.Falha("Campos obrigatórios ausentes: username e passwordAttempt."));
        }

        if (request.Sample is not { } amostra || !double.IsFinite(amostra.TotalDurationMs) || amostra.TotalDurationMs < 0d)
        {
            logger.LogDebug("evento={Evento} fluxo={Fluxo} username={Username} motivo={Motivo}", "backend.login.validacao_falhou", "login", request.Username, "amostra_ou_duracao_invalida");
            return TypedResults.BadRequest(RespostaOperacao.Falha("A amostra de digitação é obrigatória e deve conter duração total válida."));
        }

        if (!string.IsNullOrEmpty(amostra.TextEntered) && !string.Equals(amostra.TextEntered, request.PasswordAttempt, StringComparison.Ordinal))
        {
            logger.LogDebug("evento={Evento} fluxo={Fluxo} username={Username} motivo={Motivo}", "backend.login.validacao_falhou", "login", request.Username, "texto_divergente");
            return TypedResults.BadRequest(RespostaOperacao.Falha("O texto informado na amostra não coincide com passwordAttempt."));
        }

        if (!VetorCaracteristicas.TentarExtrair(amostra.Features, request.PasswordAttempt, out var tentativa))
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} username={Username} featuresRecebidas={FeaturesRecebidas} tamanhoSenha={TamanhoSenha} motivo={Motivo}",
                "backend.login.validacao_falhou",
                "login",
                request.Username,
                amostra.Features?.Count ?? 0,
                request.PasswordAttempt.Length,
                "features_invalidas_ou_incompativeis");
            return TypedResults.BadRequest(RespostaOperacao.Falha("As features da amostra de digitação são inválidas ou incompatíveis com a senha informada."));
        }

        var limiar = biometria.Value.LimiarConfianca;
        var regras = seguranca.Value;
        var agora = relogio.GetUtcNow();
        var nome = NomeUsuario.Normalizar(request.Username);
        var usuario = await db.Usuarios.SingleOrDefaultAsync(item => item.Nome == nome, cancellationToken);

        if (usuario?.EstaBloqueado(agora) == true)
        {
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} bloqueadoAte={BloqueadoAte}",
                "backend.login.bloqueado",
                "login",
                usuario.Id,
                nome,
                usuario.BloqueadoAte);
            return TypedResults.Ok(CredenciaisInvalidas("Conta temporariamente bloqueada por excesso de tentativas.", limiar, tentativa));
        }

        if (!verificadorSenha.Verificar(usuario, request.PasswordAttempt))
        {
            if (usuario is not null)
            {
                usuario.RegistrarFalha(agora, regras.MaximoTentativas, regras.DuracaoBloqueio);
                await db.SaveChangesAsync(cancellationToken);
            }

            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} username={Username} usuarioEncontrado={UsuarioEncontrado} tentativasFalhas={TentativasFalhas} bloqueadoAte={BloqueadoAte}",
                "backend.login.senha_invalida",
                "login",
                nome,
                usuario is not null,
                usuario?.TentativasFalhas ?? 0,
                usuario?.BloqueadoAte);
            return TypedResults.Ok(CredenciaisInvalidas("Usuário ou senha incorretos.", limiar, tentativa));
        }

        if (!usuario.PossuiBiometria)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username}",
                "backend.login.sem_biometria",
                "login",
                usuario.Id,
                nome);
            return TypedResults.Ok(CredenciaisInvalidas("A coleta de amostras de digitação ainda não foi concluída.", limiar, tentativa));
        }

        var inicioInferencia = relogio.GetTimestamp();
        var resultado = await verificadorBiometrico.VerificarAsync(usuario, tentativa, cancellationToken);
        var tempoInferencia = relogio.GetElapsedTime(inicioInferencia);

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} decisao={Decisao} confianca={Confianca} limiar={Limiar} inferenciaMs={InferenciaMs} modeloId={ModeloId} gravarParaRetreino={GravarParaRetreino} mediaHoldPerfilMs={MediaHoldPerfilMs} mediaHoldTentativaMs={MediaHoldTentativaMs}",
            "backend.login.inferencia_concluida",
            "login",
            usuario.Id,
            usuario.Nome,
            resultado.Decisao,
            Math.Round(resultado.Confianca, 4),
            Math.Round(resultado.Limiar, 4),
            Math.Round(tempoInferencia.TotalMilliseconds, 2),
            resultado.ModeloId,
            resultado.DeveGravarParaRetreino,
            Math.Round(resultado.MediaHoldPerfilMs, 1),
            Math.Round(tentativa.MediaHoldMs, 1));

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
            logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username}",
                "backend.login.conflito_concorrencia",
                "login",
                usuario.Id,
                usuario.Nome);
            return TypedResults.Conflict(RespostaOperacao.Falha("Outra tentativa de login está em andamento. Tente novamente."));
        }

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} decisao={Decisao} tokenEmitido={TokenEmitido} amostraPersistida={AmostraPersistida} tentativasFalhas={TentativasFalhas} bloqueadoAte={BloqueadoAte}",
            "backend.login.concluido",
            "login",
            usuario.Id,
            usuario.Nome,
            resultado.Decisao,
            token is not null,
            resultado.DeveGravarParaRetreino,
            usuario.TentativasFalhas,
            usuario.BloqueadoAte);

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
