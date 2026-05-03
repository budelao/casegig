using CaseGig.Api.Middleware;
using CaseGig.Api.Observability;
using Microsoft.Extensions.Logging;

namespace CaseGig.Api.Extensions;

internal static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ObservabilityLoggingOptions>(builder.Configuration.GetSection("Observability:Logging"));

        builder.Services.AddSingleton<ObservabilityExportQueue>();
        builder.Logging.Services.AddSingleton<ILoggerProvider, ObservabilityExportLoggerProvider>();
        builder.Services.AddHostedService<ObservabilityExportWorker>();

        builder.Services.AddObservabilityHttpClients();

        return builder;
    }
}

