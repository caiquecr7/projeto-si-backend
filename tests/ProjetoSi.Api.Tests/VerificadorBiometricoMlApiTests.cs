using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Tests;

public class VerificadorBiometricoMlApiTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Treinar_modelo_inicial_envia_vetores_para_ml_api_e_retorna_id_do_modelo()
    {
        HttpRequestMessage? requisicaoCapturada = null;
        string? corpoCapturado = null;

        var handler = new FakeHttpMessageHandler(async (request, ct) =>
        {
            requisicaoCapturada = request;
            corpoCapturado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"modelId": 42, "modelVersion": 1, "success": true}""", Encoding.UTF8, "application/json")
            };
        });

        var verificador = CriarVerificador(handler);
        var usuario = Usuario.Criar("ana", "hash", Agora);
        var vetor = CriarVetor();

        var modeloId = await verificador.TreinarModeloInicialAsync(usuario, [vetor], CancellationToken.None);

        Assert.Equal(42L, modeloId);
        Assert.NotNull(requisicaoCapturada);
        Assert.Equal(HttpMethod.Post, requisicaoCapturada!.Method);
        Assert.Equal("http://localhost:8000/api/enrollment", requisicaoCapturada.RequestUri!.ToString());

        using var json = JsonDocument.Parse(corpoCapturado!);
        Assert.Equal("ana", json.RootElement.GetProperty("username").GetString());
        Assert.Equal(usuario.Id, json.RootElement.GetProperty("userId").GetGuid());
        Assert.False(json.RootElement.GetProperty("recordSamples").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("vectors").GetArrayLength());
    }

    [Fact]
    public async Task Verificar_mapeia_resposta_genuina_da_ml_api()
    {
        HttpRequestMessage? requisicaoCapturada = null;
        string? corpoCapturado = null;

        var handler = new FakeHttpMessageHandler(async (request, ct) =>
        {
            requisicaoCapturada = request;
            corpoCapturado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"decision": "GENUINE", "confidence": 0.912, "threshold": 0.65, "userAverageHoldTimeMs": 94.6, "modelId": 7, "shouldRecordForRetraining": true}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var verificador = CriarVerificador(handler);
        var usuario = Usuario.Criar("ana", "hash", Agora);
        var tentativa = CriarVetor();

        var resultado = await verificador.VerificarAsync(usuario, tentativa, CancellationToken.None);

        Assert.Equal(DecisaoBiometrica.Genuine, resultado.Decisao);
        Assert.Equal(0.912, resultado.Confianca, 6);
        Assert.Equal(0.65, resultado.Limiar, 6);
        Assert.Equal(94.6, resultado.MediaHoldPerfilMs, 6);
        Assert.Equal(7L, resultado.ModeloId);
        Assert.True(resultado.DeveGravarParaRetreino);

        Assert.NotNull(requisicaoCapturada);
        Assert.Equal("http://localhost:8000/api/authenticate", requisicaoCapturada!.RequestUri!.ToString());

        using var json = JsonDocument.Parse(corpoCapturado!);
        Assert.Equal("ana", json.RootElement.GetProperty("username").GetString());
        Assert.Equal(usuario.Id, json.RootElement.GetProperty("userId").GetGuid());
        Assert.False(json.RootElement.GetProperty("recordSample").GetBoolean());
        Assert.Equal(tentativa.Valores.Length, json.RootElement.GetProperty("vector").GetArrayLength());
    }

    [Fact]
    public async Task Verificar_mapeia_resposta_impostora_da_ml_api()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"decision": "IMPOSTOR", "confidenceScore": 0.18, "thresholdScore": 0.65, "userAverageHoldTimeMs": 90.0, "modelId": 3}""",
                Encoding.UTF8,
                "application/json")
        }));

        var verificador = CriarVerificador(handler);
        var usuario = Usuario.Criar("ana", "hash", Agora);

        var resultado = await verificador.VerificarAsync(usuario, CriarVetor(), CancellationToken.None);

        Assert.Equal(DecisaoBiometrica.Impostor, resultado.Decisao);
        Assert.Equal(0.18, resultado.Confianca, 6);
        Assert.Equal(0.65, resultado.Limiar, 6);
        Assert.Equal(90.0, resultado.MediaHoldPerfilMs, 6);
        Assert.Equal(3L, resultado.ModeloId);
        Assert.False(resultado.DeveGravarParaRetreino);
    }

    [Fact]
    public async Task Verificar_nao_marca_gravacao_quando_ml_api_indica_false_para_genuino_incerto()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"decision": "GENUINE", "confidence": 0.72, "threshold": 0.70, "userAverageHoldTimeMs": 95.0, "modelId": 5, "shouldRecordForRetraining": false}""",
                Encoding.UTF8,
                "application/json")
        }));

        var verificador = CriarVerificador(handler);
        var usuario = Usuario.Criar("ana", "hash", Agora);

        var resultado = await verificador.VerificarAsync(usuario, CriarVetor(), CancellationToken.None);

        Assert.Equal(DecisaoBiometrica.Genuine, resultado.Decisao);
        Assert.False(resultado.DeveGravarParaRetreino);
    }

    private static VerificadorBiometricoMlApi CriarVerificador(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var opcoes = Options.Create(new BiometriaOptions
        {
            MlApiUrl = "http://localhost:8000",
            LimiarConfianca = 0.35
        });

        return new VerificadorBiometricoMlApi(httpClient, opcoes);
    }

    private static VetorCaracteristicas CriarVetor()
    {
        var features = new Dictionary<string, double>
        {
            ["H.a.0"] = 0.1,
            ["DD.a.b.0"] = 0.25,
            ["UD.a.b.0"] = 0.15,
            ["H.b.0"] = 0.08
        };

        Assert.True(VetorCaracteristicas.TentarExtrair(features, "ab", out var vetor));
        return vetor!;
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
