using CaseGig.Api.Contracts;
using CaseGig.Api.Middleware;
using CaseGig.Api.Workers;
using CaseGig.Application;
using CaseGig.Domain.Services;
using CaseGig.Infrastructure;
using CaseGig.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = ColoredJsonConsoleFormatter.FormatterName;
});

builder.Logging.AddConsoleFormatter<ColoredJsonConsoleFormatter, ColoredJsonConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new JsonWriterOptions
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    options.UseColors = !Console.IsOutputRedirected;
    options.WorkerColor = ConsoleColor.Yellow;
    options.ApiColor = ConsoleColor.Green;
});

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<DateOnly>(() => new OpenApiSchema
    {
        Type = "string",
        Pattern = "^\\d{2}/\\d{2}/\\d{4}$",
        Example = new OpenApiString("31/12/2026")
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<OrdemService>();
builder.Services.AddScoped<OrdemProcessamentoService>();
builder.Services.AddHostedService<OrdemAgendadaWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migration");
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestmentDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Falha ao aplicar migrations");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

sealed class ColoredJsonConsoleFormatterOptions : ConsoleFormatterOptions
{
    public ConsoleColor WorkerColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleColor ApiColor { get; set; } = ConsoleColor.Green;
    public bool UseColors { get; set; } = !Console.IsOutputRedirected;
    public JsonWriterOptions JsonWriterOptions { get; set; } = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}

sealed class ColoredJsonConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "coloredJson";
    private static readonly object ConsoleLock = new();
    private readonly ColoredJsonConsoleFormatterOptions _options;

    public ColoredJsonConsoleFormatter(IOptionsMonitor<ColoredJsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        var prefix = GetPrefix(logEntry.Category);
        if (!string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(message))
        {
            message = $"{prefix} {message}";
        }

        Dictionary<string, object?>? state = null;
        if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> stateValues)
        {
            state = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in stateValues)
            {
                state[kvp.Key] = kvp.Value;
            }
        }

        List<object?>? scopes = null;
        if (_options.IncludeScopes && scopeProvider is not null)
        {
            scopes = new List<object?>();
            scopeProvider.ForEachScope(
                static (scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                    {
                        var scopeDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var kvp in scopeValues)
                        {
                            scopeDict[kvp.Key] = kvp.Value;
                        }
                        list.Add(scopeDict);
                        return;
                    }

                    list.Add(scope?.ToString());
                },
                scopes);
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["EventId"] = logEntry.EventId.Id,
            ["LogLevel"] = logEntry.LogLevel.ToString(),
            ["Category"] = logEntry.Category,
            ["Source"] = GetSource(logEntry.Category),
            ["Message"] = message,
            ["State"] = state,
            ["Scopes"] = scopes
        };

        if (logEntry.Exception is not null)
        {
            payload["Exception"] = logEntry.Exception.ToString();
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = _options.JsonWriterOptions.Encoder ?? JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        if (!_options.UseColors)
        {
            textWriter.WriteLine(json);
            return;
        }

        var color = GetColor(logEntry.Category);

        lock (ConsoleLock)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                textWriter.WriteLine(json);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }

    private ConsoleColor GetColor(string category)
    {
        if (category.Contains(".Workers.", StringComparison.Ordinal) || category.Contains(".Workers", StringComparison.Ordinal))
        {
            return _options.WorkerColor;
        }

        return _options.ApiColor;
    }

    private static string? GetPrefix(string category)
    {
        if (category.Contains(".Workers.", StringComparison.Ordinal) || category.Contains(".Workers", StringComparison.Ordinal))
        {
            return "WORKER:";
        }

        return "API:";
    }

    private static string GetSource(string category)
    {
        if (category.Contains(".Workers.", StringComparison.Ordinal) || category.Contains(".Workers", StringComparison.Ordinal))
        {
            return "WORKER";
        }

        return "API";
    }
}

sealed class DateOnlyDdMmYyyyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "dd/MM/yyyy";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Data inválida. Formato esperado: {Format}");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"Data inválida. Formato esperado: {Format}");
        }

        if (!DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new JsonException($"Data inválida. Formato esperado: {Format}");
        }

        return date;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
