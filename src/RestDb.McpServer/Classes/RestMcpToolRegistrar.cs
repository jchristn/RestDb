namespace RestDb.McpServer.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.McpServer.Telemetry;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Registers the RestDb tool catalog on every Voltaic MCP transport. Each tool is exposed as a
    /// proper MCP tool (tools/list and tools/call) and as a JSON-RPC method of the same name. Direct
    /// method calls return the raw <see cref="RestMcpResponse"/>; tools/call flags failed responses with isError.
    /// </summary>
    internal static class RestMcpToolRegistrar
    {
        internal static void Register(McpServer server, IEnumerable<RestMcpToolDefinition> tools)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            Register(
                tools,
                McpTelemetryNames.TransportStdio,
                (name, description, schema, handler) => server.RegisterTool(name, description, schema, handler),
                (name, handler) => server.RegisterMethod(name, handler));
        }

        internal static void Register(McpHttpServer server, IEnumerable<RestMcpToolDefinition> tools)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            Register(
                tools,
                McpTelemetryNames.TransportHttp,
                (name, description, schema, handler) => server.RegisterTool(name, description, schema, handler),
                (name, handler) => server.RegisterMethod(name, handler));
        }

        internal static void Register(McpTcpServer server, IEnumerable<RestMcpToolDefinition> tools)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            Register(
                tools,
                McpTelemetryNames.TransportTcp,
                (name, description, schema, handler) => server.RegisterTool(name, description, schema, handler),
                (name, handler) => server.RegisterMethod(name, handler));
        }

        internal static void Register(McpWebsocketsServer server, IEnumerable<RestMcpToolDefinition> tools)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            Register(
                tools,
                McpTelemetryNames.TransportWebSocket,
                (name, description, schema, handler) => server.RegisterTool(name, description, schema, handler),
                (name, handler) => server.RegisterMethod(name, handler));
        }

        internal static JsonElement? ToArguments(RpcParameters? parameters)
        {
            if (parameters == null || !parameters.HasValue) return null;

            string? rawJson = parameters.RawJson;
            if (String.IsNullOrWhiteSpace(rawJson)) return null;

            using JsonDocument document = JsonDocument.Parse(rawJson);
            return document.RootElement.Clone();
        }

        private static void Register(
            IEnumerable<RestMcpToolDefinition> tools,
            string transport,
            Action<string, string, object, Func<RpcParameters?, CancellationToken, Task<object>>> registerTool,
            Action<string, Func<RpcParameters?, CancellationToken, Task<object>>> registerMethod)
        {
            if (tools == null) throw new ArgumentNullException(nameof(tools));

            foreach (RestMcpToolDefinition tool in tools)
            {
                RestMcpToolDefinition current = tool;
                Func<RpcParameters?, CancellationToken, Task<object>> methodHandler =
                    (RpcParameters? parameters, CancellationToken token) =>
                        McpTelemetry.InvokeToolAsync(current.Name, transport, McpTelemetryNames.InvocationMethod, () => current.Handler(ToArguments(parameters), token));
                Func<RpcParameters?, CancellationToken, Task<object>> toolHandler =
                    async (RpcParameters? parameters, CancellationToken token) =>
                        ToToolResult(await McpTelemetry.InvokeToolAsync(
                            current.Name,
                            transport,
                            McpTelemetryNames.InvocationTool,
                            () => current.Handler(ToArguments(parameters), token)).ConfigureAwait(false));

                registerTool(current.Name, current.Description, current.InputSchema, toolHandler);
                registerMethod(current.Name, methodHandler);
            }
        }

        /// <summary>
        /// Marks a failed downstream RestDb response as an MCP tool error (isError: true) so clients can
        /// distinguish failures without parsing the payload. The text content is unchanged.
        /// </summary>
        internal static object ToToolResult(object result)
        {
            if (result is RestMcpResponse response && !response.Success)
            {
                McpToolCallResult error = McpToolCallResult.FromText(JsonSerializer.Serialize(response));
                error.IsError = true;
                return error;
            }

            return result;
        }
    }
}
