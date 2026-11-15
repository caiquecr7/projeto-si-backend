using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Common.Biometria;

public sealed class VerificadorBiometricoMlApi : IVerificadorBiometrico
{
    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly BiometriaOptions _opcoes;

    public VerificadorBiometricoMlApi(HttpClient httpClient, IOptions<BiometriaOptions> opcoes)
    {
        _httpClient = httpClient;
        _opcoes = opcoes.Value;

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

        using var respostaHttp = await _httpClient.PostAsJsonAsync("/api/enrollment", payload, JsonOpcoes, cancellationToken);
        respostaHttp.EnsureSuccessStatusCode();

        var resposta = await respostaHttp.Content.ReadFromJsonAsync<RespostaTreinamentoMlApi>(JsonOpcoes, cancellationToken);
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

        using var respostaHttp = await _httpClient.PostAsJsonAsync("/api/authenticate", payload, JsonOpcoes, cancellationToken);
        respostaHttp.EnsureSuccessStatusCode();

        var resposta = await respostaHttp.Content.ReadFromJsonAsync<RespostaAutenticacaoMlApi>(JsonOpcoes, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia da API de ML na verificação biométrica.");

        var decisao = MapearDecisao(resposta.Decision);
        var confianca = resposta.Confidence ?? resposta.ConfidenceScore ?? 0d;
        var limiar = resposta.Threshold ?? resposta.ThresholdScore ?? _opcoes.LimiarConfianca;
        var mediaHoldPerfilMs = resposta.UserAverageHoldTimeMs ?? 0d;
        var deveGravarParaRetreino = resposta.ShouldRecordForRetraining ?? false;

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
