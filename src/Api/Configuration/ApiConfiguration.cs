using CaseGig.Api.Common.Json;
using CaseGig.Api.Middlewares;
using CaseGig.Api.Models.Responses;
using CaseGig.Infrastructure.Persistence;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json.Serialization;

namespace CaseGig.Api.Configuration;

internal static class ApiConfiguration
{
    public static WebApplicationBuilder AddApiConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new DateOnlyDdMmYyyyJsonConverter());
            });

        builder.Services.Configure<ApiBehaviorOptions>(options =>
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

        return builder;
    }

    public static WebApplication InitializeDatabase(this WebApplication app, bool recreateDb)
    {
        DatabaseInitializer.Initialize(app.Services, recreateDb, app.Environment.IsDevelopment());
        return app;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        var ptBr = new CultureInfo("pt-BR");
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(ptBr),
            SupportedCultures = new List<CultureInfo> { ptBr },
            SupportedUICultures = new List<CultureInfo> { ptBr }
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
