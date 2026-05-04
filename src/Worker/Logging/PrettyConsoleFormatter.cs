using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CaseGig.Worker.Logging;

internal sealed class PrettyConsoleFormatterOptions : ConsoleFormatterOptions
{
    public ConsoleColor BaseColor { get; set; } = ConsoleColor.Yellow;
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

        var now = _options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        var timestampFormat = string.IsNullOrWhiteSpace(_options.TimestampFormat) ? "HH:mm:ss.fff" : _options.TimestampFormat;
        var timestamp = now.ToString(timestampFormat, CultureInfo.InvariantCulture);
        var level = GetLevelToken(logEntry.LogLevel);

        var line = $"{timestamp} {level} WORKER {message}";
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
            var ansiColor = GetAnsiColorCode(logEntry.LogLevel);
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

        var color = GetColor(logEntry.LogLevel);
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

    private ConsoleColor GetColor(LogLevel level)
    {
        if (level >= LogLevel.Error)
        {
            return ConsoleColor.Red;
        }

        if (level == LogLevel.Warning)
        {
            return ConsoleColor.DarkYellow;
        }

        return _options.BaseColor;
    }

    private static string GetAnsiColorCode(LogLevel level)
    {
        if (level >= LogLevel.Error)
        {
            return "31";
        }

        if (level == LogLevel.Warning)
        {
            return "33";
        }

        return "33";
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
}
