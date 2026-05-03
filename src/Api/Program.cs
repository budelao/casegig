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
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Globalization;
using System.Data.Common;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);
var recreateDb = args.Any(x => string.Equals(x, "--recreate-db", StringComparison.OrdinalIgnoreCase));
var consoleFormatterName = builder.Configuration.GetValue<string>("Logging:Console:FormatterName");
if (string.IsNullOrWhiteSpace(consoleFormatterName))
{
    consoleFormatterName = builder.Environment.IsDevelopment()
        ? PrettyConsoleFormatter.FormatterName
        : ColoredJsonConsoleFormatter.FormatterName;
}
var forceColors = builder.Configuration.GetValue("Logging:Console:ForceColors", false);
var useColors = builder.Configuration.GetValue<bool?>("Logging:Console:UseColors") ?? (forceColors || !Console.IsOutputRedirected);
var prettyColorMode = builder.Configuration.GetValue<string>("Logging:Console:ColorMode");
if (string.IsNullOrWhiteSpace(prettyColorMode))
{
    prettyColorMode = "console";
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = consoleFormatterName;
});

builder.Logging.AddConsoleFormatter<ColoredJsonConsoleFormatter, ColoredJsonConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new JsonWriterOptions
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    options.UseColors = useColors;
    options.WorkerColor = ConsoleColor.Yellow;
    options.ApiColor = ConsoleColor.Green;
});

builder.Logging.AddConsoleFormatter<PrettyConsoleFormatter, PrettyConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.UseColors = useColors;
    options.WorkerColor = ConsoleColor.Yellow;
    options.ApiColor = ConsoleColor.Green;
    options.TimestampFormat = "HH:mm:ss.fff";
    options.ColorMode = prettyColorMode;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new DateOnlyDdMmYyyyJsonConverter());
    });

builder.Services.Configure<ObservabilityLoggingOptions>(builder.Configuration.GetSection("Observability:Logging"));

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

    options.OperationFilter<IdempotencyHeadersOperationFilter>();
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
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    var dbContext = scope.ServiceProvider.GetRequiredService<InvestmentDbContext>();

    if (recreateDb && app.Environment.IsDevelopment())
    {
        logger.LogWarning("Recriando banco de dados por solicitação (--recreate-db)");
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
    }
    else if (app.Environment.IsDevelopment() && HasMigrationsHistoryTable(dbContext))
    {
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Falha ao aplicar migrations no startup");
            throw;
        }
    }

    EnsureRequiredSchema(dbContext);
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void EnsureRequiredSchema(InvestmentDbContext dbContext)
{
    var providerName = dbContext.Database.ProviderName ?? string.Empty;
    if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
    {
        EnsureMySqlColumnExists(dbContext, "Ordens", "IdempotencyKey");
        EnsureMySqlColumnExists(dbContext, "Ordens", "IdempotencyOperation");
        EnsureMySqlColumnExists(dbContext, "Ordens", "IdempotencyRequestHash");
        return;
    }
}

static bool HasMigrationsHistoryTable(InvestmentDbContext dbContext)
{
    var providerName = dbContext.Database.ProviderName ?? string.Empty;
    if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
    {
        return MySqlTableExists(dbContext, "__EFMigrationsHistory");
    }

    return true;
}

static bool MySqlTableExists(InvestmentDbContext dbContext, string tableName)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    try
    {
        if (shouldClose)
        {
            connection.Open();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName";
        AddParameter(cmd, "@tableName", tableName);
        var count = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return count > 0;
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

static void EnsureMySqlColumnExists(InvestmentDbContext dbContext, string tableName, string columnName)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    try
    {
        if (shouldClose)
        {
            connection.Open();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";
        AddParameter(cmd, "@tableName", tableName);
        AddParameter(cmd, "@columnName", columnName);
        var count = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (count > 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Banco de dados desatualizado: coluna '{columnName}' não encontrada em '{tableName}'. " +
            $"Atualize o schema (aplicando migrations em um banco controlado por EF) ou recrie o banco usando o script scripts/provision-db.ps1.");
    }
    finally
    {
        if (shouldClose)
        {
            connection.Close();
        }
    }
}

static void AddParameter(DbCommand command, string name, object? value)
{
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value ?? DBNull.Value;
    command.Parameters.Add(parameter);
}

sealed class IdempotencyHeadersOperationFilter : IOperationFilter
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

sealed class PrettyConsoleFormatterOptions : ConsoleFormatterOptions
{
    public ConsoleColor WorkerColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleColor ApiColor { get; set; } = ConsoleColor.Green;
    public bool UseColors { get; set; } = !Console.IsOutputRedirected;
    public string ColorMode { get; set; } = "console";
}

sealed class PrettyConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "pretty";
    private static readonly object ConsoleLock = new();
    private readonly PrettyConsoleFormatterOptions _options;

    public PrettyConsoleFormatter(IOptionsMonitor<PrettyConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message) && logEntry.Exception is null)
        {
            return;
        }

        string? sourceFromScope = null;
        if (_options.IncludeScopes && scopeProvider is not null)
        {
            scopeProvider.ForEachScope(
                (scope, _) =>
                {
                    if (sourceFromScope is not null)
                    {
                        return;
                    }

                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                    {
                        foreach (var kvp in scopeValues)
                        {
                            if (string.Equals(kvp.Key, "Source", StringComparison.OrdinalIgnoreCase) && kvp.Value is string s && !string.IsNullOrWhiteSpace(s))
                            {
                                sourceFromScope = s;
                                return;
                            }
                        }
                    }
                },
                state: (object?)null);
        }

        var source = NormalizeSource(sourceFromScope) ?? GetSourceFromCategory(logEntry.Category);
        var now = _options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        var timestampFormat = string.IsNullOrWhiteSpace(_options.TimestampFormat) ? "HH:mm:ss.fff" : _options.TimestampFormat;
        var timestamp = now.ToString(timestampFormat, CultureInfo.InvariantCulture);
        var level = GetLevelToken(logEntry.LogLevel);

        var line = $"{timestamp} {level} {source} {message}";
        var exceptionText = logEntry.Exception?.ToString();

        if (!_options.UseColors)
        {
            textWriter.WriteLine(line);
            if (!string.IsNullOrWhiteSpace(exceptionText))
            {
                textWriter.WriteLine(exceptionText);
            }
            return;
        }

        if (string.Equals(_options.ColorMode, "ansi", StringComparison.OrdinalIgnoreCase))
        {
            var ansiColor = GetAnsiColorCode(source, logEntry.LogLevel);
            textWriter.Write("\u001b[");
            textWriter.Write(ansiColor);
            textWriter.Write("m");
            textWriter.WriteLine(line);
            textWriter.Write("\u001b[0m");
            if (!string.IsNullOrWhiteSpace(exceptionText))
            {
                textWriter.Write("\u001b[31m");
                textWriter.WriteLine(exceptionText);
                textWriter.Write("\u001b[0m");
            }
            return;
        }

        var color = GetColor(source, logEntry.LogLevel);
        lock (ConsoleLock)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                textWriter.WriteLine(line);
                if (!string.IsNullOrWhiteSpace(exceptionText))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    textWriter.WriteLine(exceptionText);
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }

    private ConsoleColor GetColor(string source, LogLevel level)
    {
        if (level >= LogLevel.Error)
        {
            return ConsoleColor.Red;
        }

        if (level == LogLevel.Warning)
        {
            return ConsoleColor.DarkYellow;
        }

        if (string.Equals(source, "WORKER", StringComparison.OrdinalIgnoreCase))
        {
            return _options.WorkerColor;
        }

        return _options.ApiColor;
    }

    private static string GetAnsiColorCode(string source, LogLevel level)
    {
        if (level >= LogLevel.Error)
        {
            return "31";
        }

        if (level == LogLevel.Warning)
        {
            return "33";
        }

        if (string.Equals(source, "WORKER", StringComparison.OrdinalIgnoreCase))
        {
            return "33";
        }

        return "32";
    }

    private static string GetLevelToken(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "UNK"
        };
    }

    private static string GetSourceFromCategory(string category)
    {
        if (category.Contains(".Workers.", StringComparison.Ordinal) || category.Contains(".Workers", StringComparison.Ordinal))
        {
            return "WORKER";
        }

        return "API";
    }

    private static string? NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (string.Equals(source, "WORKER", StringComparison.OrdinalIgnoreCase))
        {
            return "WORKER";
        }

        if (string.Equals(source, "API", StringComparison.OrdinalIgnoreCase))
        {
            return "API";
        }

        return source;
    }
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

        Dictionary<string, object?>? state = null;
        if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> stateValues)
        {
            state = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in stateValues)
            {
                state[kvp.Key] = ToJsonSafe(kvp.Value);
            }
        }

        string? sourceFromScope = null;
        List<object?>? scopes = null;
        if (_options.IncludeScopes && scopeProvider is not null)
        {
            scopes = new List<object?>();
            scopeProvider.ForEachScope(
                (scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                    {
                        var scopeDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var kvp in scopeValues)
                        {
                            var safeValue = ToJsonSafe(kvp.Value);
                            scopeDict[kvp.Key] = safeValue;
                            if (sourceFromScope is null && string.Equals(kvp.Key, "Source", StringComparison.OrdinalIgnoreCase) && safeValue is string s)
                            {
                                sourceFromScope = s;
                            }
                        }
                        list.Add(scopeDict);
                        return;
                    }

                    list.Add(scope?.ToString());
                },
                scopes);
        }

        var source = NormalizeSource(sourceFromScope) ?? GetSourceFromCategory(logEntry.Category);
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["EventId"] = logEntry.EventId.Id,
            ["LogLevel"] = logEntry.LogLevel.ToString(),
            ["Category"] = logEntry.Category,
            ["Source"] = source,
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

        var color = GetColor(source);

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

    private ConsoleColor GetColor(string source)
    {
        if (string.Equals(source, "WORKER", StringComparison.OrdinalIgnoreCase))
        {
            return _options.WorkerColor;
        }

        return _options.ApiColor;
    }

    private static string GetSourceFromCategory(string category)
    {
        if (category.Contains(".Workers.", StringComparison.Ordinal) || category.Contains(".Workers", StringComparison.Ordinal))
        {
            return "WORKER";
        }

        return "API";
    }

    private static string? NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (string.Equals(source, "WORKER", StringComparison.OrdinalIgnoreCase))
        {
            return "WORKER";
        }

        if (string.Equals(source, "API", StringComparison.OrdinalIgnoreCase))
        {
            return "API";
        }

        return source;
    }

    private static object? ToJsonSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement)
        {
            return value;
        }

        if (value is string || value is bool)
        {
            return value;
        }

        if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong)
        {
            return value;
        }

        if (value is float || value is double || value is decimal)
        {
            return value;
        }

        if (value is Guid || value is DateTime || value is DateTimeOffset || value is TimeSpan)
        {
            return value;
        }

        if (value is Enum e)
        {
            return e.ToString();
        }

        if (value is Type t)
        {
            return t.FullName ?? t.Name;
        }

        if (value is Exception ex)
        {
            return ex.ToString();
        }

        if (value is IEnumerable<KeyValuePair<string, object?>> dictValues)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in dictValues)
            {
                dict[kvp.Key] = ToJsonSafe(kvp.Value);
            }
            return dict;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(ToJsonSafe(item));
            }
            return list;
        }

        return value.ToString();
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
