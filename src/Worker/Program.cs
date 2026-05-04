using CaseGig.Application;
using CaseGig.Domain.Services;
using CaseGig.Infrastructure;
using CaseGig.Worker;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<OrdemService>();
builder.Services.AddScoped<OrdemProcessamentoService>();

builder.Services.AddHostedService<OrdemAgendadaWorker>();

var host = builder.Build();
host.Run();
