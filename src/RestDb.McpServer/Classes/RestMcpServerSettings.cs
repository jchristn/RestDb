namespace RestDb.McpServer.Classes
{
    using System;

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

        public string HttpHostname { get; set; } = "+";

        public int HttpPort { get; set; } = 8010;

        public string TcpHostname { get; set; } = "0.0.0.0";

        public int TcpPort { get; set; } = 8011;

        public string WebSocketHostname { get; set; } = "+";

        public int WebSocketPort { get; set; } = 8012;

        public bool StdioOnly { get; set; } = false;

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

            string stdioValue = Environment.GetEnvironmentVariable("RESTDB_MCP_STDIO") ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(stdioValue) && Boolean.TryParse(stdioValue, out bool stdioOnly))
            {
                StdioOnly = stdioOnly;
            }
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
