using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Common.Biometria;

public sealed class VerificadorBiometricoMlApi : IVerificadorBiometrico
{
    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly BiometriaOptions _opcoes;
    private readonly ILogger<VerificadorBiometricoMlApi> _logger;
    private readonly TimeProvider _relogio;

    public VerificadorBiometricoMlApi(
        HttpClient httpClient,
        IOptions<BiometriaOptions> opcoes,
        ILogger<VerificadorBiometricoMlApi>? logger = null,
        TimeProvider? relogio = null)
    {
        _httpClient = httpClient;
        _opcoes = opcoes.Value;
        _logger = logger ?? NullLogger<VerificadorBiometricoMlApi>.Instance;
        _relogio = relogio ?? TimeProvider.System;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_opcoes.MlApiUrl))
        {
            _httpClient.BaseAddress = new Uri(_opcoes.MlApiUrl);
        }
    }

    public async Task<long?> TreinarModeloInicialAsync(
        Usuario usuario,
        IReadOnlyList<VetorCaracteristicas> vetores,
        CancellationToken cancellationToken)
    {
        var payload = new RequisicaoTreinamentoMlApi(
            usuario.Nome,
            usuario.Id,
            vetores.Select(vetor => vetor.Valores).ToArray(),
            RecordSamples: false);

        var dimensao = vetores.Count > 0 ? vetores[0].Valores.Length : 0;
        var inicio = _relogio.GetTimestamp();
        _logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} amostras={Amostras} dimensaoVetor={DimensaoVetor} endpoint={Endpoint}",
            "backend.ml_api.enrollment_enviando",
            "enrollment",
            usuario.Id,
            usuario.Nome,
            vetores.Count,
            dimensao,
            "/api/enrollment");

        using var respostaHttp = await _httpClient.PostAsJsonAsync("/api/enrollment", payload, JsonOpcoes, cancellationToken);
        var duracaoMs = Math.Round(_relogio.GetElapsedTime(inicio).TotalMilliseconds, 2);

        if (!respostaHttp.IsSuccessStatusCode)
        {
            _logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} statusCode={StatusCode} duracaoMs={DuracaoMs}",
                "backend.ml_api.erro_http",
                "enrollment",
                usuario.Id,
                usuario.Nome,
                (int)respostaHttp.StatusCode,
                duracaoMs);
        }

        respostaHttp.EnsureSuccessStatusCode();

        var resposta = await respostaHttp.Content.ReadFromJsonAsync<RespostaTreinamentoMlApi>(JsonOpcoes, cancellationToken);
        _logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} statusCode={StatusCode} modelId={ModelId} modelVersion={ModelVersion} duracaoMs={DuracaoMs}",
            "backend.ml_api.enrollment_resposta",
            "enrollment",
            usuario.Id,
            usuario.Nome,
            (int)respostaHttp.StatusCode,
            resposta?.ModelId,
            resposta?.ModelVersion,
            duracaoMs);
        return resposta?.ModelId;
    }

    public async Task<ResultadoVerificacao> VerificarAsync(
        Usuario usuario,
        VetorCaracteristicas tentativa,
        CancellationToken cancellationToken)
    {
        var payload = new RequisicaoAutenticacaoMlApi(
            usuario.Nome,
            usuario.Id,
            tentativa.Valores,
            RecordSample: false);

        var inicio = _relogio.GetTimestamp();
        _logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} dimensaoVetor={DimensaoVetor} endpoint={Endpoint}",
            "backend.ml_api.authenticate_enviando",
            "login",
            usuario.Id,
            usuario.Nome,
            tentativa.Valores.Length,
            "/api/authenticate");

        using var respostaHttp = await _httpClient.PostAsJsonAsync("/api/authenticate", payload, JsonOpcoes, cancellationToken);
        var duracaoMs = Math.Round(_relogio.GetElapsedTime(inicio).TotalMilliseconds, 2);

        if (!respostaHttp.IsSuccessStatusCode)
        {
            _logger.LogDebug(
                "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} statusCode={StatusCode} duracaoMs={DuracaoMs}",
                "backend.ml_api.erro_http",
                "login",
                usuario.Id,
                usuario.Nome,
                (int)respostaHttp.StatusCode,
                duracaoMs);
        }

        respostaHttp.EnsureSuccessStatusCode();

        var resposta = await respostaHttp.Content.ReadFromJsonAsync<RespostaAutenticacaoMlApi>(JsonOpcoes, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da API de ML na verificação biométrica.");

        var decisao = MapearDecisao(resposta.Decision);
        var confianca = resposta.Confidence ?? resposta.ConfidenceScore ?? 0d;
        var limiar = resposta.Threshold ?? resposta.ThresholdScore ?? _opcoes.LimiarConfianca;
        var mediaHoldPerfilMs = resposta.UserAverageHoldTimeMs ?? 0d;
        var deveGravarParaRetreino = resposta.ShouldRecordForRetraining ?? false;

        _logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} userId={UserId} username={Username} statusCode={StatusCode} decisao={Decisao} confianca={Confianca} limiar={Limiar} modelId={ModelId} gravarParaRetreino={GravarParaRetreino} duracaoMs={DuracaoMs}",
            "backend.ml_api.authenticate_resposta",
            "login",
            usuario.Id,
            usuario.Nome,
            (int)respostaHttp.StatusCode,
            decisao,
            Math.Round(confianca, 4),
            Math.Round(limiar, 4),
            resposta.ModelId,
            deveGravarParaRetreino,
            duracaoMs);

        return new ResultadoVerificacao(decisao, confianca, limiar, mediaHoldPerfilMs, resposta.ModelId, deveGravarParaRetreino);
    }

    private static DecisaoBiometrica MapearDecisao(string? decisao) =>
        decisao?.Trim().ToUpperInvariant() switch
        {
            "GENUINE" => DecisaoBiometrica.Genuine,
            "INVALID_CREDENTIALS" => DecisaoBiometrica.InvalidCredentials,
            _ => DecisaoBiometrica.Impostor
        };

    public sealed record RequisicaoTreinamentoMlApi(
        string Username,
        Guid UserId,
        IReadOnlyList<double[]> Vectors,
        bool RecordSamples = false);

    public sealed record RespostaTreinamentoMlApi(
        long? ModelId,
        int? ModelVersion,
        bool Success = true,
        string? Message = null);

    public sealed record RequisicaoAutenticacaoMlApi(
        string Username,
        Guid UserId,
        double[] Vector,
        bool RecordSample = false);

    public sealed record RespostaAutenticacaoMlApi(
        string? Decision,
        double? Confidence,
        double? ConfidenceScore,
        double? Threshold,
        double? ThresholdScore,
        double? UserAverageHoldTimeMs,
        long? ModelId,
        bool? ShouldRecordForRetraining = null);
}
