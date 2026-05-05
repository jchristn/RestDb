namespace RestDb.Test.Shared;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RestDb.McpServer.Classes;
using Voltaic;

internal static class McpBridgeAssertions
{
    public static async Task StreamableHttpAcceptsStandardJsonContentTypeAndListsToolsAsync()
    {
        await using McpBridgeTestSession session = await McpBridgeTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage initializeResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{" +
            "\"jsonrpc\":\"2.0\"," +
            "\"id\":0," +
            "\"method\":\"initialize\"," +
            "\"params\":{" +
            "\"protocolVersion\":\"2025-06-18\"," +
            "\"capabilities\":{\"elicitation\":{\"form\":{}}}," +
            "\"clientInfo\":{\"name\":\"codex-mcp-client\",\"title\":\"Codex\",\"version\":\"0.125.0\"}" +
            "}" +
            "}").ConfigureAwait(false);

        string initializeBody = await initializeResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(initializeResponse, HttpStatusCode.OK, initializeBody);
        TestAssert.Contains("application/json", initializeResponse.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase, initializeBody);
        TestAssert.Contains("\"result\":", initializeBody, StringComparison.Ordinal, initializeBody);

        string sessionId = GetSessionId(initializeResponse);
        TestAssert.False(string.IsNullOrWhiteSpace(sessionId), "Expected Mcp-Session-Id header on initialize response.");

        using HttpResponseMessage initializedResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            sessionId).ConfigureAwait(false);

        string initializedBody = await initializedResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(initializedResponse, HttpStatusCode.Accepted, initializedBody);
        TestAssert.True(string.IsNullOrEmpty(initializedBody), "Expected empty body for streamable HTTP notification response.");

        using HttpResponseMessage toolsResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}",
            sessionId).ConfigureAwait(false);

        string toolsBody = await toolsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(toolsResponse, HttpStatusCode.OK, toolsBody);
        using JsonDocument json = JsonDocument.Parse(toolsBody);
        JsonElement result = RequireProperty(json.RootElement, "result", toolsBody);
        JsonElement tools = RequireProperty(result, "tools", toolsBody);
        TestAssert.True(tools.GetArrayLength() > 0, toolsBody);
    }

    public static async Task StreamableHttpSendsImmediateSsePreludeAsync()
    {
        await using McpBridgeTestSession session = await McpBridgeTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage initializeResponse = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{" +
            "\"jsonrpc\":\"2.0\"," +
            "\"id\":0," +
            "\"method\":\"initialize\"," +
            "\"params\":{" +
            "\"protocolVersion\":\"2025-06-18\"," +
            "\"capabilities\":{\"elicitation\":{\"form\":{}}}," +
            "\"clientInfo\":{\"name\":\"codex-mcp-client\",\"title\":\"Codex\",\"version\":\"0.125.0\"}" +
            "}" +
            "}").ConfigureAwait(false);

        string sessionId = GetSessionId(initializeResponse);
        TestAssert.False(string.IsNullOrWhiteSpace(sessionId), "Expected Mcp-Session-Id header on initialize response.");

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK);
        TestAssert.Contains("text/event-stream", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase, response.Content.Headers.ToString());

        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] buffer = new byte[64];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), tokenSource.Token).ConfigureAwait(false);
        TestAssert.True(bytesRead > 0, "Expected immediate SSE prelude bytes.");

        string prelude = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        TestAssert.Contains(": connected", prelude, StringComparison.Ordinal, prelude);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, string json, string? sessionId = null)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, path);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        }

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string GetSessionId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var values))
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static JsonElement RequireProperty(JsonElement element, string propertyName, string body)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidOperationException("Expected JSON property '" + propertyName + "'." + Environment.NewLine + body);
        }

        return value;
    }

    private static void AssertStatus(HttpResponseMessage response, HttpStatusCode expected, string? body = null)
    {
        TestAssert.Equal(expected, response.StatusCode, body ?? ("Expected " + expected + " but found " + response.StatusCode + "."));
    }

    private sealed class McpBridgeTestSession : IAsyncDisposable
    {
        private readonly CancellationTokenSource _TokenSource;
        private readonly Task _InnerServerTask;
        private readonly Task _BridgeTask;

        private McpBridgeTestSession(
            int bridgePort,
            McpHttpServer innerServer,
            RestMcpHttpBridge bridge,
            CancellationTokenSource tokenSource,
            Task innerServerTask,
            Task bridgeTask)
        {
            BridgePort = bridgePort;
            InnerServer = innerServer;
            Bridge = bridge;
            _TokenSource = tokenSource;
            _InnerServerTask = innerServerTask;
            _BridgeTask = bridgeTask;
        }

        internal int BridgePort { get; }

        internal McpHttpServer InnerServer { get; }

        internal RestMcpHttpBridge Bridge { get; }

        internal static async Task<McpBridgeTestSession> StartAsync()
        {
            int innerPort = ReserveLoopbackPort();
            int bridgePort = ReserveLoopbackPort();
            CancellationTokenSource tokenSource = new CancellationTokenSource();

            McpHttpServer innerServer = new McpHttpServer("127.0.0.1", innerPort, includeDefaultMethods: true, mcpPath: null)
            {
                ServerName = "RestDb.McpServer.Tests",
                ServerVersion = "2.0.7"
            };

            RestMcpHttpBridge bridge = new RestMcpHttpBridge("127.0.0.1", bridgePort, "http://127.0.0.1:" + innerPort, innerServer);

            Task innerTask = Task.Run(() => innerServer.StartAsync(tokenSource.Token));
            Task bridgeTask = Task.Run(() => bridge.StartAsync(tokenSource.Token));

            McpBridgeTestSession session = new McpBridgeTestSession(
                bridgePort,
                innerServer,
                bridge,
                tokenSource,
                innerTask,
                bridgeTask);

            await session.WaitForBridgeAsync().ConfigureAwait(false);
            return session;
        }

        internal HttpClient CreateClient()
        {
            return new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:" + BridgePort),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public async ValueTask DisposeAsync()
        {
            Bridge.Stop();
            InnerServer.Stop();
            _TokenSource.Cancel();

            try
            {
                await Task.WhenAll(_InnerServerTask, _BridgeTask).ConfigureAwait(false);
            }
            catch
            {
            }

            Bridge.Dispose();
            InnerServer.Dispose();
            _TokenSource.Dispose();
        }

        private async Task WaitForBridgeAsync()
        {
            using HttpClient client = CreateClient();
            DateTime timeout = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync("/").ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch
                {
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for MCP bridge test session to become ready.");
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
}
