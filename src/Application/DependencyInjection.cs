using CaseGig.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CaseGig.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CriarOrdemUseCase>();
        services.AddScoped<ConsultarOrdensUseCase>();
        services.AddScoped<ConsultarPosicoesUseCase>();
        services.AddScoped<ProcessarOrdensAgendadasUseCase>();

        return services;
    }
}
