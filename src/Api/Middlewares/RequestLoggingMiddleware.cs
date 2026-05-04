using Microsoft.Extensions.Options;
using CaseGig.Api.Configuration;

namespace CaseGig.Api.Middlewares;

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
                "Request iniciada. Method={Method} Path={Path} QueryString={QueryString} ContentLength={ContentLength} CorrelationId={CorrelationId} Headers={Headers}",
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
                        "Request finalizada com erro. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} Headers={Headers}",
                        statusCode,
                        elapsedMs,
                        correlationId,
                        responseHeaders);
                }
                else if (statusCode >= StatusCodes.Status400BadRequest)
                {
                    _logger.LogWarning(
                        "Request finalizada com falha. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} Headers={Headers}",
                        statusCode,
                        elapsedMs,
                        correlationId,
                        responseHeaders);
                }
                else
                {
                    _logger.LogInformation(
                        "Request finalizada. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} Headers={Headers}",
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
                    "Request falhou por exceção não tratada. ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
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
