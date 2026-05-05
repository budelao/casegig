using CaseGig.Infrastructure;

namespace CaseGig.Api.Configuration;

internal static class ObservabilityConfiguration
{
    public static WebApplicationBuilder AddObservabilityConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddObservabilityExporting(builder.Configuration, builder.Logging);

        return builder;
    }
}
