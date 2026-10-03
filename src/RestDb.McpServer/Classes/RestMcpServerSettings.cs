namespace RestDb.McpServer.Classes
{
    using System;
    using System.Collections.Generic;

    internal class RestMcpServerSettings
    {
        public bool ShowHelp { get; set; } = false;

        public bool InstallAgentDefinitions { get; set; } = false;

        public bool DryRun { get; set; } = false;

        public bool Yes { get; set; } = false;

        public string RestDbServerUrl { get; set; } = "http://localhost:8000";

        public string ApiKeyHeader { get; set; } = "x-api-key";

        public string? ApiKey { get; set; } = null;

        public string? BearerToken { get; set; } = null;

        public string HttpHostname { get; set; } = "localhost";

        public int HttpPort { get; set; } = 8010;

        public string TcpHostname { get; set; } = "127.0.0.1";

        public int TcpPort { get; set; } = 8011;

        public string WebSocketHostname { get; set; } = "localhost";

        public int WebSocketPort { get; set; } = 8012;

        public bool StdioOnly { get; set; } = false;

        /// <summary>
        /// Browser origins allowed to use the HTTP and WebSocket transports in addition to loopback origins.
        /// "*" allows any origin.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new List<string>();

        /// <summary>
        /// Optional bearer token MCP clients must present on the HTTP and WebSocket transports.
        /// </summary>
        public string? McpToken { get; set; } = null;

        /// <summary>
        /// Whether the telemetry host (OTLP export and Prometheus scrape endpoint) starts. Default true.
        /// </summary>
        public bool TelemetryEnable { get; set; } = true;

        /// <summary>
        /// Service name stamped as service.name on metrics and spans. Default restdb-mcp.
        /// </summary>
        public string TelemetryServiceName { get; set; } = "restdb-mcp";

        /// <summary>
        /// Whether traces and metrics are pushed to an OTLP collector. Default true.
        /// </summary>
        public bool OtlpEnable { get; set; } = true;

        /// <summary>
        /// OTLP collector endpoint. Default http://127.0.0.1:4317 (gRPC).
        /// </summary>
        public string OtlpEndpoint { get; set; } = "http://127.0.0.1:4317";

        /// <summary>
        /// OTLP protocol: grpc (default) or httpprotobuf.
        /// </summary>
        public string OtlpProtocol { get; set; } = "grpc";

        /// <summary>
        /// Explicit Prometheus scrape endpoint switch, or null for the default: on in network mode, off in stdio mode
        /// (agent clients may launch several stdio instances that would contend for one port).
        /// </summary>
        public bool? PrometheusEnable { get; set; } = null;

        /// <summary>
        /// Hostname the Prometheus endpoint binds. Default 127.0.0.1. The listener only answers requests whose Host header
        /// matches, so inside a container use the DNS name the scraper targets (for example "mcp"). Wildcards are rejected.
        /// </summary>
        public string PrometheusHostname { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port of the Prometheus endpoint. Default 9465.
        /// </summary>
        public int PrometheusPort { get; set; } = 9465;

        /// <summary>
        /// The effective Prometheus switch after applying the mode default.
        /// </summary>
        public bool EffectivePrometheusEnable
        {
            get
            {
                return PrometheusEnable ?? !StdioOnly;
            }
        }

        public static RestMcpServerSettings FromArgs(string[] args)
        {
            RestMcpServerSettings settings = new RestMcpServerSettings();
            settings.ApplyEnvironmentDefaults();

            if (args == null || args.Length < 1) return settings;

            for (int i = 0; i < args.Length; i++)
            {
                string current = args[i] ?? String.Empty;
                string currentLower = current.ToLowerInvariant();

                switch (currentLower)
                {
                    case "install":
                        settings.InstallAgentDefinitions = true;
                        break;
                    case "--dry-run":
                        settings.DryRun = true;
                        break;
                    case "--yes":
                    case "-y":
                        settings.Yes = true;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        settings.ShowHelp = true;
                        break;
                    case "--stdio":
                        settings.StdioOnly = true;
                        break;
                    case "--server-url":
                        settings.RestDbServerUrl = ReadStringValue(args, ref i, settings.RestDbServerUrl);
                        break;
                    case "--api-key":
                        settings.ApiKey = ReadStringValue(args, ref i, settings.ApiKey);
                        break;
                    case "--api-key-header":
                        settings.ApiKeyHeader = ReadStringValue(args, ref i, settings.ApiKeyHeader);
                        break;
                    case "--bearer-token":
                        settings.BearerToken = ReadStringValue(args, ref i, settings.BearerToken);
                        break;
                    case "--http-host":
                        settings.HttpHostname = ReadStringValue(args, ref i, settings.HttpHostname);
                        break;
                    case "--http-port":
                        settings.HttpPort = ReadIntValue(args, ref i, settings.HttpPort);
                        break;
                    case "--tcp-host":
                        settings.TcpHostname = ReadStringValue(args, ref i, settings.TcpHostname);
                        break;
                    case "--tcp-port":
                        settings.TcpPort = ReadIntValue(args, ref i, settings.TcpPort);
                        break;
                    case "--ws-host":
                        settings.WebSocketHostname = ReadStringValue(args, ref i, settings.WebSocketHostname);
                        break;
                    case "--ws-port":
                        settings.WebSocketPort = ReadIntValue(args, ref i, settings.WebSocketPort);
                        break;
                    case "--allowed-origins":
                        settings.AllowedOrigins = ParseList(ReadStringValue(args, ref i, String.Empty));
                        break;
                    case "--mcp-token":
                        settings.McpToken = ReadStringValue(args, ref i, settings.McpToken);
                        break;
                    case "--no-telemetry":
                        settings.TelemetryEnable = false;
                        break;
                    case "--otlp-endpoint":
                        settings.OtlpEndpoint = ReadStringValue(args, ref i, settings.OtlpEndpoint);
                        break;
                    case "--no-otlp":
                        settings.OtlpEnable = false;
                        break;
                    case "--prometheus-host":
                        settings.PrometheusHostname = ReadStringValue(args, ref i, settings.PrometheusHostname);
                        settings.PrometheusEnable = true;
                        break;
                    case "--prometheus-port":
                        settings.PrometheusPort = ReadIntValue(args, ref i, settings.PrometheusPort);
                        settings.PrometheusEnable = true;
                        break;
                    case "--no-prometheus":
                        settings.PrometheusEnable = false;
                        break;
                }
            }

            if (String.IsNullOrWhiteSpace(settings.RestDbServerUrl))
            {
                settings.RestDbServerUrl = "http://localhost:8000";
            }

            if (String.IsNullOrWhiteSpace(settings.ApiKeyHeader))
            {
                settings.ApiKeyHeader = "x-api-key";
            }

            if (String.IsNullOrWhiteSpace(settings.McpToken))
            {
                settings.McpToken = null;
            }

            return settings;
        }

        private void ApplyEnvironmentDefaults()
        {
            RestDbServerUrl = GetEnvironmentValue("RESTDB_MCP_SERVER_URL", RestDbServerUrl);
            ApiKeyHeader = GetEnvironmentValue("RESTDB_MCP_API_KEY_HEADER", ApiKeyHeader);
            ApiKey = GetEnvironmentValue("RESTDB_MCP_API_KEY", ApiKey);
            BearerToken = GetEnvironmentValue("RESTDB_MCP_BEARER_TOKEN", BearerToken);
            HttpHostname = GetEnvironmentValue("RESTDB_MCP_HTTP_HOST", HttpHostname);
            HttpPort = GetEnvironmentInt("RESTDB_MCP_HTTP_PORT", HttpPort);
            TcpHostname = GetEnvironmentValue("RESTDB_MCP_TCP_HOST", TcpHostname);
            TcpPort = GetEnvironmentInt("RESTDB_MCP_TCP_PORT", TcpPort);
            WebSocketHostname = GetEnvironmentValue("RESTDB_MCP_WS_HOST", WebSocketHostname);
            WebSocketPort = GetEnvironmentInt("RESTDB_MCP_WS_PORT", WebSocketPort);
            AllowedOrigins = ParseList(GetEnvironmentValue("RESTDB_MCP_ALLOWED_ORIGINS", String.Empty));
            McpToken = GetEnvironmentValue("RESTDB_MCP_TOKEN", McpToken);
            if (String.IsNullOrWhiteSpace(McpToken)) McpToken = null;

            TelemetryEnable = GetEnvironmentBool("RESTDB_MCP_TELEMETRY_ENABLE") ?? TelemetryEnable;
            TelemetryServiceName = GetEnvironmentValue("RESTDB_MCP_TELEMETRY_SERVICE_NAME", TelemetryServiceName);
            OtlpEnable = GetEnvironmentBool("RESTDB_MCP_OTLP_ENABLE") ?? OtlpEnable;
            OtlpEndpoint = GetEnvironmentValue("RESTDB_MCP_OTLP_ENDPOINT", OtlpEndpoint);
            OtlpProtocol = GetEnvironmentValue("RESTDB_MCP_OTLP_PROTOCOL", OtlpProtocol);
            PrometheusEnable = GetEnvironmentBool("RESTDB_MCP_PROMETHEUS_ENABLE") ?? PrometheusEnable;
            PrometheusHostname = GetEnvironmentValue("RESTDB_MCP_PROMETHEUS_HOST", PrometheusHostname);
            PrometheusPort = GetEnvironmentInt("RESTDB_MCP_PROMETHEUS_PORT", PrometheusPort);

            string stdioValue = Environment.GetEnvironmentVariable("RESTDB_MCP_STDIO") ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(stdioValue) && Boolean.TryParse(stdioValue, out bool stdioOnly))
            {
                StdioOnly = stdioOnly;
            }
        }

        private static List<string> ParseList(string? value)
        {
            List<string> items = new List<string>();
            if (String.IsNullOrWhiteSpace(value)) return items;

            foreach (string item in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                items.Add(item);
            }

            return items;
        }

        private static string ReadStringValue(string[] args, ref int index, string? defaultValue)
        {
            if (index + 1 >= args.Length) return defaultValue ?? String.Empty;
            index++;
            return args[index] ?? defaultValue ?? String.Empty;
        }

        private static int ReadIntValue(string[] args, ref int index, int defaultValue)
        {
            if (index + 1 >= args.Length) return defaultValue;
            index++;
            if (Int32.TryParse(args[index], out int value)) return value;
            return defaultValue;
        }

        private static string GetEnvironmentValue(string name, string? defaultValue)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return String.IsNullOrWhiteSpace(value) ? (defaultValue ?? String.Empty) : value;
        }

        private static bool? GetEnvironmentBool(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!String.IsNullOrWhiteSpace(value) && Boolean.TryParse(value, out bool result)) return result;
            return null;
        }

        private static int GetEnvironmentInt(string name, int defaultValue)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!String.IsNullOrWhiteSpace(value) && Int32.TryParse(value, out int result))
            {
                return result;
            }

            return defaultValue;
        }
    }
}
