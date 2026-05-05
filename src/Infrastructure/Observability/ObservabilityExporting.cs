using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;

namespace CaseGig.Infrastructure.Observability;

internal sealed class ObservabilityExportQueue
{
    private readonly Channel<ObservabilityLogEvent> _channel;

    public ObservabilityExportQueue(int capacity = 10_000)
    {
        _channel = Channel.CreateBounded<ObservabilityLogEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryWrite(ObservabilityLogEvent logEvent) => _channel.Writer.TryWrite(logEvent);

    public IAsyncEnumerable<ObservabilityLogEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed record ObservabilityLogEvent(
    DateTimeOffset Timestamp,
    LogLevel Level,
    int EventId,
    string Category,
    string Source,
    string? Service,
    string Message,
    string? Exception,
    Dictionary<string, object?>? State,
    List<object?>? Scopes);

internal sealed class ObservabilityExportLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? _scopeProvider;
    private readonly ObservabilityExportQueue _queue;
    private readonly IOptionsMonitor<ObservabilityLoggingOptions> _options;

    public ObservabilityExportLoggerProvider(ObservabilityExportQueue queue, IOptionsMonitor<ObservabilityLoggingOptions> options)
    {
        _queue = queue;
        _options = options;
    }

    public ILogger CreateLogger(string categoryName) => new ObservabilityExportLogger(categoryName, _queue, _options, () => _scopeProvider);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private sealed class ObservabilityExportLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ObservabilityExportQueue _queue;
        private readonly IOptionsMonitor<ObservabilityLoggingOptions> _options;
        private readonly Func<IExternalScopeProvider?> _scopeProviderAccessor;

        public ObservabilityExportLogger(
            string categoryName,
            ObservabilityExportQueue queue,
            IOptionsMonitor<ObservabilityLoggingOptions> options,
            Func<IExternalScopeProvider?> scopeProviderAccessor)
        {
            _categoryName = categoryName;
            _queue = queue;
            _options = options;
            _scopeProviderAccessor = scopeProviderAccessor;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
        {
            var opt = _options.CurrentValue;
            if (!opt.Enabled)
            {
                return false;
            }

            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var opt = _options.CurrentValue;
            if (!AnyExporterEnabled(opt))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var scopeProvider = _scopeProviderAccessor();
            if (scopeProvider is not null && HasSkipExport(scopeProvider))
            {
                return;
            }

            var safeState = TryGetSafeState(state);
            var (scopes, sourceFromScope, serviceFromScope) = GetSafeScopes(scopeProvider);
            var source = NormalizeSource(sourceFromScope) ?? GetSourceFromCategory(_categoryName);

            var evt = new ObservabilityLogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: logLevel,
                EventId: eventId.Id,
                Category: _categoryName,
                Source: source,
                Service: serviceFromScope ?? opt.ServiceName,
                Message: message,
                Exception: exception?.ToString(),
                State: safeState,
                Scopes: scopes);

            _queue.TryWrite(evt);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }

        private static bool AnyExporterEnabled(ObservabilityLoggingOptions options)
        {
            var export = options.Export;

            var splunk = export.Splunk.Enabled
                && !string.IsNullOrWhiteSpace(export.Splunk.HecEndpoint)
                && !string.IsNullOrWhiteSpace(export.Splunk.Token)
                && !export.Splunk.Token.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);

            var grafana = export.Grafana.Enabled
                && !string.IsNullOrWhiteSpace(export.Grafana.LokiEndpoint);

            var datadog = export.Datadog.Enabled
                && !string.IsNullOrWhiteSpace(export.Datadog.ApiKey)
                && !export.Datadog.ApiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);

            return splunk || grafana || datadog;
        }

        private static bool HasSkipExport(IExternalScopeProvider scopeProvider)
        {
            var skip = false;
            scopeProvider.ForEachScope(
                (scope, _) =>
                {
                    if (skip)
                    {
                        return;
                    }

                    if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                    {
                        foreach (var kvp in values)
                        {
                            if (string.Equals(kvp.Key, "SkipExport", StringComparison.OrdinalIgnoreCase) && kvp.Value is bool b && b)
                            {
                                skip = true;
                                return;
                            }
                        }
                    }
                },
                state: (object?)null);

            return skip;
        }

        private static Dictionary<string, object?>? TryGetSafeState<TState>(TState state)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> stateValues)
            {
                return null;
            }

            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in stateValues)
            {
                dict[kvp.Key] = ToJsonSafe(kvp.Value);
            }
            return dict;
        }

        private static (List<object?>? Scopes, string? Source, string? Service) GetSafeScopes(IExternalScopeProvider? scopeProvider)
        {
            if (scopeProvider is null)
            {
                return (null, null, null);
            }

            string? sourceFromScope = null;
            string? serviceFromScope = null;
            var scopes = new List<object?>();
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

                            if (serviceFromScope is null && string.Equals(kvp.Key, "Service", StringComparison.OrdinalIgnoreCase) && safeValue is string srv)
                            {
                                serviceFromScope = srv;
                            }
                        }
                        list.Add(scopeDict);
                        return;
                    }

                    list.Add(scope?.ToString());
                },
                scopes);

            return (scopes, sourceFromScope, serviceFromScope);
        }

        private static string GetSourceFromCategory(string category) => "APP";

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
}

internal sealed class ObservabilityExportWorker : BackgroundService
{
    private readonly ObservabilityExportQueue _queue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ObservabilityLoggingOptions> _options;
    private readonly ILogger<ObservabilityExportWorker> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ObservabilityExportWorker(
        ObservabilityExportQueue queue,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ObservabilityLoggingOptions> options,
        ILogger<ObservabilityExportWorker> logger)
    {
        _queue = queue;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _queue.ReadAllAsync(stoppingToken))
        {
            var opt = _options.CurrentValue;
            if (!opt.Enabled)
            {
                continue;
            }

            await ExportAsync(evt, opt, stoppingToken);
        }
    }

    private async Task ExportAsync(ObservabilityLogEvent evt, ObservabilityLoggingOptions options, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>(3);

        if (options.Export.Splunk.Enabled && IsNonPlaceholder(options.Export.Splunk.HecEndpoint) && IsNonPlaceholder(options.Export.Splunk.Token))
        {
            tasks.Add(ExportToSplunkAsync(evt, options, cancellationToken));
        }

        if (options.Export.Grafana.Enabled && IsNonPlaceholder(options.Export.Grafana.LokiEndpoint))
        {
            tasks.Add(ExportToLokiAsync(evt, options, cancellationToken));
        }

        if (options.Export.Datadog.Enabled && IsNonPlaceholder(options.Export.Datadog.ApiKey))
        {
            tasks.Add(ExportToDatadogAsync(evt, options, cancellationToken));
        }

        if (tasks.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            using (_logger.BeginScope(new Dictionary<string, object?> { ["SkipExport"] = true }))
            {
                _logger.LogWarning(ex, "Falha ao exportar logs para observabilidade");
            }
        }
    }

    private async Task ExportToSplunkAsync(ObservabilityLogEvent evt, ObservabilityLoggingOptions options, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ExportClients.Splunk);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["time"] = evt.Timestamp.ToUnixTimeSeconds(),
            ["host"] = Environment.MachineName,
            ["source"] = evt.Source,
            ["sourcetype"] = options.Export.Splunk.SourceType,
            ["index"] = options.Export.Splunk.Index,
            ["event"] = BuildStructuredPayload(evt)
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = content };

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Splunk HEC retornou {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private async Task ExportToLokiAsync(ObservabilityLogEvent evt, ObservabilityLoggingOptions options, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ExportClients.Loki);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app"] = options.ServiceName,
            ["source"] = evt.Source,
            ["level"] = evt.Level.ToString()
        };

        if (!string.IsNullOrWhiteSpace(options.Export.Datadog.Environment))
        {
            labels["env"] = options.Export.Datadog.Environment!;
        }

        var line = JsonSerializer.Serialize(BuildStructuredPayload(evt), JsonOptions);
        var body = new
        {
            streams = new[]
            {
                new
                {
                    stream = labels,
                    values = new[]
                    {
                        new[] { (evt.Timestamp.ToUnixTimeMilliseconds() * 1_000_000L).ToString(), line }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = content };

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Grafana Loki retornou {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private async Task ExportToDatadogAsync(ObservabilityLogEvent evt, ObservabilityLoggingOptions options, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ExportClients.Datadog);

        var dd = options.Export.Datadog;

        var item = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = evt.Message,
            ["ddsource"] = evt.Source,
            ["service"] = dd.Service ?? options.ServiceName,
            ["hostname"] = Environment.MachineName,
            ["status"] = evt.Level.ToString().ToLowerInvariant(),
            ["date"] = evt.Timestamp.ToUnixTimeMilliseconds(),
            ["logger.name"] = evt.Category,
            ["exception"] = evt.Exception,
            ["attributes"] = BuildStructuredPayload(evt)
        };

        if (!string.IsNullOrWhiteSpace(dd.Environment))
        {
            item["ddtags"] = $"env:{dd.Environment}";
        }

        var json = JsonSerializer.Serialize(new[] { item }, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = content };

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Datadog Logs retornou {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private static object BuildStructuredPayload(ObservabilityLogEvent evt)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Timestamp"] = evt.Timestamp,
            ["EventId"] = evt.EventId,
            ["LogLevel"] = evt.Level.ToString(),
            ["Category"] = evt.Category,
            ["Source"] = evt.Source,
            ["Service"] = evt.Service,
            ["Message"] = evt.Message,
            ["State"] = evt.State,
            ["Scopes"] = evt.Scopes,
            ["Exception"] = evt.Exception
        };
    }

    private static bool IsNonPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

internal static class ExportClients
{
    public const string Splunk = "observability-splunk";
    public const string Loki = "observability-loki";
    public const string Datadog = "observability-datadog";
}
