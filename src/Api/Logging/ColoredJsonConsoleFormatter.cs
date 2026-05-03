using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CaseGig.Api.Logging;

internal sealed class ColoredJsonConsoleFormatterOptions : ConsoleFormatterOptions
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

internal sealed class ColoredJsonConsoleFormatter : ConsoleFormatter
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
