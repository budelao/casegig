using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CaseGig.Api.Common.Logging;

internal sealed class PrettyConsoleFormatterOptions : ConsoleFormatterOptions
{
    public ConsoleColor WorkerColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleColor ApiColor { get; set; } = ConsoleColor.Green;
    public bool UseColors { get; set; } = !Console.IsOutputRedirected;
    public string ColorMode { get; set; } = "console";
}

internal sealed class PrettyConsoleFormatter : ConsoleFormatter
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
        if (category.Contains(".BackgroundJobs.", StringComparison.Ordinal)
            || category.Contains(".BackgroundJobs", StringComparison.Ordinal)
            || category.Contains(".Workers.", StringComparison.Ordinal)
            || category.Contains(".Workers", StringComparison.Ordinal))
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
