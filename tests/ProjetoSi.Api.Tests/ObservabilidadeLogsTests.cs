using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjetoSi.Api.Common.Biometria;
using ProjetoSi.Api.Common.Observabilidade;
using ProjetoSi.Api.Domain;

namespace ProjetoSi.Api.Tests;

public class ObservabilidadeLogsTests
{
    [Fact]
    public void Filtro_debug_desativa_logs_em_producao_e_permite_override_por_env()
    {
        using var prodSemOverride = CriarLoggerFactory(
            logLevelProjetoSi: "Information",
            debugLogsEnv: null);
        Assert.False(prodSemOverride.CreateLogger("ProjetoSi.Api.Features.Login.Autenticar+Endpoint").IsEnabled(LogLevel.Debug));
        Assert.True(prodSemOverride.CreateLogger("ProjetoSi.Api.Features.Login.Autenticar+Endpoint").IsEnabled(LogLevel.Information));

        using var prodComDebug = CriarLoggerFactory(
            logLevelProjetoSi: "Information",
            debugLogsEnv: "true");
        Assert.True(prodComDebug.CreateLogger("ProjetoSi.Api.Features.Login.Autenticar+Endpoint").IsEnabled(LogLevel.Debug));

        using var devComDisable = CriarLoggerFactory(
            logLevelProjetoSi: "Debug",
            debugLogsEnv: "false");
        Assert.False(devComDisable.CreateLogger("ProjetoSi.Api.Features.Cadastro.CadastrarUsuario+Endpoint").IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public async Task Middleware_e_handler_propagam_correlation_id_e_emitem_logs_debug()
    {
        var loggerCapturado = new CapturingLogger<MiddlewareCorrelacaoLogs>();
        string? headerRecebidoNaMlApi = null;

        var handlerMl = new HandlerCorrelacaoMlApi
        {
            InnerHandler = new FakeHttpMessageHandler((request, _) =>
            {
                headerRecebidoNaMlApi = request.Headers.TryGetValues(ContextoCorrelacao.CabecalhoHttp, out var valores)
                    ? valores.FirstOrDefault()
                    : null;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            })
        };

        using var httpClient = new HttpClient(handlerMl);

        var middleware = new MiddlewareCorrelacaoLogs(
            async ctx =>
            {
                Assert.Equal("trace-e2e-99", ContextoCorrelacao.Atual);
                using var req = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8000/api/authenticate");
                await httpClient.SendAsync(req, ctx.RequestAborted);
                ctx.Response.StatusCode = StatusCodes.Status200OK;
            },
            loggerCapturado,
            TimeProvider.System);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/authenticate";
        httpContext.Request.Headers[ContextoCorrelacao.CabecalhoHttp] = "trace-e2e-99";

        await middleware.InvokeAsync(httpContext);

        Assert.Null(ContextoCorrelacao.Atual);
        Assert.Equal("trace-e2e-99", httpContext.Response.Headers[ContextoCorrelacao.CabecalhoHttp].ToString());
        Assert.Equal("trace-e2e-99", headerRecebidoNaMlApi);
        Assert.Equal(2, loggerCapturado.Entradas.Count(e => e.Nivel == LogLevel.Debug));
        Assert.Contains(loggerCapturado.Entradas, e => e.Mensagem.Contains("backend.http.request_iniciada"));
        Assert.Contains(loggerCapturado.Entradas, e => e.Mensagem.Contains("backend.http.request_concluida"));
    }

    [Fact]
    public void Sanitizacao_gera_novo_id_quando_cabecalho_e_invalido()
    {
        var sanitizado = ConfiguracaoLogs.SanitizarCorrelacao("id invalido com espaco\ne\nquebra");
        Assert.Equal(32, sanitizado.Length);
        Assert.All(sanitizado, ch => Assert.True(char.IsAsciiHexDigitLower(ch)));
    }

    [Fact]
    public async Task Verificador_biometrico_registra_eventos_estruturados_sem_vazamento()
    {
        var logger = new CapturingLogger<VerificadorBiometricoMlApi>();
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"decision": "GENUINE", "confidence": 0.89, "threshold": 0.70, "userAverageHoldTimeMs": 100.2, "modelId": 12, "shouldRecordForRetraining": true}""",
                Encoding.UTF8,
                "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var verificador = new VerificadorBiometricoMlApi(
            httpClient,
            Options.Create(new BiometriaOptions { MlApiUrl = "http://localhost:8000", LimiarConfianca = 0.7 }),
            logger);

        var usuario = Usuario.Criar("bruno", "hash-secreto", DateTimeOffset.UtcNow);
        Assert.True(VetorCaracteristicas.TentarExtrair(
            new Dictionary<string, double>
            {
                ["H.a.0"] = 0.1,
                ["DD.a.b.0"] = 0.25,
                ["UD.a.b.0"] = 0.15,
                ["H.b.0"] = 0.08
            },
            "ab",
            out var vetor));

        await verificador.VerificarAsync(usuario, vetor!, CancellationToken.None);

        Assert.Contains(logger.Entradas, e => e.Nivel == LogLevel.Debug && e.Mensagem.Contains("backend.ml_api.authenticate_enviando"));
        Assert.Contains(logger.Entradas, e => e.Nivel == LogLevel.Debug && e.Mensagem.Contains("backend.ml_api.authenticate_resposta"));
        Assert.DoesNotContain(logger.Entradas, e => e.Mensagem.Contains("hash-secreto", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entradas, e => e.Mensagem.Contains("H.a.0", StringComparison.Ordinal));
    }

    private static ILoggerFactory CriarLoggerFactory(string logLevelProjetoSi, string? debugLogsEnv)
    {
        var dados = new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:ProjetoSi.Api"] = logLevelProjetoSi,
        };
        if (debugLogsEnv is not null)
        {
            dados["DEBUG_LOGS"] = debugLogsEnv;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            ConfiguracaoLogs.AplicarConfiguracao(logging);
            logging.AddConfiguration(config.GetSection("Logging"));
            ConfiguracaoLogs.AplicarFiltroDebugLogs(logging, config);
        });
        return services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Nivel, string Mensagem)> Entradas { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instancia;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entradas.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instancia = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
