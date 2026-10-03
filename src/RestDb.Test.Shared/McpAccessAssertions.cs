namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RestDb.McpServer.Classes;
using Voltaic.Mcp;

/// <summary>
/// Access controls on the MCP network servers built by <see cref="RestMcpTransportFactory"/>: browser Origin
/// validation, loopback-only clients, and the optional bearer token on HTTP and WebSocket (enforced by Voltaic
/// with RestDb's configuration), plus strict framing on TCP. TCP and stdio are intentionally unauthenticated.
/// </summary>
internal static class McpAccessAssertions
{
    private const string Token = "s3cret-token";
    private const string EvilOrigin = "https://evil.example";
    private const string LoopbackOrigin = "http://localhost:5173";
    private const string ConfiguredOrigin = "https://app.example";

    // ---- Configuration -------------------------------------------------------------------------------------

    public static void FactoryAppliesOriginPolicy()
    {
        using McpHttpServer http = RestMcpTransportFactory.CreateHttpServer("localhost", 1, new[] { ConfiguredOrigin + "/", " https://other.example " }, null);
        using McpWebsocketsServer ws = RestMcpTransportFactory.CreateWebSocketServer("localhost", 1, new[] { ConfiguredOrigin + "/", " https://other.example " }, null);

        foreach (Voltaic.Core.OriginPolicy policy in new[] { http.OriginPolicy, ws.OriginPolicy })
        {
            // No Origin header means a non-browser client. An empty Origin value is not a valid browser origin.
            foreach (string? origin in new[] { null, "http://localhost", LoopbackOrigin, "http://127.0.0.1:3000", "http://[::1]:8080", ConfiguredOrigin, "HTTPS://APP.EXAMPLE", "https://other.example" })
            {
                TestAssert.True(policy.IsAllowed(origin), "Expected origin '" + origin + "' to be allowed.");
            }

            foreach (string origin in new[] { "", EvilOrigin, "null", "http://localhost.evil.example", "http://127.0.0.1.evil.example", "https://app.example:8443", "http://app.example", "file://", "chrome-extension://abcdef" })
            {
                TestAssert.False(policy.IsAllowed(origin), "Expected origin '" + origin + "' to be rejected.");
            }
        }

        using McpHttpServer wildcard = RestMcpTransportFactory.CreateHttpServer("localhost", 1, new[] { "*" }, null);
        TestAssert.True(wildcard.OriginPolicy.IsAllowed(EvilOrigin), "* should allow any origin.");
    }

    public static void FactoryConfiguresAuthenticationAndLoopbackClients()
    {
        using McpHttpServer openHttp = RestMcpTransportFactory.CreateHttpServer("localhost", 1, null, null);
        using McpWebsocketsServer openWs = RestMcpTransportFactory.CreateWebSocketServer("localhost", 1, null, "   ");
        TestAssert.Null(openHttp.AuthenticationHandler, "No token: HTTP must not require authentication.");
        TestAssert.Null(openWs.AuthenticationHandler, "A blank token must not enable authentication.");
        TestAssert.True(openHttp.RestrictToLoopbackClients, "A localhost HTTP server must serve loopback clients only.");
        TestAssert.True(openWs.RestrictToLoopbackClients, "A localhost WebSocket server must serve loopback clients only.");

        using McpHttpServer tokenHttp = RestMcpTransportFactory.CreateHttpServer("localhost", 1, null, Token);
        using McpWebsocketsServer tokenWs = RestMcpTransportFactory.CreateWebSocketServer("localhost", 1, null, Token);
        TestAssert.NotNull(tokenHttp.AuthenticationHandler, "Token: HTTP must require authentication.");
        TestAssert.NotNull(tokenWs.AuthenticationHandler, "Token: WebSocket must require authentication.");

        using McpHttpServer allInterfaces = RestMcpTransportFactory.CreateHttpServer("+", 1, null, null);
        TestAssert.False(allInterfaces.RestrictToLoopbackClients, "A server bound to all interfaces must accept remote clients.");
    }

    public static void TokenCheckAcceptsOnlyExactBearerToken()
    {
        byte[] expected = Encoding.UTF8.GetBytes(Token);
        TestAssert.True(RestMcpTransportFactory.IsAuthorized("Bearer " + Token, expected), "Correct token.");
        TestAssert.True(RestMcpTransportFactory.IsAuthorized("bearer " + Token, expected), "Scheme is case-insensitive.");

        foreach (string? header in new[] { null, "", "Bearer", "Bearer ", "Bearer wrong", "Bearer " + Token + "x", "Bearer " + Token.ToUpperInvariant(), "Basic " + Token, Token })
        {
            TestAssert.False(RestMcpTransportFactory.IsAuthorized(header, expected), "Expected Authorization '" + header + "' to be rejected.");
        }
    }

    // ---- HTTP ----------------------------------------------------------------------------------------------

    public static async Task HttpAllowsLoopbackOriginAndEchoesItAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, InitializeBody(), LoopbackOrigin, null).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        TestAssert.Equal(HttpStatusCode.OK, response.StatusCode, body);
        TestAssert.Equal(LoopbackOrigin, GetHeader(response, "Access-Control-Allow-Origin"), "Expected the allowed origin to be echoed.");
        TestAssert.Contains("Origin", GetHeader(response, "Vary"), StringComparison.OrdinalIgnoreCase, "Expected Vary: Origin.");
        TestAssert.Contains("\"protocolVersion\"", body, StringComparison.Ordinal, body);
    }

    public static async Task HttpAllowsConfiguredOriginAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync(new[] { ConfiguredOrigin }).ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, InitializeBody(), ConfiguredOrigin, null).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        TestAssert.Equal(ConfiguredOrigin, GetHeader(response, "Access-Control-Allow-Origin"), "Expected the configured origin to be echoed.");
    }

    public static async Task HttpPreflightFromLoopbackOriginAllowsMcpHeadersAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync(null, Token).ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        // Browsers never send Authorization on a preflight, so preflight must not require the token.
        using HttpResponseMessage response = await SendPreflightAsync(client, LoopbackOrigin).ConfigureAwait(false);

        TestAssert.Equal(HttpStatusCode.NoContent, response.StatusCode, "Expected 204 for an allowed preflight.");
        TestAssert.Equal(LoopbackOrigin, GetHeader(response, "Access-Control-Allow-Origin"), "Expected the allowed origin to be echoed.");

        string allowHeaders = GetHeader(response, "Access-Control-Allow-Headers") ?? string.Empty;
        foreach (string header in new[] { "Authorization", "Content-Type", "Mcp-Session-Id", "MCP-Protocol-Version" })
        {
            TestAssert.Contains(header, allowHeaders, StringComparison.OrdinalIgnoreCase, "Expected " + header + " in Access-Control-Allow-Headers: " + allowHeaders);
        }

        TestAssert.Contains("Mcp-Session-Id", GetHeader(response, "Access-Control-Expose-Headers"), StringComparison.OrdinalIgnoreCase, "Expected Mcp-Session-Id to be exposed.");
    }

    public static async Task HttpNeverSendsWildcardCorsAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage noOrigin = await SendAsync(client, HttpMethod.Post, InitializeBody(), null, null).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.OK, noOrigin.StatusCode, "Non-browser requests without an Origin must keep working.");
        TestAssert.Null(GetHeader(noOrigin, "Access-Control-Allow-Origin"), "No CORS grant should be sent when there is no Origin.");

        using HttpResponseMessage health = await client.GetAsync("/").ConfigureAwait(false);
        TestAssert.Null(GetHeader(health, "Access-Control-Allow-Origin"), "Health responses must not grant any origin.");
    }

    public static async Task HttpRejectsDisallowedOriginAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        foreach (string origin in new[] { EvilOrigin, "null", "http://localhost.evil.example" })
        {
            using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, InitializeBody(), origin, null).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            TestAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "Expected 403 for Origin '" + origin + "'. " + body);
            TestAssert.Null(GetHeader(response, "Access-Control-Allow-Origin"), "A rejected origin must not receive a CORS grant.");
            TestAssert.DoesNotContain("protocolVersion", body, StringComparison.Ordinal, "The request must not reach the MCP handlers. " + body);
        }

        TestAssert.Empty(session.Server.GetActiveSessions(), "A rejected initialize must not create a session.");
    }

    public static async Task HttpRejectsPreflightFromDisallowedOriginAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendPreflightAsync(client, EvilOrigin).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "Expected 403 for a preflight from a disallowed origin.");
        TestAssert.Null(GetHeader(response, "Access-Control-Allow-Origin"), "A rejected preflight must not receive a CORS grant.");
        TestAssert.Null(GetHeader(response, "Access-Control-Allow-Headers"), "A rejected preflight must not list allowed headers.");
    }

    public static async Task HttpRejectsDisallowedOriginOnSseAndDeleteAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        foreach (HttpMethod method in new[] { HttpMethod.Get, HttpMethod.Delete })
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, "/mcp");
            request.Headers.TryAddWithoutValidation("Origin", EvilOrigin);
            request.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", "any");
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            TestAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "Expected 403 for " + method + " /mcp from a disallowed origin.");
        }
    }

    public static async Task HttpRejectsNonJsonBodyAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        // text/plain is a CORS "simple request" a page can send without a preflight; Streamable HTTP requires JSON.
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Content = new StringContent(InitializeBody(), Encoding.UTF8, "text/plain");
        using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

        TestAssert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        TestAssert.Empty(session.Server.GetActiveSessions(), "A rejected request must not create a session.");
    }

    public static async Task HttpAcceptsValidBearerTokenAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync(null, Token).ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, InitializeBody(), null, "Bearer " + Token).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.OK, response.StatusCode, body);

        // Voltaic's own HTTP client sends the header on every request, including its initialize handshake.
        using McpHttpClient mcpClient = new McpHttpClient();
        mcpClient.SetRequestHeader("Authorization", "Bearer " + Token);
        await mcpClient.ConnectStreamableAsync("http://localhost:" + session.Port).ConfigureAwait(false);

        JsonElement echo = await mcpClient.CallAsync<JsonElement>("tools/call", new
        {
            name = McpTestTools.EchoToolName,
            arguments = new { message = "token-hello" }
        }).ConfigureAwait(false);
        TestAssert.Contains("token-hello", echo.GetRawText(), StringComparison.Ordinal, echo.GetRawText());

        JsonElement whoami = await mcpClient.CallAsync<JsonElement>("tools/call", new { name = McpTestTools.WhoAmIToolName, arguments = new { } }).ConfigureAwait(false);
        TestAssert.Contains(RestMcpTransportFactory.TokenPrincipal, whoami.GetRawText(), StringComparison.Ordinal, "Expected the token's principal to reach the tool handler. " + whoami.GetRawText());
    }

    public static async Task HttpHealthDoesNotRequireTokenAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync(null, Token).ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/").ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.OK, response.StatusCode, "The health endpoint is used by container health checks and must not require the token.");
    }

    public static async Task HttpRejectsMissingOrInvalidBearerTokenAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync(null, Token).ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        foreach (string? authorization in new[] { null, "Bearer wrong", "Basic " + Token, Token })
        {
            using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, InitializeBody(), null, authorization).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string challenge = response.Headers.WwwAuthenticate.ToString();

            TestAssert.Equal(HttpStatusCode.Unauthorized, response.StatusCode, "Expected 401 for Authorization '" + authorization + "'. " + body);
            TestAssert.Contains("Bearer", challenge, StringComparison.OrdinalIgnoreCase, "Expected WWW-Authenticate: Bearer.");
            TestAssert.DoesNotContain("protocolVersion", body, StringComparison.Ordinal, "The request must not reach the MCP handlers. " + body);

            // RFC 6750 section 3.1: no error code when no credentials were sent; invalid_token when they were wrong.
            if (authorization == null) TestAssert.DoesNotContain("invalid_token", challenge, StringComparison.Ordinal, challenge);
            else TestAssert.Contains("error=\"invalid_token\"", challenge, StringComparison.Ordinal, challenge);
        }

        using HttpRequestMessage sse = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        sse.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        using HttpResponseMessage sseResponse = await client.SendAsync(sse, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.Unauthorized, sseResponse.StatusCode, "Expected 401 for GET /mcp without a token.");

        TestAssert.Empty(session.Server.GetActiveSessions(), "Rejected requests must not create sessions.");
    }

    public static async Task HttpRejectsValidTokenFromDisallowedOriginAsync()
    {
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync(null, Token).ConfigureAwait(false);
        using HttpClient client = session.CreateClient();

        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, InitializeBody(), EvilOrigin, "Bearer " + Token).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.Forbidden, response.StatusCode, "A valid token must not bypass the Origin check.");
    }

    public static async Task HttpLoopbackServerRejectsNonLoopbackClientsAsync()
    {
        IPAddress? lanAddress = GetNonLoopbackIPv4();
        if (lanAddress == null)
        {
            Console.WriteLine("No non-loopback IPv4 address available; skipping the remote-client check.");
            return;
        }

        // Bound to "localhost" as by default. http.sys accepts that prefix on every interface and routes by Host
        // header, so connect through a LAN address while spoofing Host: localhost.
        await using McpBridgeAssertions.McpHttpTestSession session = await McpBridgeAssertions.McpHttpTestSession.StartAsync().ConfigureAwait(false);
        using HttpClient local = session.CreateClient();

        using HttpResponseMessage fromLoopback = await SendAsync(local, HttpMethod.Post, InitializeBody(), null, null).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.OK, fromLoopback.StatusCode, "Loopback clients must keep working.");

        HttpStatusCode? status = await TrySendFromAddressAsync(lanAddress, session.Port, "localhost:" + session.Port, InitializeBody()).ConfigureAwait(false);
        TestAssert.True(
            status == null || status == HttpStatusCode.Forbidden,
            "Expected a non-loopback client to be refused (403 or no connection) but found " + status + ".");
    }

    // ---- WebSocket -----------------------------------------------------------------------------------------

    public static Task WebSocketAllowsNoOriginAndLoopbackOriginAsync() => WithWebSocketServerAsync(null, null, async port =>
    {
        foreach (string? origin in new[] { null, LoopbackOrigin })
        {
            string reply = await WebSocketCallAsync(port, origin, null, ToolsListBody()).ConfigureAwait(false);
            TestAssert.Contains(McpTestTools.EchoToolName, reply, StringComparison.Ordinal, "Expected tools/list for origin '" + origin + "'. " + reply);
        }
    });

    public static Task WebSocketAcceptsValidBearerTokenAsync() => WithWebSocketServerAsync(null, Token, async port =>
    {
        string reply = await WebSocketCallAsync(port, null, "Bearer " + Token, ToolCallBody(McpTestTools.EchoToolName, "{\"message\":\"ws-token-hello\"}")).ConfigureAwait(false);
        TestAssert.Contains("ws-token-hello", reply, StringComparison.Ordinal, reply);

        // Voltaic's own WebSocket client can now send the header on the upgrade request.
        using McpWebsocketsClient client = new McpWebsocketsClient();
        client.SetRequestHeader("Authorization", "Bearer " + Token);
        TestAssert.True(await client.ConnectAsync("ws://localhost:" + port + RestMcpTransportFactory.McpPath).ConfigureAwait(false), "Expected McpWebsocketsClient to connect with the token.");

        JsonElement whoami = await client.CallAsync<JsonElement>("tools/call", new { name = McpTestTools.WhoAmIToolName, arguments = new { } }).ConfigureAwait(false);
        TestAssert.Contains(RestMcpTransportFactory.TokenPrincipal, whoami.GetRawText(), StringComparison.Ordinal, "Expected the token's principal to reach the WebSocket tool handler. " + whoami.GetRawText());
    });

    public static Task WebSocketWithoutTokenHasAnonymousCallerAsync() => WithWebSocketServerAsync(null, null, async port =>
    {
        string reply = await WebSocketCallAsync(port, null, null, ToolCallBody(McpTestTools.WhoAmIToolName, "{}")).ConfigureAwait(false);
        TestAssert.Contains(McpTestTools.AnonymousCaller, reply, StringComparison.Ordinal, "Without a token there is no authenticated caller. " + reply);
    });

    public static Task WebSocketRejectsDisallowedOriginAsync() => WithWebSocketServerAsync(null, null, async port =>
    {
        (HttpStatusCode status, _) = await WebSocketUpgradeAsync(port, "/mcp", EvilOrigin, null).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.Forbidden, status, "Expected 403 for a WebSocket upgrade from a disallowed origin.");

        await AssertWebSocketConnectFailsAsync(port, EvilOrigin, null).ConfigureAwait(false);
    });

    public static Task WebSocketAllowsConfiguredOriginAsync() => WithWebSocketServerAsync(new[] { ConfiguredOrigin }, null, async port =>
    {
        string reply = await WebSocketCallAsync(port, ConfiguredOrigin, null, ToolsListBody()).ConfigureAwait(false);
        TestAssert.Contains(McpTestTools.EchoToolName, reply, StringComparison.Ordinal, reply);
    });

    public static Task WebSocketRejectsMissingOrInvalidBearerTokenAsync() => WithWebSocketServerAsync(null, Token, async port =>
    {
        foreach (string? authorization in new[] { null, "Bearer wrong", "Basic " + Token })
        {
            (HttpStatusCode status, string challenge) = await WebSocketUpgradeAsync(port, "/mcp", null, authorization).ConfigureAwait(false);
            TestAssert.Equal(HttpStatusCode.Unauthorized, status, "Expected 401 for a WebSocket upgrade with Authorization '" + authorization + "'.");
            TestAssert.Contains("Bearer", challenge, StringComparison.OrdinalIgnoreCase, "Expected WWW-Authenticate: Bearer on the rejected upgrade.");
        }

        await AssertWebSocketConnectFailsAsync(port, null, null).ConfigureAwait(false);

        using McpWebsocketsClient client = new McpWebsocketsClient();
        client.SetRequestHeader("Authorization", "Bearer wrong");
        TestAssert.False(await client.ConnectAsync("ws://localhost:" + port + RestMcpTransportFactory.McpPath).ConfigureAwait(false), "Expected McpWebsocketsClient to be refused with a wrong token.");

        (HttpStatusCode evil, _) = await WebSocketUpgradeAsync(port, "/mcp", EvilOrigin, "Bearer " + Token).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.Forbidden, evil, "A valid token must not bypass the Origin check.");
    });

    public static Task WebSocketLoopbackServerRejectsNonLoopbackClientsAsync() => WithWebSocketServerAsync(null, null, async port =>
    {
        IPAddress? lanAddress = GetNonLoopbackIPv4();
        if (lanAddress == null)
        {
            Console.WriteLine("No non-loopback IPv4 address available; skipping the remote-client check.");
            return;
        }

        HttpStatusCode? status = await TryWebSocketUpgradeFromAddressAsync(lanAddress, port, "localhost:" + port).ConfigureAwait(false);
        TestAssert.True(
            status == null || status == HttpStatusCode.Forbidden,
            "Expected a non-loopback WebSocket client to be refused (403 or no connection) but found " + status + ".");
    });

    public static Task WebSocketRejectsWrongPathAndPlainRequestsAsync() => WithWebSocketServerAsync(null, null, async port =>
    {
        (HttpStatusCode wrongPath, _) = await WebSocketUpgradeAsync(port, "/not-mcp", null, null).ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.NotFound, wrongPath, "Expected 404 for an upgrade on the wrong path.");

        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using HttpResponseMessage plain = await client.GetAsync("http://localhost:" + port + "/mcp").ConfigureAwait(false);
        TestAssert.Equal(HttpStatusCode.BadRequest, plain.StatusCode, "Expected 400 for a non-upgrade request.");
    });

    // ---- TCP -----------------------------------------------------------------------------------------------

    public static Task TcpRejectsBrowserStyleHttpRequestAsync() => WithTcpServerAsync(async (port, requestCount) =>
    {
        // What a page could send with fetch(): an HTTP POST whose Content-Length header matches a JSON-RPC body.
        string json = ToolCallBody(McpTestTools.EchoToolName, "{\"message\":\"x\"}");
        string request =
            "POST / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:" + port + "\r\n" +
            "Origin: " + EvilOrigin + "\r\n" +
            "Content-Type: text/plain\r\n" +
            "Content-Length: " + Encoding.UTF8.GetByteCount(json) + "\r\n\r\n" +
            json;

        string reply = await RawTcpExchangeAsync(port, request).ConfigureAwait(false);
        TestAssert.DoesNotContain("\"result\"", reply, StringComparison.Ordinal, "The request must not produce a result. " + reply);
        TestAssert.Equal(0, requestCount(), "The request must not reach the MCP handlers.");

        string garbage = await RawTcpExchangeAsync(port, "hello there\r\n\r\n").ConfigureAwait(false);
        TestAssert.DoesNotContain("\"result\"", garbage, StringComparison.Ordinal, garbage);
        TestAssert.Equal(0, requestCount(), "Unframed input must not reach the MCP handlers.");
    });

    public static Task TcpAcceptsFramedRequestsAsync() => WithTcpServerAsync(async (port, requestCount) =>
    {
        string json = ToolCallBody(McpTestTools.EchoToolName, "{\"message\":\"framed-hello\"}");
        string reply = await RawTcpFramedCallAsync(port, json, null, requestCount, expectedRequests: 1).ConfigureAwait(false);
        TestAssert.Contains("framed-hello", reply, StringComparison.Ordinal, "Expected a framed request to be served. " + reply);

        string typed = await RawTcpFramedCallAsync(port, json, "application/json; charset=utf-8", requestCount, expectedRequests: 1).ConfigureAwait(false);
        TestAssert.Contains("framed-hello", typed, StringComparison.Ordinal, "Content-Type is an allowed framing header. " + typed);
    });

    // ---- Settings and install ------------------------------------------------------------------------------

    public static void SettingsDefaultToLoopbackAndParseAccessOptions()
    {
        RestMcpServerSettings defaults = RestMcpServerSettings.FromArgs(Array.Empty<string>());
        if (Environment.GetEnvironmentVariable("RESTDB_MCP_HTTP_HOST") == null) TestAssert.Equal("localhost", defaults.HttpHostname, "HTTP should bind to localhost by default.");
        if (Environment.GetEnvironmentVariable("RESTDB_MCP_TCP_HOST") == null) TestAssert.Equal("127.0.0.1", defaults.TcpHostname, "TCP should bind to 127.0.0.1 by default.");
        if (Environment.GetEnvironmentVariable("RESTDB_MCP_WS_HOST") == null) TestAssert.Equal("localhost", defaults.WebSocketHostname, "WebSocket should bind to localhost by default.");
        if (Environment.GetEnvironmentVariable("RESTDB_MCP_TOKEN") == null) TestAssert.Null(defaults.McpToken, "No MCP token by default.");

        RestMcpServerSettings parsed = RestMcpServerSettings.FromArgs(new[]
        {
            "--allowed-origins", ConfiguredOrigin + ", https://other.example",
            "--mcp-token", Token
        });

        TestAssert.Equal(2, parsed.AllowedOrigins.Count, string.Join("|", parsed.AllowedOrigins));
        TestAssert.Contains(parsed.AllowedOrigins, o => o == ConfiguredOrigin, string.Join("|", parsed.AllowedOrigins));
        TestAssert.Contains(parsed.AllowedOrigins, o => o == "https://other.example", string.Join("|", parsed.AllowedOrigins));
        TestAssert.Equal(Token, parsed.McpToken, "Expected --mcp-token to be parsed.");

        RestMcpServerSettings blank = RestMcpServerSettings.FromArgs(new[] { "--mcp-token", "   " });
        TestAssert.Null(blank.McpToken, "A blank token must not enable token auth with an empty secret.");
    }

    public static void InstallWritesAuthorizationHeaderOnlyWithToken()
    {
        const string url = "http://localhost:8010/mcp";

        foreach (JsonObject config in new[]
        {
            RestMcpInstallHelper.BuildClaudeConfig(url, null),
            RestMcpInstallHelper.BuildGeminiConfig(url, null),
            RestMcpInstallHelper.BuildCursorConfig(url, null)
        })
        {
            TestAssert.Null(config["headers"], "No headers expected without a token: " + config.ToJsonString());
        }

        foreach (JsonObject config in new[]
        {
            RestMcpInstallHelper.BuildClaudeConfig(url, Token),
            RestMcpInstallHelper.BuildGeminiConfig(url, Token),
            RestMcpInstallHelper.BuildCursorConfig(url, Token)
        })
        {
            TestAssert.Equal("Bearer " + Token, config["headers"]?["Authorization"]?.GetValue<string>(), config.ToJsonString());
        }

        TestAssert.DoesNotContain("http_headers", RestMcpInstallHelper.BuildCodexToml(url, null), StringComparison.Ordinal, "No Codex headers expected without a token.");
        TestAssert.Contains(
            "http_headers = { Authorization = \"Bearer " + Token + "\" }",
            RestMcpInstallHelper.BuildCodexToml(url, Token),
            StringComparison.Ordinal,
            RestMcpInstallHelper.BuildCodexToml(url, Token));
    }

    // ---- Helpers -------------------------------------------------------------------------------------------

    private static string InitializeBody()
    {
        return "{\"jsonrpc\":\"2.0\",\"id\":0,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" +
            McpBridgeAssertions.NewestHandshakeProtocolVersion +
            "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"restdb-tests\",\"version\":\"1.0.0\"}}}";
    }

    private static string ToolsListBody()
    {
        return "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}";
    }

    private static string ToolCallBody(string toolName, string argumentsJson)
    {
        return "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + argumentsJson + "}}";
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string json, string? origin, string? authorization)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, "/mcp");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (origin != null) request.Headers.TryAddWithoutValidation("Origin", origin);
        if (authorization != null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendPreflightAsync(HttpClient client, string origin)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/mcp");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization, content-type, mcp-session-id, mcp-protocol-version");
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string? GetHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out IEnumerable<string>? values)) return string.Join(", ", values);
        if (response.Content.Headers.TryGetValues(name, out IEnumerable<string>? contentValues)) return string.Join(", ", contentValues);
        return null;
    }

    private static async Task WithWebSocketServerAsync(IEnumerable<string>? allowedOrigins, string? mcpToken, Func<int, Task> body)
    {
        int port = ReserveLoopbackPort();
        using McpWebsocketsServer server = RestMcpTransportFactory.CreateWebSocketServer("localhost", port, allowedOrigins, mcpToken);
        RestMcpToolRegistrar.Register(server, McpTestTools.Build());

        using CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task serverTask = Task.Run(() => server.StartAsync(tokenSource.Token));

        try
        {
            await WaitForHttpListenerAsync(port).ConfigureAwait(false);
            await body(port).ConfigureAwait(false);
        }
        finally
        {
            server.Stop();
            tokenSource.Cancel();
            await SwallowAsync(serverTask).ConfigureAwait(false);
        }
    }

    private static async Task<string> WebSocketCallAsync(int port, string? origin, string? authorization, string json)
    {
        using ClientWebSocket socket = new ClientWebSocket();
        if (origin != null) socket.Options.SetRequestHeader("Origin", origin);
        if (authorization != null) socket.Options.SetRequestHeader("Authorization", authorization);

        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(new Uri("ws://localhost:" + port + "/mcp"), timeout.Token).ConfigureAwait(false);

        // The server refuses requests before the MCP handshake, so initialize first.
        await socket.SendAsync(Encoding.UTF8.GetBytes(InitializeBody()), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
        string initialized = await ReceiveWebSocketMessageAsync(socket, timeout.Token).ConfigureAwait(false);
        TestAssert.Contains("\"result\"", initialized, StringComparison.Ordinal, "Expected the WebSocket initialize to succeed. " + initialized);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
        string reply = await ReceiveWebSocketMessageAsync(socket, timeout.Token).ConfigureAwait(false);

        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
        }

        return reply;
    }

    private static async Task<string> ReceiveWebSocketMessageAsync(ClientWebSocket socket, CancellationToken token)
    {
        StringBuilder message = new StringBuilder();
        byte[] buffer = new byte[16384];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        }
        while (!result.EndOfMessage);

        return message.ToString();
    }

    private static async Task AssertWebSocketConnectFailsAsync(int port, string? origin, string? authorization)
    {
        using ClientWebSocket socket = new ClientWebSocket();
        if (origin != null) socket.Options.SetRequestHeader("Origin", origin);
        if (authorization != null) socket.Options.SetRequestHeader("Authorization", authorization);

        try
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await socket.ConnectAsync(new Uri("ws://localhost:" + port + "/mcp"), timeout.Token).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            return;
        }

        throw new InvalidOperationException("Expected the WebSocket connection to be refused.");
    }

    /// <summary>
    /// Sends a WebSocket upgrade request and returns the status code and any WWW-Authenticate challenge.
    /// </summary>
    private static async Task<(HttpStatusCode Status, string Challenge)> WebSocketUpgradeAsync(int port, string path, string? origin, string? authorization)
    {
        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:" + port + path);
        request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        request.Headers.TryAddWithoutValidation("Sec-WebSocket-Key", Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        if (origin != null) request.Headers.TryAddWithoutValidation("Origin", origin);
        if (authorization != null) request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        return (response.StatusCode, response.Headers.WwwAuthenticate.ToString());
    }

    private static IPAddress? GetNonLoopbackIPv4()
    {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(info.Address))
                {
                    return info.Address;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Posts to the given address with a spoofed Host header. Returns the status code, or null when the connection is refused.
    /// </summary>
    private static async Task<HttpStatusCode?> TrySendFromAddressAsync(IPAddress address, int port, string hostHeader, string json)
    {
        string request =
            "POST /mcp HTTP/1.1\r\n" +
            "Host: " + hostHeader + "\r\n" +
            "Content-Type: application/json\r\n" +
            "Accept: application/json, text/event-stream\r\n" +
            "Content-Length: " + Encoding.UTF8.GetByteCount(json) + "\r\n" +
            "Connection: close\r\n\r\n" +
            json;
        return await RawHttpStatusAsync(address, port, request).ConfigureAwait(false);
    }

    private static async Task<HttpStatusCode?> TryWebSocketUpgradeFromAddressAsync(IPAddress address, int port, string hostHeader)
    {
        string request =
            "GET /mcp HTTP/1.1\r\n" +
            "Host: " + hostHeader + "\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Key: " + Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "\r\n\r\n";
        return await RawHttpStatusAsync(address, port, request).ConfigureAwait(false);
    }

    private static async Task<HttpStatusCode?> RawHttpStatusAsync(IPAddress address, int port, string request)
    {
        using TcpClient client = new TcpClient();
        try
        {
            using CancellationTokenSource connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(address, port, connectTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException || ex is OperationCanceledException)
        {
            return null;
        }

        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request)).ConfigureAwait(false);

        byte[] buffer = new byte[512];
        using CancellationTokenSource readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int read;
        try
        {
            read = await stream.ReadAsync(buffer, readTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.IO.IOException || ex is OperationCanceledException)
        {
            return null;
        }

        if (read <= 0) return null;

        // "HTTP/1.1 403 Forbidden"
        string[] statusLine = Encoding.ASCII.GetString(buffer, 0, read).Split('\n')[0].Split(' ');
        if (statusLine.Length > 1 && int.TryParse(statusLine[1], out int code)) return (HttpStatusCode)code;
        throw new InvalidOperationException("Unexpected response: " + Encoding.ASCII.GetString(buffer, 0, read));
    }

    private static async Task WithTcpServerAsync(Func<int, Func<int>, Task> body)
    {
        int port = ReserveLoopbackPort();
        using McpTcpServer server = RestMcpTransportFactory.CreateTcpServer(IPAddress.Loopback, port);
        RestMcpToolRegistrar.Register(server, McpTestTools.Build());

        int requests = 0;
        server.RequestReceived += (sender, e) => Interlocked.Increment(ref requests);

        using CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task serverTask = Task.Run(() => server.StartAsync(tokenSource.Token));

        try
        {
            await WaitForTcpListenerAsync(port).ConfigureAwait(false);
            await body(port, () => Volatile.Read(ref requests)).ConfigureAwait(false);
        }
        finally
        {
            server.Stop();
            tokenSource.Cancel();
            await SwallowAsync(serverTask).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a raw TCP connection, completes the MCP handshake with Content-Length framed messages (initialize, then
    /// notifications/initialized), sends the framed request, and returns its framed reply. Asserts that exactly
    /// <paramref name="expectedRequests"/> requests reached the server besides the two handshake messages.
    /// </summary>
    private static async Task<string> RawTcpFramedCallAsync(int port, string json, string? contentType, Func<int> requestCount, int expectedRequests)
    {
        using TcpClient client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        NetworkStream stream = client.GetStream();
        int before = requestCount();

        await WriteFramedAsync(stream, InitializeBody(), contentType).ConfigureAwait(false);
        string initialized = await ReadFramedAsync(stream).ConfigureAwait(false);
        TestAssert.Contains("\"result\"", initialized, StringComparison.Ordinal, "Expected the framed initialize to succeed. " + initialized);
        await WriteFramedAsync(stream, "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", contentType).ConfigureAwait(false);

        await WriteFramedAsync(stream, json, contentType).ConfigureAwait(false);
        string reply = await ReadFramedAsync(stream).ConfigureAwait(false);

        // RequestReceived counts every message (initialize and notifications/initialized too) and is raised off the
        // reply path, so wait for the expected total to land.
        int expected = expectedRequests + 2;
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (requestCount() - before < expected && DateTime.UtcNow < deadline) await Task.Delay(20).ConfigureAwait(false);
        TestAssert.Equal(expected, requestCount() - before, "Unexpected number of messages at the MCP server.");
        return reply;
    }

    private static async Task WriteFramedAsync(NetworkStream stream, string json, string? contentType)
    {
        string frame = "Content-Length: " + Encoding.UTF8.GetByteCount(json) + "\r\n" +
            (contentType != null ? "Content-Type: " + contentType + "\r\n" : string.Empty) +
            "\r\n" + json;
        await stream.WriteAsync(Encoding.UTF8.GetBytes(frame)).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one Content-Length framed message (headers and body) within 10 seconds.
    /// </summary>
    private static async Task<string> ReadFramedAsync(NetworkStream stream)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        List<byte> received = new List<byte>();
        byte[] one = new byte[1];

        while (true)
        {
            int read = await stream.ReadAsync(one, timeout.Token).ConfigureAwait(false);
            if (read <= 0) throw new InvalidOperationException("Connection closed before a framed reply arrived: " + Encoding.UTF8.GetString(received.ToArray()));
            received.Add(one[0]);

            string text = Encoding.UTF8.GetString(received.ToArray());
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0 || !TryGetContentLength(text, out int length)) continue;
            if (received.Count - Encoding.UTF8.GetByteCount(text.Substring(0, headerEnd + 4)) >= length) return text;
        }
    }

    /// <summary>
    /// Sends the payload and returns whatever arrives until the peer closes, a complete framed reply arrives,
    /// or 3 seconds pass without data.
    /// </summary>
    private static async Task<string> RawTcpExchangeAsync(int port, string payload)
    {
        using TcpClient client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        NetworkStream stream = client.GetStream();
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        await stream.WriteAsync(bytes).ConfigureAwait(false);

        StringBuilder reply = new StringBuilder();
        byte[] buffer = new byte[16384];

        while (true)
        {
            using CancellationTokenSource idle = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (System.IO.IOException)
            {
                break;
            }

            if (read <= 0) break;
            reply.Append(Encoding.UTF8.GetString(buffer, 0, read));

            string text = reply.ToString();
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd >= 0 && TryGetContentLength(text, out int length) && Encoding.UTF8.GetByteCount(text.Substring(headerEnd + 4)) >= length) break;
        }

        return reply.ToString();
    }

    private static bool TryGetContentLength(string text, out int length)
    {
        length = 0;
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(trimmed.Substring("Content-Length:".Length).Trim(), out length);
            }
        }

        return false;
    }

    private static async Task WaitForHttpListenerAsync(int port)
    {
        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        DateTime timeout = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync("http://localhost:" + port + "/mcp").ConfigureAwait(false);
                return;
            }
            catch
            {
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Timed out waiting for the WebSocket server to listen.");
    }

    private static async Task WaitForTcpListenerAsync(int port)
    {
        DateTime timeout = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                using TcpClient probe = new TcpClient();
                await probe.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                return;
            }
            catch
            {
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Timed out waiting for TCP port " + port + " to listen.");
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
