namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RestDb.McpServer.Classes;
using RestDb.McpServer.Registrations;
using Voltaic.Mcp;

/// <summary>
/// Exercises the TCP and WebSocket MCP transports with the RestDb tool registration used in production.
/// </summary>
internal static class McpTransportAssertions
{
    private delegate Task<JsonElement> McpCall(string method, object? parameters);

    public static Task TcpListsAndCallsRegisteredToolsAsync() => WithTcpAsync(ListsAndCallsRegisteredToolsAsync);

    public static Task TcpInvokesToolAsDirectMethodAsync() => WithTcpAsync(InvokesToolAsDirectMethodAsync);

    public static Task TcpRejectsInvalidToolCallsAsync() => WithTcpAsync(RejectsInvalidToolCallsAsync);

    public static Task WebSocketListsAndCallsRegisteredToolsAsync() => WithWebSocketAsync(ListsAndCallsRegisteredToolsAsync);

    public static Task WebSocketInvokesToolAsDirectMethodAsync() => WithWebSocketAsync(InvokesToolAsDirectMethodAsync);

    public static Task WebSocketRejectsInvalidToolCallsAsync() => WithWebSocketAsync(RejectsInvalidToolCallsAsync);

    public static Task TcpFlagsFailedDownstreamResponsesAsync() => WithTcpAsync(FlagsFailedDownstreamResponsesAsync);

    public static Task WebSocketFlagsFailedDownstreamResponsesAsync() => WithWebSocketAsync(FlagsFailedDownstreamResponsesAsync);

    public static Task TcpListsOnlyRestDbToolsAsync() => WithTcpAsync(ListsOnlyRestDbToolsAsync);

    public static Task WebSocketListsOnlyRestDbToolsAsync() => WithWebSocketAsync(ListsOnlyRestDbToolsAsync);

    public static Task TcpPingReturnsEmptyResultAsync() => WithTcpAsync(PingReturnsEmptyResultAsync);

    public static Task WebSocketPingReturnsEmptyResultAsync() => WithWebSocketAsync(PingReturnsEmptyResultAsync);

    public static Task TcpRejectsRemovedVoltaicDemoToolsAsync() => WithTcpAsync(RejectsRemovedVoltaicDemoToolsAsync);

    public static Task WebSocketRejectsRemovedVoltaicDemoToolsAsync() => WithWebSocketAsync(RejectsRemovedVoltaicDemoToolsAsync);

    public static Task TcpCatalogListsExactlyRestDbToolsAsync() => WithCatalogAsync(CatalogListsExactlyRestDbToolsAsync);

    public static Task TcpCatalogRejectsNonStringTableContextAsync() => WithCatalogAsync(CatalogRejectsNonStringTableContextAsync);

    public static Task TcpCatalogAcceptsStringTableContextAsync() => WithCatalogAsync(CatalogAcceptsStringTableContextAsync);

    public static Task TcpCatalogAcceptsArbitraryFiltersAsync() => WithCatalogAsync(CatalogAcceptsArbitraryFiltersAsync);

    private static async Task ListsOnlyRestDbToolsAsync(McpCall call)
    {
        JsonElement list = await call("tools/list", null).ConfigureAwait(false);
        string body = list.GetRawText();
        McpBridgeAssertions.AssertToolNamesExactly(McpBridgeAssertions.RequireProperty(list, "tools", body), McpTestTools.Names(), body);
    }

    private static async Task PingReturnsEmptyResultAsync(McpCall call)
    {
        JsonElement result = await call("ping", null).ConfigureAwait(false);
        McpBridgeAssertions.AssertEmptyObject(result, result.GetRawText());
    }

    private static async Task RejectsRemovedVoltaicDemoToolsAsync(McpCall call)
    {
        foreach (string name in McpBridgeAssertions.RemovedVoltaicDemoToolNames)
        {
            await AssertRpcErrorAsync(
                () => call("tools/call", new { name = name, arguments = new { } }),
                McpBridgeAssertions.InvalidParamsCode,
                name).ConfigureAwait(false);

            // "ping" is the MCP protocol method, so it stays callable; the demo names must not.
            if (name == "ping") continue;

            await AssertRpcErrorAsync(
                () => call(name, new { }),
                McpBridgeAssertions.MethodNotFoundCode,
                null).ConfigureAwait(false);
        }
    }

    private static async Task CatalogListsExactlyRestDbToolsAsync(McpCall call, List<RestMcpToolDefinition> catalog)
    {
        JsonElement list = await call("tools/list", null).ConfigureAwait(false);
        string body = list.GetRawText();
        List<string> names = new List<string>();
        foreach (RestMcpToolDefinition tool in catalog) names.Add(tool.Name);

        TestAssert.True(names.Count > 0, "Expected the RestDb tool catalog to be non-empty.");
        McpBridgeAssertions.AssertToolNamesExactly(McpBridgeAssertions.RequireProperty(list, "tools", body), names, body);
    }

    private static async Task CatalogRejectsNonStringTableContextAsync(McpCall call, List<RestMcpToolDefinition> catalog)
    {
        // restdb_update_database_context declares tables as { additionalProperties: { type: string } }, which Voltaic 2.x enforces.
        await AssertRpcErrorAsync(
            () => call("tools/call", new
            {
                name = "restdb_update_database_context",
                arguments = new { databaseName = "test", tables = new { person = 42 } }
            }),
            McpBridgeAssertions.InvalidParamsCode,
            "person").ConfigureAwait(false);
    }

    private static async Task CatalogAcceptsStringTableContextAsync(McpCall call, List<RestMcpToolDefinition> catalog)
    {
        await AssertPassesSchemaValidationAsync(() => call("tools/call", new
        {
            name = "restdb_update_database_context",
            arguments = new { databaseName = "test", tables = new { person = "People known to the system." } }
        })).ConfigureAwait(false);
    }

    private static async Task CatalogAcceptsArbitraryFiltersAsync(McpCall call, List<RestMcpToolDefinition> catalog)
    {
        // filters declares additionalProperties: true, so any property name and value type must pass validation.
        await AssertPassesSchemaValidationAsync(() => call("tools/call", new
        {
            name = "restdb_enumerate_table_records",
            arguments = new { databaseName = "test", tableName = "person", filters = new { firstName = "Joel", age = 40, active = true } }
        })).ConfigureAwait(false);
    }

    /// <summary>
    /// The catalog proxies to an unreachable RestDb URL, so a call that passes schema validation fails
    /// downstream. Anything other than an invalid-params error proves the arguments were accepted.
    /// </summary>
    private static async Task AssertPassesSchemaValidationAsync(Func<Task<JsonElement>> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TestAssert.DoesNotContain("RPC Error " + McpBridgeAssertions.InvalidParamsCode, ex.Message, StringComparison.Ordinal, "Expected the arguments to pass schema validation." + Environment.NewLine + ex.Message);
        }
    }

    public static void ProxyKeepsOnlyApplicationHeaders()
    {
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = "Sat, 26 Sep 2026 00:00:00 GMT",
            ["Connection"] = "close",
            ["Content-Type"] = "application/json",
            ["Content-Length"] = "16",
            ["Access-Control-Allow-Origin"] = "*",
            ["Access-Control-Expose-Headers"] = "x-expression",
            ["x-expression"] = "(age >= 18)",
            ["X-Restart-Required"] = "true",
            ["x-operation-message"] = "Settings saved."
        };

        Dictionary<string, string>? selected = RestMcpRestProxy.SelectApplicationHeaders(headers);
        TestAssert.NotNull(selected, "Expected the x- headers to be kept.");
        TestAssert.Equal(3, selected!.Count, string.Join(", ", selected.Keys));
        TestAssert.Equal("(age >= 18)", selected["x-expression"], "x-expression value");
        TestAssert.Equal("true", selected["x-restart-required"], "Header lookup stays case-insensitive.");
        TestAssert.Equal("Settings saved.", selected["x-operation-message"], "x-operation-message value");

        Dictionary<string, string> transportOnly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = "Sat, 26 Sep 2026 00:00:00 GMT",
            ["Access-Control-Allow-Origin"] = "*"
        };
        TestAssert.Null(RestMcpRestProxy.SelectApplicationHeaders(transportOnly), "Headers must be omitted when there are no application headers.");
    }

    public static Task LiveCatalogOmitsTransportHeadersAsync() => WithLiveCatalogAsync(async (call, session) =>
    {
        JsonElement result = await call("tools/call", new { name = "restdb_retrieve_database_list", arguments = new { } }).ConfigureAwait(false);
        string text = ToolText(result);
        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement root = document.RootElement;

        TestAssert.True(McpBridgeAssertions.RequireProperty(root, "Success", text).GetBoolean(), text);
        TestAssert.Equal(200, McpBridgeAssertions.RequireProperty(root, "StatusCode", text).GetInt32(), text);
        TestAssert.Contains(session.DatabaseName, McpBridgeAssertions.RequireProperty(root, "Body", text).GetRawText(), StringComparison.Ordinal, text);
        TestAssert.False(root.TryGetProperty("Headers", out _), "Transport-only responses must not carry a Headers property. " + text);

        foreach (string noise in new[] { "Date", "Connection", "Access-Control", "Content-Length", "Cache-Control" })
        {
            TestAssert.DoesNotContain(noise, text, StringComparison.OrdinalIgnoreCase, "Transport header '" + noise + "' leaked into the tool result. " + text);
        }

        // Direct JSON-RPC method calls return the same trimmed response object.
        JsonElement direct = await call("restdb_retrieve_database_list", new { }).ConfigureAwait(false);
        TestAssert.False(direct.TryGetProperty("Headers", out _), "Direct calls must not carry transport headers. " + direct.GetRawText());
    });

    public static Task LiveCatalogKeepsExpressionDebugHeaderAsync() => WithLiveCatalogAsync(async (call, session) =>
    {
        string tableName = "restdb_mcp_hdr_" + Guid.NewGuid().ToString("N").Substring(0, 20);

        try
        {
            JsonElement created = await call("tools/call", new
            {
                name = "restdb_create_table",
                arguments = new
                {
                    databaseName = session.DatabaseName,
                    table = new { Name = tableName, PrimaryKey = "person_id", Columns = TestData.SampleColumns() }
                }
            }).ConfigureAwait(false);
            TestAssert.Contains("\\u0022Success\\u0022:true", created.GetRawText(), StringComparison.Ordinal, "Create table failed: " + created.GetRawText());

            JsonElement inserted = await call("tools/call", new
            {
                name = "restdb_insert_table_record",
                arguments = new { databaseName = session.DatabaseName, tableName, record = TestData.SampleInsertValues() }
            }).ConfigureAwait(false);
            TestAssert.Contains("\\u0022Success\\u0022:true", inserted.GetRawText(), StringComparison.Ordinal, "Insert failed: " + inserted.GetRawText());

            object expression = new { Left = "age", Operator = "GreaterThanOrEqualTo", Right = 18 };

            JsonElement debugResult = await call("tools/call", new
            {
                name = "restdb_search_table_records",
                arguments = new { databaseName = session.DatabaseName, tableName, expression, debug = true }
            }).ConfigureAwait(false);
            string debugText = ToolText(debugResult);
            using (JsonDocument debugDocument = JsonDocument.Parse(debugText))
            {
                JsonElement headers = McpBridgeAssertions.RequireProperty(debugDocument.RootElement, "Headers", debugText);
                bool foundExpression = false;
                foreach (JsonProperty header in headers.EnumerateObject())
                {
                    TestAssert.True(header.Name.StartsWith("x-", StringComparison.OrdinalIgnoreCase), "Only x- headers may be returned, found '" + header.Name + "'. " + debugText);
                    if (header.Name.Equals("x-expression", StringComparison.OrdinalIgnoreCase))
                    {
                        foundExpression = !string.IsNullOrWhiteSpace(header.Value.GetString());
                    }
                }

                TestAssert.True(foundExpression, "Expected the x-expression debug header with debug=true. " + debugText);
                TestAssert.Contains("joel", McpBridgeAssertions.RequireProperty(debugDocument.RootElement, "Body", debugText).GetRawText(), StringComparison.Ordinal, debugText);
            }

            JsonElement plainResult = await call("tools/call", new
            {
                name = "restdb_search_table_records",
                arguments = new { databaseName = session.DatabaseName, tableName, expression }
            }).ConfigureAwait(false);
            string plainText = ToolText(plainResult);
            TestAssert.DoesNotContain("x-expression", plainText, StringComparison.OrdinalIgnoreCase, "Without debug there is no x-expression header. " + plainText);
        }
        finally
        {
            try
            {
                await call("tools/call", new { name = "restdb_drop_table", arguments = new { databaseName = session.DatabaseName, tableName } }).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    });

    private static string ToolText(JsonElement toolResult)
    {
        string raw = toolResult.GetRawText();
        foreach (JsonElement item in McpBridgeAssertions.RequireProperty(toolResult, "content", raw).EnumerateArray())
        {
            if (item.TryGetProperty("text", out JsonElement text)) return text.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("Expected text content in the tool result. " + raw);
    }

    private static async Task WithLiveCatalogAsync(Func<McpCall, RestDbLiveApiSession, Task> body)
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        RestMcpServerSettings settings = new RestMcpServerSettings
        {
            RestDbServerUrl = session.BaseAddress.ToString().TrimEnd('/'),
            ApiKey = session.ApiKey,
            ApiKeyHeader = session.ApiKeyHeader
        };

        using RestMcpRestProxy proxy = new RestMcpRestProxy(settings);
        List<RestMcpToolDefinition> catalog = RestMcpToolCatalog.Build(proxy);
        await WithTcpAsync(catalog, call => body(call, session)).ConfigureAwait(false);
    }

    private static async Task WithCatalogAsync(Func<McpCall, List<RestMcpToolDefinition>, Task> body)
    {
        RestMcpServerSettings settings = new RestMcpServerSettings
        {
            RestDbServerUrl = "http://127.0.0.1:" + ReserveLoopbackPort()
        };

        using RestMcpRestProxy proxy = new RestMcpRestProxy(settings);
        List<RestMcpToolDefinition> catalog = RestMcpToolCatalog.Build(proxy);
        await WithTcpAsync(catalog, call => body(call, catalog)).ConfigureAwait(false);
    }

    private static async Task FlagsFailedDownstreamResponsesAsync(McpCall call)
    {
        JsonElement failed = await call("tools/call", new { name = McpTestTools.DownstreamNotFoundToolName, arguments = new { } }).ConfigureAwait(false);
        McpBridgeAssertions.AssertDownstreamToolResult(failed, expectError: true, "downstream-not-found", failed.GetRawText());

        JsonElement succeeded = await call("tools/call", new { name = McpTestTools.DownstreamOkToolName, arguments = new { } }).ConfigureAwait(false);
        McpBridgeAssertions.AssertDownstreamToolResult(succeeded, expectError: false, "downstream-ok", succeeded.GetRawText());

        // Direct JSON-RPC method calls keep returning the raw RestDb response rather than a tool result.
        JsonElement direct = await call(McpTestTools.DownstreamNotFoundToolName, new { }).ConfigureAwait(false);
        string directBody = direct.GetRawText();
        TestAssert.False(McpBridgeAssertions.RequireProperty(direct, "Success", directBody).GetBoolean(), directBody);
        TestAssert.Equal(404, McpBridgeAssertions.RequireProperty(direct, "StatusCode", directBody).GetInt32(), directBody);
        TestAssert.False(direct.TryGetProperty("isError", out _), "Direct method calls must not be wrapped as tool results." + Environment.NewLine + directBody);
    }

    private static async Task ListsAndCallsRegisteredToolsAsync(McpCall call)
    {
        JsonElement initialize = await call("initialize", new
        {
            protocolVersion = McpBridgeAssertions.NewestHandshakeProtocolVersion,
            capabilities = new { },
            clientInfo = new { name = "restdb-tests", version = "1.0.0" }
        }).ConfigureAwait(false);

        string initializeBody = initialize.GetRawText();
        JsonElement capabilities = McpBridgeAssertions.RequireProperty(initialize, "capabilities", initializeBody);
        TestAssert.True(capabilities.TryGetProperty("tools", out _), "Expected the tools capability to be advertised." + Environment.NewLine + initializeBody);

        JsonElement list = await call("tools/list", null).ConfigureAwait(false);
        string listBody = list.GetRawText();
        bool listed = false;
        foreach (JsonElement tool in McpBridgeAssertions.RequireProperty(list, "tools", listBody).EnumerateArray())
        {
            if (tool.TryGetProperty("name", out JsonElement name) && name.GetString() == McpTestTools.EchoToolName)
            {
                listed = true;
                TestAssert.True(tool.TryGetProperty("inputSchema", out _), "Expected inputSchema on the listed tool." + Environment.NewLine + listBody);
            }
        }

        TestAssert.True(listed, "Expected tool '" + McpTestTools.EchoToolName + "' in tools/list." + Environment.NewLine + listBody);

        JsonElement result = await call("tools/call", new
        {
            name = McpTestTools.EchoToolName,
            arguments = new { message = "transport-hello" }
        }).ConfigureAwait(false);

        string resultBody = result.GetRawText();
        TestAssert.Contains("transport-hello", McpBridgeAssertions.RequireProperty(result, "content", resultBody).GetRawText(), StringComparison.Ordinal, resultBody);
        TestAssert.False(
            result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True,
            "Expected a successful tool result." + Environment.NewLine + resultBody);
    }

    private static async Task InvokesToolAsDirectMethodAsync(McpCall call)
    {
        JsonElement result = await call(McpTestTools.EchoToolName, new { message = "direct-hello" }).ConfigureAwait(false);
        string body = result.GetRawText();
        TestAssert.Equal("direct-hello", McpBridgeAssertions.RequireProperty(result, "echoed", body).GetString(), body);
    }

    private static async Task RejectsInvalidToolCallsAsync(McpCall call)
    {
        await AssertRpcErrorAsync(
            () => call("tools/call", new { name = "no_such_tool", arguments = new { } }),
            McpBridgeAssertions.InvalidParamsCode,
            "no_such_tool").ConfigureAwait(false);

        await AssertRpcErrorAsync(
            () => call("tools/call", new { name = McpTestTools.EchoToolName, arguments = new { } }),
            McpBridgeAssertions.InvalidParamsCode,
            "message").ConfigureAwait(false);

        await AssertRpcErrorAsync(
            () => call("no/such/method", null),
            -32601,
            null).ConfigureAwait(false);

        JsonElement failure;
        try
        {
            failure = await call("tools/call", new { name = McpTestTools.FailToolName, arguments = new { } }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains("RPC Error", StringComparison.Ordinal))
        {
            return;
        }

        string failureBody = failure.GetRawText();
        TestAssert.True(
            failure.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True,
            "Expected a JSON-RPC error or an isError tool result for a failing tool." + Environment.NewLine + failureBody);
    }

    private static async Task AssertRpcErrorAsync(Func<Task<JsonElement>> action, int expectedCode, string? expectedMessageFragment)
    {
        try
        {
            JsonElement unexpected = await action().ConfigureAwait(false);
            throw new InvalidOperationException("Expected JSON-RPC error " + expectedCode + " but the call succeeded: " + unexpected.GetRawText());
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            TestAssert.Contains("RPC Error " + expectedCode, ex.Message, StringComparison.Ordinal, ex.Message);
            if (expectedMessageFragment != null)
            {
                TestAssert.Contains(expectedMessageFragment, ex.Message, StringComparison.Ordinal, ex.Message);
            }
        }
    }

    private static async Task WithTcpAsync(Func<McpCall, Task> body) => await WithTcpAsync(McpTestTools.Build(), body).ConfigureAwait(false);

    private static async Task WithTcpAsync(List<RestMcpToolDefinition> tools, Func<McpCall, Task> body)
    {
        int port = ReserveLoopbackPort();
        using McpTcpServer server = RestMcpTransportFactory.CreateTcpServer(IPAddress.Loopback, port);
        RestMcpToolRegistrar.Register(server, tools);

        using CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task serverTask = Task.Run(() => server.StartAsync(tokenSource.Token));

        try
        {
            using McpTcpClient client = new McpTcpClient();
            await ConnectWithRetryAsync(() => client.ConnectAsync("127.0.0.1", port)).ConfigureAwait(false);
            await body((method, parameters) => client.CallAsync<JsonElement>(method, parameters)).ConfigureAwait(false);
        }
        finally
        {
            server.Stop();
            tokenSource.Cancel();
            await SwallowAsync(serverTask).ConfigureAwait(false);
        }
    }

    private static async Task WithWebSocketAsync(Func<McpCall, Task> body) => await WithWebSocketAsync(McpTestTools.Build(), body).ConfigureAwait(false);

    private static async Task WithWebSocketAsync(List<RestMcpToolDefinition> tools, Func<McpCall, Task> body)
    {
        int port = ReserveLoopbackPort();
        using McpWebsocketsServer server = RestMcpTransportFactory.CreateWebSocketServer("localhost", port, null, null);
        RestMcpToolRegistrar.Register(server, tools);

        using CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task serverTask = Task.Run(() => server.StartAsync(tokenSource.Token));

        try
        {
            using McpWebsocketsClient client = new McpWebsocketsClient();
            await ConnectWithRetryAsync(() => client.ConnectAsync("ws://localhost:" + port + RestMcpTransportFactory.McpPath)).ConfigureAwait(false);
            await body((method, parameters) => client.CallAsync<JsonElement>(method, parameters)).ConfigureAwait(false);
        }
        finally
        {
            server.Stop();
            tokenSource.Cancel();
            await SwallowAsync(serverTask).ConfigureAwait(false);
        }
    }

    private static async Task ConnectWithRetryAsync(Func<Task<bool>> connect)
    {
        DateTime timeout = DateTime.UtcNow.AddSeconds(10);
        Exception? last = null;

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                if (await connect().ConfigureAwait(false)) return;
            }
            catch (Exception ex)
            {
                last = ex;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Timed out connecting to the MCP transport under test.", last);
    }

    private static async Task SwallowAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static int ReserveLoopbackPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
