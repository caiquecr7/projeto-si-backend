using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Console;

namespace ProjetoSi.Api.Common.Observabilidade;

public static partial class ContextoCorrelacao
{
    public const string CabecalhoHttp = "X-Correlation-ID";
    private static readonly AsyncLocal<string?> CorrelacaoAtual = new();

    public static string? Atual => CorrelacaoAtual.Value;

    public static IDisposable Definir(string correlacaoId)
    {
        var anterior = CorrelacaoAtual.Value;
        CorrelacaoAtual.Value = correlacaoId;
        return new RestauradorCorrelacao(anterior);
    }

    private sealed class RestauradorCorrelacao(string? anterior) : IDisposable
    {
        public void Dispose() => CorrelacaoAtual.Value = anterior;
    }
}

public static partial class ConfiguracaoLogs
{
    [GeneratedRegex("^[A-Za-z0-9._:-]{1,64}$")]
    private static partial Regex PadraoCorrelacaoSegura();

    public static void AplicarConfiguracao(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "O";
            options.UseUtcTimestamp = true;
        });
    }

    public static void AplicarFiltroDebugLogs(ILoggingBuilder logging, IConfiguration configuration)
    {
        var valorBruto = configuration["DEBUG_LOGS"]?.Trim();
        if (string.IsNullOrEmpty(valorBruto))
        {
            return;
        }

        if (EhVerdadeiro(valorBruto))
        {
            logging.AddFilter("ProjetoSi.Api", LogLevel.Debug);
        }
        else if (EhFalso(valorBruto))
        {
            logging.AddFilter("ProjetoSi.Api", LogLevel.Information);
        }
    }

    public static string SanitizarCorrelacao(string? valorBruto)
    {
        var candidato = valorBruto?.Trim();
        return !string.IsNullOrEmpty(candidato) && PadraoCorrelacaoSegura().IsMatch(candidato)
            ? candidato
            : Guid.NewGuid().ToString("N");
    }

    public static string DeterminarFluxo(PathString caminho)
    {
        var valor = caminho.Value ?? string.Empty;
        if (valor.Contains("register", StringComparison.OrdinalIgnoreCase))
        {
            return "register";
        }

        if (valor.Contains("enrollment", StringComparison.OrdinalIgnoreCase))
        {
            return "enrollment";
        }

        if (valor.Contains("authenticate", StringComparison.OrdinalIgnoreCase))
        {
            return "login";
        }

        if (valor.Contains("health", StringComparison.OrdinalIgnoreCase))
        {
            return "health";
        }

        return "api";
    }

    private static bool EhVerdadeiro(string valor) =>
        valor.Equals("true", StringComparison.OrdinalIgnoreCase)
        || valor.Equals("1", StringComparison.Ordinal)
        || valor.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || valor.Equals("on", StringComparison.OrdinalIgnoreCase);

    private static bool EhFalso(string valor) =>
        valor.Equals("false", StringComparison.OrdinalIgnoreCase)
        || valor.Equals("0", StringComparison.Ordinal)
        || valor.Equals("no", StringComparison.OrdinalIgnoreCase)
        || valor.Equals("off", StringComparison.OrdinalIgnoreCase);
}

public sealed class MiddlewareCorrelacaoLogs(
    RequestDelegate next,
    ILogger<MiddlewareCorrelacaoLogs> logger,
    TimeProvider relogio)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlacao = ConfiguracaoLogs.SanitizarCorrelacao(
            context.Request.Headers[ContextoCorrelacao.CabecalhoHttp].FirstOrDefault());
        var fluxo = ConfiguracaoLogs.DeterminarFluxo(context.Request.Path);
        var inicio = relogio.GetTimestamp();

        context.Response.Headers[ContextoCorrelacao.CabecalhoHttp] = correlacao;

        using var _ = ContextoCorrelacao.Definir(correlacao);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlacao,
            ["Flow"] = fluxo
        });

        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} metodo={Metodo} caminho={Caminho}",
            "backend.http.request_iniciada",
            fluxo,
            context.Request.Method,
            context.Request.Path.Value);

        await next(context);

        var duracaoMs = Math.Round(relogio.GetElapsedTime(inicio).TotalMilliseconds, 2);
        logger.LogDebug(
            "evento={Evento} fluxo={Fluxo} metodo={Metodo} caminho={Caminho} statusCode={StatusCode} duracaoMs={DuracaoMs}",
            "backend.http.request_concluida",
            fluxo,
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            duracaoMs);
    }
}

public sealed class HandlerCorrelacaoMlApi : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlacao = ContextoCorrelacao.Atual;
        if (!string.IsNullOrEmpty(correlacao) && !request.Headers.Contains(ContextoCorrelacao.CabecalhoHttp))
        {
            request.Headers.TryAddWithoutValidation(ContextoCorrelacao.CabecalhoHttp, correlacao);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
