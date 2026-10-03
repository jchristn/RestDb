namespace RestDb.McpServer.Telemetry
{
    /// <summary>
    /// Every telemetry name RestDb.McpServer emits: the meter and activity source, instrument names, attribute (label)
    /// keys, and bounded label values. These strings are public contract consumed by Grafana dashboards and alert rules
    /// (see TELEMETRY.md); treat any change as a breaking change.
    /// Thread safety: constants only; safe for concurrent use.
    /// </summary>
    public static class McpTelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the RestDb.McpServer meter and activity source.
        /// </summary>
        public const string SourceName = "RestDb.McpServer";

        /// <summary>
        /// Name of the .NET HttpClient meter (outbound request duration and connection pool metrics).
        /// </summary>
        public const string HttpClientMeterName = "System.Net.Http";

        /// <summary>
        /// Name of the Voltaic meter and activity source (MCP transport, session, and protocol telemetry; its server
        /// span per request is the parent of the RestDb.McpServer tool span).
        /// </summary>
        public const string VoltaicSourceName = "Voltaic";

        #endregion

        #region Instruments

        /// <summary>
        /// Counter of MCP tool invocations, by tool, transport, invocation style, and outcome.
        /// </summary>
        public const string ToolCalls = "restdb.mcp.tool.calls";

        /// <summary>
        /// Histogram of MCP tool invocation duration in seconds, by tool, transport, invocation style, and outcome.
        /// </summary>
        public const string ToolDuration = "restdb.mcp.tool.duration";

        /// <summary>
        /// Up/down counter of MCP tool invocations currently executing, by transport.
        /// </summary>
        public const string ToolActive = "restdb.mcp.tool.active";

        /// <summary>
        /// Counter of requests from the MCP server to the RestDb API, by method, route template, outcome, and status code.
        /// </summary>
        public const string DownstreamRequests = "restdb.mcp.downstream.requests";

        /// <summary>
        /// Histogram of RestDb API request duration in seconds as seen by the MCP server, by method, route template, and outcome.
        /// </summary>
        public const string DownstreamDuration = "restdb.mcp.downstream.duration";

        /// <summary>
        /// Counter of MCP client connections accepted, by transport.
        /// </summary>
        public const string Connections = "restdb.mcp.connections";

        /// <summary>
        /// Up/down counter of MCP client connections currently open, by transport.
        /// </summary>
        public const string ConnectionsActive = "restdb.mcp.connections.active";

        /// <summary>
        /// Gauge that is always 1, labeled with the running version and mode (network or stdio).
        /// </summary>
        public const string BuildInfo = "restdb.mcp.build.info";

        /// <summary>
        /// Gauge that is 1 when MCP clients must present a bearer token on HTTP and WebSocket.
        /// </summary>
        public const string ConfigTokenRequired = "restdb.mcp.config.token_required";

        /// <summary>
        /// Gauge of the number of browser origins allowed in addition to loopback.
        /// </summary>
        public const string ConfigAllowedOrigins = "restdb.mcp.config.allowed_origins";

        #endregion

        #region Attributes

        /// <summary>
        /// OpenTelemetry GenAI attribute key for the tool name. Bounded by the tool catalog.
        /// </summary>
        public const string AttributeToolName = "gen_ai.tool.name";

        /// <summary>
        /// OpenTelemetry GenAI attribute key for the operation name (always execute_tool). Span-only.
        /// </summary>
        public const string AttributeGenAiOperation = "gen_ai.operation.name";

        /// <summary>
        /// OpenTelemetry MCP attribute key for the JSON-RPC method name. Span-only.
        /// </summary>
        public const string AttributeMcpMethod = "mcp.method.name";

        /// <summary>
        /// Attribute key for the MCP transport (http, tcp, websocket, or stdio).
        /// </summary>
        public const string AttributeTransport = "restdb.mcp.transport";

        /// <summary>
        /// Attribute key for the invocation style: tool (tools/call) or method (direct JSON-RPC method).
        /// </summary>
        public const string AttributeInvocation = "restdb.mcp.invocation";

        /// <summary>
        /// Attribute key for the outcome (see the Outcome* constants).
        /// </summary>
        public const string AttributeOutcome = "outcome";

        /// <summary>
        /// OpenTelemetry attribute key for the error type: an exception type name or an HTTP status code.
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// OpenTelemetry attribute key for the HTTP request method.
        /// </summary>
        public const string AttributeHttpMethod = "http.request.method";

        /// <summary>
        /// OpenTelemetry attribute key for the HTTP response status code.
        /// </summary>
        public const string AttributeHttpStatusCode = "http.response.status_code";

        /// <summary>
        /// OpenTelemetry attribute key for the RestDb route template (for example /{database}/{table}).
        /// </summary>
        public const string AttributeUrlTemplate = "url.template";

        /// <summary>
        /// OpenTelemetry attribute key for the downstream server host. Span-only.
        /// </summary>
        public const string AttributeServerAddress = "server.address";

        /// <summary>
        /// OpenTelemetry attribute key for the downstream server port. Span-only.
        /// </summary>
        public const string AttributeServerPort = "server.port";

        /// <summary>
        /// Attribute key for the MCP server mode (network or stdio).
        /// </summary>
        public const string AttributeMode = "restdb.mcp.mode";

        /// <summary>
        /// OpenTelemetry attribute key for the service version.
        /// </summary>
        public const string AttributeServiceVersion = "service.version";

        #endregion

        #region Values

        /// <summary>Outcome: the tool or request succeeded.</summary>
        public const string OutcomeSuccess = "success";

        /// <summary>Outcome: RestDb answered with a non-success status (the tool result is flagged isError).</summary>
        public const string OutcomeDownstreamError = "downstream_error";

        /// <summary>Outcome: an exception escaped (handler failure, network error, timeout).</summary>
        public const string OutcomeError = "error";

        /// <summary>Invocation style: tools/call.</summary>
        public const string InvocationTool = "tool";

        /// <summary>Invocation style: direct JSON-RPC method named after the tool.</summary>
        public const string InvocationMethod = "method";

        /// <summary>Transport: Streamable HTTP.</summary>
        public const string TransportHttp = "http";

        /// <summary>Transport: framed TCP.</summary>
        public const string TransportTcp = "tcp";

        /// <summary>Transport: WebSocket.</summary>
        public const string TransportWebSocket = "websocket";

        /// <summary>Transport: stdio.</summary>
        public const string TransportStdio = "stdio";

        /// <summary>Mode: HTTP, TCP, and WebSocket listeners.</summary>
        public const string ModeNetwork = "network";

        /// <summary>Mode: stdio only.</summary>
        public const string ModeStdio = "stdio";

        #endregion
    }
}
