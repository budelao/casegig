using CaseGig.Application.Idempotency;
using CaseGig.Application.Operations;
using CaseGig.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CaseGig.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IdempotencyService>();
        services.AddScoped<OrdemOperationHandlerFactory>();
        services.AddScoped<IOrdemOperationHandler, AporteOrdemOperationHandler>();
        services.AddScoped<IOrdemOperationHandler, ResgateOrdemOperationHandler>();
        services.AddScoped<CriarOrdemUseCase>();
        services.AddScoped<CriarOrdemAgendadaUseCase>();
        services.AddScoped<ConsultarOrdensUseCase>();
        services.AddScoped<ConsultarPosicoesUseCase>();
        services.AddScoped<ProcessarOrdensAgendadasUseCase>();

        return services;
    }
}
