using CaseGig.Api.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CaseGig.Api.Extensions;

internal static class LoggingExtensions
{
    public static WebApplicationBuilder ConfigureConsoleLogging(this WebApplicationBuilder builder)
    {
        var consoleFormatterName = builder.Configuration.GetValue<string>("Logging:Console:FormatterName");
        if (string.IsNullOrWhiteSpace(consoleFormatterName))
        {
            consoleFormatterName = builder.Environment.IsDevelopment()
                ? PrettyConsoleFormatter.FormatterName
                : ColoredJsonConsoleFormatter.FormatterName;
        }

        var forceColors = builder.Configuration.GetValue("Logging:Console:ForceColors", false);
        var useColors = builder.Configuration.GetValue<bool?>("Logging:Console:UseColors") ?? (forceColors || !Console.IsOutputRedirected);
        var prettyColorMode = builder.Configuration.GetValue<string>("Logging:Console:ColorMode");
        if (string.IsNullOrWhiteSpace(prettyColorMode))
        {
            prettyColorMode = "console";
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = consoleFormatterName;
        });

        builder.Logging.AddConsoleFormatter<ColoredJsonConsoleFormatter, ColoredJsonConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
            options.JsonWriterOptions = new JsonWriterOptions
            {
                Indented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.UseColors = useColors;
            options.WorkerColor = ConsoleColor.Yellow;
            options.ApiColor = ConsoleColor.Green;
        });

        builder.Logging.AddConsoleFormatter<PrettyConsoleFormatter, PrettyConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
            options.UseColors = useColors;
            options.WorkerColor = ConsoleColor.Yellow;
            options.ApiColor = ConsoleColor.Green;
            options.TimestampFormat = "HH:mm:ss.fff";
            options.ColorMode = prettyColorMode;
        });

        return builder;
    }
}

