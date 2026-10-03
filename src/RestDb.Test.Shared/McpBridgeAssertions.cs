namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RestDb.McpServer.Classes;
using Voltaic.Mcp;

internal static class McpBridgeAssertions
{
    internal const string StatelessProtocolVersion = "2026-07-28";
    internal const string NewestHandshakeProtocolVersion = "2025-11-25";
    internal const int ParseErrorCode = -32700;
    internal const int InvalidParamsCode = -32602;
    internal const int InvalidRequestCode = -32600;
    internal const int MethodNotFoundCode = -32601;

    public static async Task StreamableHttpAcceptsStandardJsonContentTypeAndListsToolsAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
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
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
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

        // Before 2025-11-25 the prelude is an SSE comment line; from 2025-11-25 on it is a priming event (an id, a retry
        // hint, and empty data). Either way the client sees bytes as soon as the stream opens.
        string prelude = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        TestAssert.True(prelude.StartsWith(":", StringComparison.Ordinal) || prelude.StartsWith("id:", StringComparison.Ordinal), "Expected an SSE comment or priming event. " + prelude);
    }

    public static async Task SseRelaysServerNotificationsAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);

        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK);

        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        StringBuilder received = new StringBuilder();
        byte[] buffer = new byte[4096];

        // Keep re-sending until the relay from the inner stream is attached and the notification arrives.
        Task sender = Task.Run(async () =>
        {
            while (!tokenSource.IsCancellationRequested)
            {
                session.Server.SendNotificationToSession(sessionId, "notifications/message", new { level = "info", data = "bridge-sse-check" });
                await Task.Delay(250, tokenSource.Token).ConfigureAwait(false);
            }
        });

        try
        {
            while (!received.ToString().Contains("bridge-sse-check", StringComparison.Ordinal))
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), tokenSource.Token).ConfigureAwait(false);
                if (bytesRead <= 0) break;
                received.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            tokenSource.Cancel();
            try { await sender.ConfigureAwait(false); } catch { }
        }

        TestAssert.Contains("notifications/message", received.ToString(), StringComparison.Ordinal, "Expected a relayed notification on the SSE stream." + Environment.NewLine + received);
        TestAssert.Contains("bridge-sse-check", received.ToString(), StringComparison.Ordinal, received.ToString());
    }

    public static async Task StatelessToolsListCarriesResultTypeAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendStatelessAsync(client, "tools/list", null, "{}").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);
        TestAssert.False(response.Headers.Contains("Mcp-Session-Id"), "Stateless requests must not create an MCP session." + Environment.NewLine + body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Equal("complete", RequireProperty(result, "resultType", body).GetString(), body);
        TestAssert.True(result.TryGetProperty("ttlMs", out _), "Expected ttlMs on a stateless list result." + Environment.NewLine + body);
        TestAssert.True(result.TryGetProperty("cacheScope", out _), "Expected cacheScope on a stateless list result." + Environment.NewLine + body);
        AssertToolListed(RequireProperty(result, "tools", body), McpTestTools.EchoToolName, body);
    }

    public static async Task StatelessServerDiscoverAdvertisesStatelessRevisionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendStatelessAsync(client, "server/discover", null, "{}").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Equal("complete", RequireProperty(result, "resultType", body).GetString(), body);

        bool found = false;
        foreach (JsonElement version in RequireProperty(result, "supportedVersions", body).EnumerateArray())
        {
            if (version.GetString() == StatelessProtocolVersion) found = true;
        }

        TestAssert.True(found, "Expected server/discover to advertise " + StatelessProtocolVersion + "." + Environment.NewLine + body);
    }

    public static async Task StatelessToolsCallInvokesRegisteredToolAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendStatelessAsync(
            client,
            "tools/call",
            McpTestTools.EchoToolName,
            "{\"name\":\"" + McpTestTools.EchoToolName + "\",\"arguments\":{\"message\":\"stateless-hello\"}}").ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Equal("complete", RequireProperty(result, "resultType", body).GetString(), body);
        TestAssert.Contains("stateless-hello", RequireProperty(result, "content", body).GetRawText(), StringComparison.Ordinal, body);
    }

    public static async Task HandshakeToolsCallInvokesRegisteredToolAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"" + McpTestTools.EchoToolName + "\",\"arguments\":{\"message\":\"session-hello\"}}}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Contains("session-hello", RequireProperty(result, "content", body).GetRawText(), StringComparison.Ordinal, body);
        TestAssert.False(result.TryGetProperty("resultType", out _), "Handshake-era results must not carry resultType." + Environment.NewLine + body);
    }

    public static async Task InitializeNegotiatesNewestHandshakeRevisionForStatelessVersionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendJsonAsync(client, HttpMethod.Post, "/mcp", BuildInitializeBody(StatelessProtocolVersion)).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Equal(NewestHandshakeProtocolVersion, RequireProperty(result, "protocolVersion", body).GetString(), body);
    }

    public static async Task DeleteTerminatesSessionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);
        TestAssert.True(session.Server.GetActiveSessions().Contains(sessionId), "Expected the initialized session to be active.");

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, "/mcp");
        request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

        TestAssert.True(response.IsSuccessStatusCode, "Expected DELETE /mcp to succeed but found " + response.StatusCode + ".");
        TestAssert.False(session.Server.GetActiveSessions().Contains(sessionId), "Expected DELETE /mcp to remove the session.");

        using HttpRequestMessage sseRequest = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        sseRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        sseRequest.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        using HttpResponseMessage sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        AssertStatus(sseResponse, HttpStatusCode.NotFound, "A terminated session must be rejected with 404 but found " + sseResponse.StatusCode + ".");
    }

    public static async Task PostRequiresInitializedSessionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        const string toolsList = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}";

        // Only a successful initialize creates a session (Streamable HTTP): no session is 400, an unknown one is 404.
        using HttpResponseMessage sessionless = await SendJsonAsync(client, HttpMethod.Post, "/mcp", toolsList).ConfigureAwait(false);
        string sessionlessBody = await sessionless.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(sessionless, HttpStatusCode.BadRequest, sessionlessBody);
        AssertJsonRpcError(sessionlessBody, InvalidRequestCode, null);
        TestAssert.False(sessionless.Headers.Contains("Mcp-Session-Id"), "A rejected request must not be issued a session.");

        using HttpResponseMessage unknown = await SendJsonAsync(client, HttpMethod.Post, "/mcp", toolsList, "not-a-real-session").ConfigureAwait(false);
        string unknownBody = await unknown.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(unknown, HttpStatusCode.NotFound, unknownBody);
        TestAssert.DoesNotContain("\"tools\"", unknownBody, StringComparison.Ordinal, "An unknown session must not be served. " + unknownBody);

        TestAssert.Empty(session.Server.GetActiveSessions(), "Rejected requests must not create or adopt sessions.");
    }

    public static async Task ToolsCallRejectsUnknownToolAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendStatelessAsync(
            client,
            "tools/call",
            "no_such_tool",
            "{\"name\":\"no_such_tool\",\"arguments\":{}}").ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertJsonRpcError(body, InvalidParamsCode, "no_such_tool");
    }

    public static async Task ToolsCallFlagsMissingRequiredArgumentAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"" + McpTestTools.EchoToolName + "\",\"arguments\":{}}}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertToolInputError(body, "message");
    }

    public static async Task ToolsCallSurfacesHandlerFailureAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"" + McpTestTools.FailToolName + "\",\"arguments\":{}}}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertToolFailure(body);
    }

    public static async Task ToolsCallFlagsFailedDownstreamResponseAsErrorAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendStatelessAsync(
            client,
            "tools/call",
            McpTestTools.DownstreamNotFoundToolName,
            "{\"name\":\"" + McpTestTools.DownstreamNotFoundToolName + "\",\"arguments\":{}}").ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        AssertDownstreamToolResult(result, expectError: true, "downstream-not-found", body);
        TestAssert.Equal("complete", RequireProperty(result, "resultType", body).GetString(), body);
    }

    public static async Task ToolsCallLeavesSuccessfulDownstreamResponseUnflaggedAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"" + McpTestTools.DownstreamOkToolName + "\",\"arguments\":{}}}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        AssertDownstreamToolResult(RequireProperty(json.RootElement, "result", body), expectError: false, "downstream-ok", body);
    }

    internal static void AssertDownstreamToolResult(JsonElement result, bool expectError, string expectedBodyText, string body)
    {
        bool isError = result.TryGetProperty("isError", out JsonElement flag) && flag.ValueKind == JsonValueKind.True;
        TestAssert.Equal(expectError, isError, "Unexpected isError value." + Environment.NewLine + body);

        string content = RequireProperty(result, "content", body).GetRawText();
        TestAssert.Contains(expectedBodyText, content, StringComparison.Ordinal, body);
        TestAssert.Contains(expectError ? "404" : "200", content, StringComparison.Ordinal, "Expected the downstream status code in the tool content." + Environment.NewLine + body);
    }

    public static async Task StatelessRequestRejectsMismatchedMethodHeaderAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpRequestMessage request = CreateStatelessRequest(
            "tools/list",
            null,
            "tools/call",
            "{\"name\":\"" + McpTestTools.EchoToolName + "\",\"arguments\":{\"message\":\"x\"}}");
        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.BadRequest, body);
        TestAssert.Contains("\"error\"", body, StringComparison.Ordinal, body);
        TestAssert.DoesNotContain("\"result\"", body, StringComparison.Ordinal, body);
    }

    public static async Task InitializeNegotiatesNewestRevisionForUnknownVersionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendJsonAsync(client, HttpMethod.Post, "/mcp", BuildInitializeBody("1999-01-01")).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Equal(NewestHandshakeProtocolVersion, RequireProperty(result, "protocolVersion", body).GetString(), body);
    }

    public static async Task MalformedJsonReturnsParseErrorAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(client, HttpMethod.Post, "/mcp", "{not json", sessionId).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertJsonRpcError(body, ParseErrorCode, null);
    }

    public static async Task SseRejectsMissingOrUnknownSessionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        // A missing session is a bad request (400); an unknown one is not found (404) so clients re-initialize.
        foreach ((string? sessionId, HttpStatusCode expected) in new[] { ((string?)null, HttpStatusCode.BadRequest), ("not-a-real-session", HttpStatusCode.NotFound) })
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (sessionId != null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            AssertStatus(response, expected, "Expected " + expected + " for GET /mcp with session '" + (sessionId ?? "(none)") + "' but found " + response.StatusCode + ".");
        }
    }

    public static async Task DeleteRejectsMissingOrUnknownSessionAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpRequestMessage missing = new HttpRequestMessage(HttpMethod.Delete, "/mcp");
        using HttpResponseMessage missingResponse = await client.SendAsync(missing).ConfigureAwait(false);
        AssertStatus(missingResponse, HttpStatusCode.BadRequest);

        using HttpRequestMessage unknown = new HttpRequestMessage(HttpMethod.Delete, "/mcp");
        unknown.Headers.TryAddWithoutValidation("Mcp-Session-Id", "not-a-real-session");
        using HttpResponseMessage unknownResponse = await client.SendAsync(unknown).ConfigureAwait(false);
        AssertStatus(unknownResponse, HttpStatusCode.NotFound);
    }

    public static async Task UnsupportedHttpMethodReturnsMethodNotAllowedAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendJsonAsync(client, HttpMethod.Put, "/mcp", "{}").ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.MethodNotAllowed);
    }

    public static async Task UnknownPathReturnsNotFoundAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/not-mcp").ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.NotFound);
    }

    public static async Task HandshakeToolsListPublishesOnlyRestDbToolsAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        AssertToolNamesExactly(RequireProperty(result, "tools", body), McpTestTools.Names(), body);
    }

    public static async Task StatelessToolsListPublishesOnlyRestDbToolsAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendStatelessAsync(client, "tools/list", null, "{}").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        AssertToolNamesExactly(RequireProperty(result, "tools", body), McpTestTools.Names(), body);
    }

    public static async Task HandshakePingReturnsEmptyResultAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\"}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        AssertEmptyObject(RequireProperty(json.RootElement, "result", body), body);
    }

    public static async Task StatelessPingIsNotAMethodAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        // The 2026-07-28 revision removed ping; stateless clients get method not found (404, -32601).
        using HttpResponseMessage response = await SendStatelessAsync(client, "ping", null, "{}").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.NotFound, body);
        AssertJsonRpcError(body, MethodNotFoundCode, "ping");
    }

    public static async Task ToolsCallRejectsRemovedVoltaicDemoToolsAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        foreach (string toolName in RemovedVoltaicDemoToolNames)
        {
            using HttpResponseMessage response = await SendJsonAsync(
                client,
                HttpMethod.Post,
                "/mcp",
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":{}}}",
                sessionId).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            AssertJsonRpcError(body, InvalidParamsCode, toolName);
        }
    }

    public static async Task BareVoltaicDemoMethodsReturnMethodNotFoundAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        foreach (string method in RemovedVoltaicDemoToolNames)
        {
            // "ping" is the MCP protocol method, so it stays callable; the demo names must not.
            if (method == "ping") continue;

            using HttpResponseMessage response = await SendJsonAsync(
                client,
                HttpMethod.Post,
                "/mcp",
                "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"" + method + "\",\"params\":{}}",
                sessionId).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            AssertJsonRpcError(body, MethodNotFoundCode, null);
        }
    }

    public static async Task RestDbToolRemainsCallableAsDirectMethodAsync()
    {
        await using McpHttpTestSession session = await McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();
        string sessionId = await InitializeSessionAsync(client).ConfigureAwait(false);

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"" + McpTestTools.EchoToolName + "\",\"params\":{\"message\":\"direct-http\"}}",
            sessionId).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement result = RequireProperty(json.RootElement, "result", body);
        TestAssert.Equal("direct-http", RequireProperty(result, "echoed", body).GetString(), body);
    }

    /// <summary>
    /// Tools Voltaic 1.x published on every MCP server. Voltaic 2.x publishes none of them by default.
    /// </summary>
    internal static readonly string[] RemovedVoltaicDemoToolNames = { "ping", "echo", "getTime", "getSessions", "getClients" };

    internal static void AssertToolNamesExactly(JsonElement tools, IEnumerable<string> expected, string body)
    {
        List<string> actual = new List<string>();
        foreach (JsonElement tool in tools.EnumerateArray())
        {
            actual.Add(RequireProperty(tool, "name", body).GetString() ?? string.Empty);
        }

        foreach (string demo in RemovedVoltaicDemoToolNames)
        {
            TestAssert.False(actual.Contains(demo), "Voltaic demo tool '" + demo + "' must not be published." + Environment.NewLine + body);
        }

        List<string> expectedSorted = expected.OrderBy(name => name, StringComparer.Ordinal).ToList();
        List<string> actualSorted = actual.OrderBy(name => name, StringComparer.Ordinal).ToList();
        TestAssert.Equal(
            string.Join(",", expectedSorted),
            string.Join(",", actualSorted),
            "Expected tools/list to contain exactly the registered RestDb tools." + Environment.NewLine + body);
    }

    internal static void AssertEmptyObject(JsonElement result, string body)
    {
        TestAssert.Equal(JsonValueKind.Object, result.ValueKind, "Expected an empty object result, not \"pong\"." + Environment.NewLine + body);
        foreach (JsonProperty property in result.EnumerateObject())
        {
            throw new InvalidOperationException("Expected an empty object result but found property '" + property.Name + "'." + Environment.NewLine + body);
        }
    }

    internal static void AssertToolFailure(string body)
    {
        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;

        if (root.TryGetProperty("error", out JsonElement error))
        {
            TestAssert.True(error.TryGetProperty("code", out _), body);
            return;
        }

        JsonElement result = RequireProperty(root, "result", body);
        TestAssert.True(
            result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True,
            "Expected a JSON-RPC error or an isError tool result for a failing tool." + Environment.NewLine + body);
    }

    /// <summary>
    /// Asserts that invalid tool arguments came back as a tool result with isError: true (so the model can correct
    /// the call) rather than a JSON-RPC error, and that the text names the offending property.
    /// </summary>
    internal static void AssertToolInputError(string body, string expectedTextFragment)
    {
        using JsonDocument json = JsonDocument.Parse(body);
        TestAssert.False(json.RootElement.TryGetProperty("error", out _), "Invalid arguments must be a tool result, not a JSON-RPC error." + Environment.NewLine + body);
        AssertToolInputErrorResult(RequireProperty(json.RootElement, "result", body), expectedTextFragment, body);
    }

    internal static void AssertToolInputErrorResult(JsonElement result, string expectedTextFragment, string body)
    {
        TestAssert.True(
            result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True,
            "Expected isError: true for invalid tool arguments." + Environment.NewLine + body);
        TestAssert.Contains(expectedTextFragment, RequireProperty(result, "content", body).GetRawText(), StringComparison.Ordinal, body);
    }

    private static void AssertJsonRpcError(string body, int expectedCode, string? expectedMessageFragment)
    {
        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement error = RequireProperty(json.RootElement, "error", body);
        TestAssert.Equal(expectedCode, RequireProperty(error, "code", body).GetInt32(), body);
        TestAssert.False(json.RootElement.TryGetProperty("result", out _), "Error responses must not carry a result." + Environment.NewLine + body);

        if (expectedMessageFragment != null)
        {
            TestAssert.Contains(expectedMessageFragment, RequireProperty(error, "message", body).GetString(), StringComparison.Ordinal, body);
        }
    }

    private static void AssertToolListed(JsonElement tools, string toolName, string body)
    {
        foreach (JsonElement tool in tools.EnumerateArray())
        {
            if (tool.TryGetProperty("name", out JsonElement name) && name.GetString() == toolName) return;
        }

        throw new InvalidOperationException("Expected tool '" + toolName + "' in tools/list." + Environment.NewLine + body);
    }

    private static async Task<string> InitializeSessionAsync(HttpClient client)
    {
        using HttpResponseMessage response = await SendJsonAsync(client, HttpMethod.Post, "/mcp", BuildInitializeBody(NewestHandshakeProtocolVersion)).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);

        string sessionId = GetSessionId(response);
        TestAssert.False(string.IsNullOrWhiteSpace(sessionId), "Expected Mcp-Session-Id header on initialize response.");

        using HttpResponseMessage initialized = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/mcp",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            sessionId).ConfigureAwait(false);
        AssertStatus(initialized, HttpStatusCode.Accepted);

        return sessionId;
    }

    private static string BuildInitializeBody(string protocolVersion)
    {
        return "{" +
            "\"jsonrpc\":\"2.0\"," +
            "\"id\":0," +
            "\"method\":\"initialize\"," +
            "\"params\":{" +
            "\"protocolVersion\":\"" + protocolVersion + "\"," +
            "\"capabilities\":{}," +
            "\"clientInfo\":{\"name\":\"restdb-tests\",\"version\":\"1.0.0\"}" +
            "}" +
            "}";
    }

    private static async Task<HttpResponseMessage> SendStatelessAsync(HttpClient client, string method, string? name, string paramsJson)
    {
        using HttpRequestMessage request = CreateStatelessRequest(method, name, method, paramsJson);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateStatelessRequest(string headerMethod, string? name, string bodyMethod, string paramsJson)
    {
        // Mirrors the request shape Claude Code 2.1.x sends under the stateless 2026-07-28 revision.
        string meta = "\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"" + StatelessProtocolVersion + "\",\"io.modelcontextprotocol/clientCapabilities\":{}}";
        string trimmed = paramsJson.Trim();
        string paramsWithMeta = trimmed == "{}"
            ? "{" + meta + "}"
            : trimmed.Substring(0, trimmed.Length - 1) + "," + meta + "}";

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", StatelessProtocolVersion);
        request.Headers.TryAddWithoutValidation("Mcp-Method", headerMethod);
        if (!string.IsNullOrWhiteSpace(name)) request.Headers.TryAddWithoutValidation("Mcp-Name", name);

        request.Content = new StringContent(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + bodyMethod + "\",\"params\":" + paramsWithMeta + "}",
            Encoding.UTF8,
            "application/json");
        return request;
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

    internal static JsonElement RequireProperty(JsonElement element, string propertyName, string body)
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

    internal sealed class McpHttpTestSession : IAsyncDisposable
    {
        private readonly CancellationTokenSource _TokenSource;
        private readonly Task _ServerTask;

        private McpHttpTestSession(int port, McpHttpServer server, CancellationTokenSource tokenSource, Task serverTask)
        {
            Port = port;
            Server = server;
            _TokenSource = tokenSource;
            _ServerTask = serverTask;
        }

        internal int Port { get; }

        internal McpHttpServer Server { get; }

        /// <summary>
        /// Starts an HTTP MCP server built by the production factory, bound to "localhost" like the default settings.
        /// </summary>
        internal static async Task<McpHttpTestSession> StartAsync(IEnumerable<string>? allowedOrigins = null, string? mcpToken = null)
        {
            int port = ReserveLoopbackPort();
            CancellationTokenSource tokenSource = new CancellationTokenSource();

            McpHttpServer server = RestMcpTransportFactory.CreateHttpServer("localhost", port, allowedOrigins, mcpToken);
            RestMcpToolRegistrar.Register(server, McpTestTools.Build());

            Task serverTask = Task.Run(() => server.StartAsync(tokenSource.Token));

            McpHttpTestSession session = new McpHttpTestSession(port, server, tokenSource, serverTask);
            await session.WaitForServerAsync().ConfigureAwait(false);
            return session;
        }

        internal HttpClient CreateClient()
        {
            return new HttpClient
            {
                BaseAddress = new Uri("http://localhost:" + Port),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public async ValueTask DisposeAsync()
        {
            Server.Stop();
            _TokenSource.Cancel();

            try
            {
                await _ServerTask.ConfigureAwait(false);
            }
            catch
            {
            }

            Server.Dispose();
            _TokenSource.Dispose();
        }

        private async Task WaitForServerAsync()
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

            throw new InvalidOperationException("Timed out waiting for the MCP HTTP test server to become ready.");
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
