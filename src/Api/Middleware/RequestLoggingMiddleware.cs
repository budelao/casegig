using Microsoft.Extensions.Options;

namespace CaseGig.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IOptionsMonitor<ObservabilityLoggingOptions> _options;
    private const string CorrelationHeaderName = "X-Correlation-Id";

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptionsMonitor<ObservabilityLoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task Invoke(HttpContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        var correlationId = GetOrCreateCorrelationId(context);
        if (options.AddCorrelationIdHeader)
        {
            context.Response.Headers[CorrelationHeaderName] = correlationId;
        }

        var scope = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = context.TraceIdentifier,
            ["Source"] = "API",
            ["Service"] = options.ServiceName
        };

        using (_logger.BeginScope(scope))
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();

            var requestHeaders = options.LogRequestHeaders ? FilterHeaders(context.Request.Headers, options.RequestHeaderAllowList) : null;
            _logger.LogInformation(
                "API: Request iniciada. Method={Method} Path={Path} QueryString={QueryString} ContentLength={ContentLength} CorrelationId={CorrelationId} Headers={Headers}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Request.ContentLength,
                correlationId,
                requestHeaders);

            try
            {
                await _next(context);

                var elapsedMs = startedAt.ElapsedMilliseconds;
                var statusCode = context.Response.StatusCode;
                var responseHeaders = options.LogResponseHeaders ? FilterHeaders(context.Response.Headers, options.ResponseHeaderAllowList) : null;
                if (statusCode >= StatusCodes.Status500InternalServerError)
                {
                    _logger.LogError(
                        "API: Request finalizada com erro. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} Headers={Headers}",
                        statusCode,
                        elapsedMs,
                        correlationId,
                        responseHeaders);
                }
                else if (statusCode >= StatusCodes.Status400BadRequest)
                {
                    _logger.LogWarning(
                        "API: Request finalizada com falha. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} Headers={Headers}",
                        statusCode,
                        elapsedMs,
                        correlationId,
                        responseHeaders);
                }
                else
                {
                    _logger.LogInformation(
                        "API: Request finalizada. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} Headers={Headers}",
                        statusCode,
                        elapsedMs,
                        correlationId,
                        responseHeaders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "API: Request falhou por exceção não tratada. ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                    startedAt.ElapsedMilliseconds,
                    correlationId);
                throw;
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming.ToString()))
        {
            return incoming.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }

    private static Dictionary<string, string[]>? FilterHeaders(IHeaderDictionary headers, string[] allowList)
    {
        if (allowList.Length == 0)
        {
            return null;
        }

        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in allowList)
        {
            if (headers.TryGetValue(name, out var values))
            {
                result[name] = values.Where(x => x is not null).Select(x => x!).ToArray();
            }
        }

        return result.Count == 0 ? null : result;
    }
}

public sealed class ObservabilityLoggingOptions
{
    public bool Enabled { get; set; } = true;
    public bool AddCorrelationIdHeader { get; set; } = true;
    public string ServiceName { get; set; } = "CaseGig.Api";

    public bool LogRequestHeaders { get; set; } = false;
    public string[] RequestHeaderAllowList { get; set; } = Array.Empty<string>();

    public bool LogResponseHeaders { get; set; } = false;
    public string[] ResponseHeaderAllowList { get; set; } = Array.Empty<string>();

    public ExportTargets Export { get; set; } = new();

    public sealed class ExportTargets
    {
        public SplunkOptions Splunk { get; set; } = new();
        public GrafanaOptions Grafana { get; set; } = new();
        public DatadogOptions Datadog { get; set; } = new();
    }

    public sealed class SplunkOptions
    {
        public bool Enabled { get; set; } = false;
        public string? HecEndpoint { get; set; }
        public string Token { get; set; } = "CHANGE_ME";
        public string? Index { get; set; }
        public string? SourceType { get; set; }
    }

    public sealed class GrafanaOptions
    {
        public bool Enabled { get; set; } = false;
        public string? LokiEndpoint { get; set; }
        public string Token { get; set; } = "CHANGE_ME";
    }

    public sealed class DatadogOptions
    {
        public bool Enabled { get; set; } = false;
        public string Site { get; set; } = "datadoghq.com";
        public string ApiKey { get; set; } = "CHANGE_ME";
        public string? Service { get; set; }
        public string? Environment { get; set; }
    }
}
