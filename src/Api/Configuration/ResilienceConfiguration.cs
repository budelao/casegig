using CaseGig.Api.BackgroundJobs;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using System.Net.Http.Headers;

namespace CaseGig.Api.Configuration;

internal static class ResilienceConfiguration
{
    public static WebApplicationBuilder AddResilienceConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient(ExportClients.Splunk, (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptionsMonitor<ObservabilityLoggingOptions>>().CurrentValue;
            client.Timeout = Timeout.InfiniteTimeSpan;
            if (!string.IsNullOrWhiteSpace(opt.Export.Splunk.HecEndpoint))
            {
                client.BaseAddress = new Uri(opt.Export.Splunk.HecEndpoint, UriKind.Absolute);
            }
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(opt.Export.Splunk.Token) && !opt.Export.Splunk.Token.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Splunk", opt.Export.Splunk.Token);
            }
        })
        .AddPolicyHandler(CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(CreateRetryPolicy())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        builder.Services.AddHttpClient(ExportClients.Loki, (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptionsMonitor<ObservabilityLoggingOptions>>().CurrentValue;
            client.Timeout = Timeout.InfiniteTimeSpan;
            if (!string.IsNullOrWhiteSpace(opt.Export.Grafana.LokiEndpoint))
            {
                client.BaseAddress = new Uri(opt.Export.Grafana.LokiEndpoint, UriKind.Absolute);
            }
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(opt.Export.Grafana.Token) && !opt.Export.Grafana.Token.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opt.Export.Grafana.Token);
            }
        })
        .AddPolicyHandler(CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(CreateRetryPolicy())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        builder.Services.AddHttpClient(ExportClients.Datadog, (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptionsMonitor<ObservabilityLoggingOptions>>().CurrentValue;
            var dd = opt.Export.Datadog;
            client.Timeout = Timeout.InfiniteTimeSpan;
            var site = string.IsNullOrWhiteSpace(dd.Site) ? "datadoghq.com" : dd.Site;
            client.BaseAddress = new Uri($"https://http-intake.logs.{site}/api/v2/logs", UriKind.Absolute);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Remove("DD-API-KEY");
            if (!string.IsNullOrWhiteSpace(dd.ApiKey) && !dd.ApiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Add("DD-API-KEY", dd.ApiKey);
            }
        })
        .AddPolicyHandler(CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(CreateRetryPolicy())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        return builder;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
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

    private static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == (HttpStatusCode)429)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(TimeSpan timeout)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(timeout);
    }
}
