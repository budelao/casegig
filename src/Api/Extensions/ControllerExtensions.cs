using CaseGig.Api.Contracts;
using CaseGig.Api.Json;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace CaseGig.Api.Extensions;

internal static class ControllerExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new DateOnlyDdMmYyyyJsonConverter());
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .SelectMany(x => x.Value?.Errors.Select(e => e.ErrorMessage) ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                return new BadRequestObjectResult(ApiResponse<object>.Fail(errors));
            };
        });

        return services;
    }
}

