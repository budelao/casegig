namespace CaseGig.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        _logger.LogInformation(
            "HTTP {Method} {Path} {QueryString} ContentLength={ContentLength}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Request.QueryString.Value,
            context.Request.ContentLength);

        await _next(context);
    }
}
