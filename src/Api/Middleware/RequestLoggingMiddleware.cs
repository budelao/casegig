namespace CaseGig.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private const string CorrelationHeaderName = "X-Correlation-Id";

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.Headers[CorrelationHeaderName] = correlationId;

        var scope = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = context.TraceIdentifier,
            ["Source"] = "API"
        };

        using (_logger.BeginScope(scope))
        {
            var startedAt = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation(
                "API: Request iniciada. Method={Method} Path={Path} QueryString={QueryString} ContentLength={ContentLength} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Request.ContentLength,
                correlationId);

            try
            {
                await _next(context);

                var elapsedMs = startedAt.ElapsedMilliseconds;
                var statusCode = context.Response.StatusCode;
                if (statusCode >= StatusCodes.Status500InternalServerError)
                {
                    _logger.LogError(
                        "API: Request finalizada com erro. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                        statusCode,
                        elapsedMs,
                        correlationId);
                }
                else if (statusCode >= StatusCodes.Status400BadRequest)
                {
                    _logger.LogWarning(
                        "API: Request finalizada com falha. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                        statusCode,
                        elapsedMs,
                        correlationId);
                }
                else
                {
                    _logger.LogInformation(
                        "API: Request finalizada. StatusCode={StatusCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                        statusCode,
                        elapsedMs,
                        correlationId);
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
}
