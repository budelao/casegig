using CaseGig.Api.Swagger;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace CaseGig.Api.Extensions;

internal static class SwaggerExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.MapType<DateOnly>(() => new OpenApiSchema
            {
                Type = "string",
                Pattern = "^\\d{2}/\\d{2}/\\d{4}$",
                Example = new OpenApiString("31/12/2026")
            });

            options.OperationFilter<IdempotencyHeadersOperationFilter>();
        });

        return services;
    }
}

