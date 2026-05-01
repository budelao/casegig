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
                "API: HTTP request started. Method={Method} Path={Path} QueryString={QueryString} ContentLength={ContentLength}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Request.ContentLength);

            try
            {
                await _next(context);

                _logger.LogInformation(
                    "API: HTTP request finished. StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                    context.Response.StatusCode,
                    startedAt.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "API: HTTP request failed. ElapsedMs={ElapsedMs}",
                    startedAt.ElapsedMilliseconds);
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
