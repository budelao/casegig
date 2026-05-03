using Polly;
using Polly.Extensions.Http;
using System.Net;

namespace CaseGig.Api.Resilience;

internal static class PollyPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == (HttpStatusCode)429)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                {
                    var baseDelayMs = 200 * Math.Pow(2, retryAttempt - 1);
                    var jitterMs = Random.Shared.Next(0, 200);
                    return TimeSpan.FromMilliseconds(baseDelayMs + jitterMs);
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == (HttpStatusCode)429)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }

    public static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(TimeSpan timeout)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeout);
    }
}

