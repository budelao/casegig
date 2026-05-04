using CaseGig.Api.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace CaseGig.Api.Configuration;

internal static class ObservabilityConfiguration
{
    public static WebApplicationBuilder AddObservabilityConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ObservabilityLoggingOptions>(builder.Configuration.GetSection("Observability:Logging"));

        builder.Services.AddSingleton<ObservabilityExportQueue>();
        builder.Logging.Services.AddSingleton<ILoggerProvider, ObservabilityExportLoggerProvider>();
        builder.Services.AddHostedService<ObservabilityExportWorker>();

        return builder;
    }
}
