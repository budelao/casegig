using CaseGig.Api.Middleware;
using CaseGig.Api.Observability;
using CaseGig.Api.Resilience;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace CaseGig.Api.Extensions;

internal static class HttpClientExtensions
{
    public static IServiceCollection AddObservabilityHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient(ExportClients.Splunk, (sp, client) =>
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
        .AddPolicyHandler(PollyPolicies.CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(PollyPolicies.CreateRetryPolicy())
        .AddPolicyHandler(PollyPolicies.CreateCircuitBreakerPolicy());

        services.AddHttpClient(ExportClients.Loki, (sp, client) =>
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
        .AddPolicyHandler(PollyPolicies.CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(PollyPolicies.CreateRetryPolicy())
        .AddPolicyHandler(PollyPolicies.CreateCircuitBreakerPolicy());

        services.AddHttpClient(ExportClients.Datadog, (sp, client) =>
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
        .AddPolicyHandler(PollyPolicies.CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(PollyPolicies.CreateRetryPolicy())
        .AddPolicyHandler(PollyPolicies.CreateCircuitBreakerPolicy());

        return services;
    }
}

