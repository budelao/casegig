using CaseGig.Application;
using CaseGig.Infrastructure;
using CaseGig.Worker;
using CaseGig.Worker.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

var forceColors = builder.Configuration.GetValue("Logging:Console:ForceColors", false);
var useColors = builder.Configuration.GetValue<bool?>("Logging:Console:UseColors") ?? (forceColors || !Console.IsOutputRedirected);
var colorMode = builder.Configuration.GetValue<string>("Logging:Console:ColorMode");
if (string.IsNullOrWhiteSpace(colorMode))
{
    colorMode = "console";
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = PrettyConsoleFormatter.FormatterName;
});
builder.Logging.AddConsoleFormatter<PrettyConsoleFormatter, PrettyConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.UseColors = useColors;
    options.BaseColor = ConsoleColor.Yellow;
    options.TimestampFormat = "HH:mm:ss.fff";
    options.ColorMode = colorMode;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OrdemAgendadaWorker>();

var host = builder.Build();
host.Run();
