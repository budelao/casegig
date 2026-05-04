using CaseGig.Api.Configuration;
using CaseGig.Application;
using CaseGig.Infrastructure;
using System.Globalization;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var ptBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

var builder = WebApplication.CreateBuilder(args);
var recreateDb = args.Any(x => string.Equals(x, "--recreate-db", StringComparison.OrdinalIgnoreCase));
builder.AddLoggingConfiguration();
builder.AddObservabilityConfiguration();
builder.AddResilienceConfiguration();
builder.AddApiConfiguration();
builder.AddSwaggerConfiguration();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.InitializeDatabase(recreateDb);
app.UseApiPipeline();

app.Run();
