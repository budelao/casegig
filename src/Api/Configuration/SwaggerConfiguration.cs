using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CaseGig.Api.Configuration;

internal static class SwaggerConfiguration
{
    public static WebApplicationBuilder AddSwaggerConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.MapType<DateOnly>(() => new OpenApiSchema
            {
                Type = "string",
                Pattern = "^\\d{2}/\\d{2}/\\d{4}$",
                Example = new OpenApiString("31/12/2026")
            });

            options.OperationFilter<IdempotencyHeadersOperationFilter>();
        });

        return builder;
    }

    private sealed class IdempotencyHeadersOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
            if (!IsIdempotentEndpoint(relativePath))
            {
                return;
            }

            operation.Parameters ??= new List<OpenApiParameter>();

            var alreadyDeclared = operation.Parameters.Any(p =>
                string.Equals(p.In?.ToString(), "Header", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));

            if (!alreadyDeclared)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "Idempotency-Key",
                    In = ParameterLocation.Header,
                    Required = false,
                    Description = "Chave de idempotência. Repetir a mesma chave com o mesmo payload retorna a primeira resposta; com payload diferente retorna 409.",
                    Schema = new OpenApiSchema { Type = "string" }
                });
            }

            if (operation.Responses.TryGetValue("200", out var okResponse))
            {
                okResponse.Headers ??= new Dictionary<string, OpenApiHeader>(StringComparer.OrdinalIgnoreCase);
                if (!okResponse.Headers.ContainsKey("Idempotency-Replayed"))
                {
                    okResponse.Headers["Idempotency-Replayed"] = new OpenApiHeader
                    {
                        Description = "Indica replay de idempotência (true quando a ordem não foi criada novamente).",
                        Schema = new OpenApiSchema { Type = "string" }
                    };
                }
            }
        }

        private static bool IsIdempotentEndpoint(string relativePath)
        {
            var path = relativePath;
            var idx = path.IndexOf('?', StringComparison.Ordinal);
            if (idx >= 0)
            {
                path = path[..idx];
            }

            path = path.Trim('/').ToLowerInvariant();
            return path is "ordens" or "ordens/agendamento";
        }
    }
}
