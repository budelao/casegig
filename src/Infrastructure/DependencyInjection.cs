using CaseGig.Application.Abstractions;
using CaseGig.Infrastructure.Persistence;
using CaseGig.Infrastructure.Repositories;
using CaseGig.Infrastructure.Transactions;
using CaseGig.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Net;
using System.Net.Http.Headers;

namespace CaseGig.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MySql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:MySql não configurado.");
        }

        if (connectionString.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ConnectionStrings:MySql está com valor placeholder. Configure ConnectionStrings__MySql (variável de ambiente) ou appsettings.Development.json.");
        }

        services.AddDbContext<InvestmentDbContext>(options =>
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            options.UseMySql(connectionString, serverVersion);
        });

        services.AddScoped<ITransactionManager, EfTransactionManager>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IFundoRepository, FundoRepository>();
        services.AddScoped<IPosicaoRepository, PosicaoRepository>();
        services.AddScoped<IOrdemRepository, OrdemRepository>();

        return services;
    }

    public static IServiceCollection AddObservabilityExporting(this IServiceCollection services, IConfiguration configuration, ILoggingBuilder logging)
    {
        services.Configure<ObservabilityLoggingOptions>(configuration.GetSection("Observability:Logging"));
        services.AddSingleton<ObservabilityExportQueue>();
        logging.Services.AddSingleton<ILoggerProvider, ObservabilityExportLoggerProvider>();
        services.AddHostedService<ObservabilityExportWorker>();

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
        .AddPolicyHandler(CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(CreateRetryPolicy())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

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
        .AddPolicyHandler(CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(CreateRetryPolicy())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

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
        .AddPolicyHandler(CreateTimeoutPolicy(TimeSpan.FromSeconds(5)))
        .AddPolicyHandler(CreateRetryPolicy())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        return services;
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
