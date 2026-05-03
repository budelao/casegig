using CaseGig.Api.Extensions;
using CaseGig.Api.Startup;
using CaseGig.Api.Workers;
using CaseGig.Application;
using CaseGig.Domain.Services;
using CaseGig.Infrastructure;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);
var recreateDb = args.Any(x => string.Equals(x, "--recreate-db", StringComparison.OrdinalIgnoreCase));
builder.ConfigureConsoleLogging();

builder.AddObservability();
builder.Services.AddApiControllers();
builder.Services.AddApiSwagger();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<OrdemService>();
builder.Services.AddScoped<OrdemProcessamentoService>();
builder.Services.AddHostedService<OrdemAgendadaWorker>();

var app = builder.Build();

DatabaseInitializer.Initialize(app, recreateDb);
app.UseApiPipeline();

app.Run();
