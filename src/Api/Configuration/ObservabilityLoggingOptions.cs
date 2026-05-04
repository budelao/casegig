namespace CaseGig.Api.Configuration;

public sealed class ObservabilityLoggingOptions
{
    public bool Enabled { get; set; } = true;
    public bool AddCorrelationIdHeader { get; set; } = true;
    public string ServiceName { get; set; } = "CaseGig.Api";

    public bool LogRequestHeaders { get; set; } = false;
    public string[] RequestHeaderAllowList { get; set; } = Array.Empty<string>();

    public bool LogResponseHeaders { get; set; } = false;
    public string[] ResponseHeaderAllowList { get; set; } = Array.Empty<string>();

    public ExportTargets Export { get; set; } = new();

    public sealed class ExportTargets
    {
        public SplunkOptions Splunk { get; set; } = new();
        public GrafanaOptions Grafana { get; set; } = new();
        public DatadogOptions Datadog { get; set; } = new();
    }

    public sealed class SplunkOptions
    {
        public bool Enabled { get; set; } = false;
        public string? HecEndpoint { get; set; }
        public string Token { get; set; } = "CHANGE_ME";
        public string? Index { get; set; }
        public string? SourceType { get; set; }
    }

    public sealed class GrafanaOptions
    {
        public bool Enabled { get; set; } = false;
        public string? LokiEndpoint { get; set; }
        public string Token { get; set; } = "CHANGE_ME";
    }

    public sealed class DatadogOptions
    {
        public bool Enabled { get; set; } = false;
        public string Site { get; set; } = "datadoghq.com";
        public string ApiKey { get; set; } = "CHANGE_ME";
        public string? Service { get; set; }
        public string? Environment { get; set; }
    }
}
