namespace RestDb.McpServer.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Builds the MCP network servers with RestDb's access configuration, so the production host and the tests
    /// use identical settings. Voltaic enforces the rules: browser Origin validation and loopback-only clients
    /// on HTTP and WebSocket, the optional bearer token on HTTP and WebSocket, and strict framing on TCP.
    /// TCP and stdio are intentionally unauthenticated.
    /// </summary>
    internal static class RestMcpTransportFactory
    {
        internal const string McpPath = "/mcp";
        internal const string ServerName = "RestDb.McpServer";
        internal const string ServerVersion = "2.1.0";
        internal const string TokenPrincipal = "mcp-token";

        internal static McpHttpServer CreateHttpServer(string hostname, int port, IEnumerable<string>? allowedOrigins, string? mcpToken)
        {
            McpHttpServer server = new McpHttpServer(hostname, port, mcpPath: McpPath)
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion,
                AuthenticationHandler = CreateAuthenticationHandler(mcpToken)
            };

            ApplyAllowedOrigins(server.OriginPolicy, allowedOrigins);
            return server;
        }

        internal static McpWebsocketsServer CreateWebSocketServer(string hostname, int port, IEnumerable<string>? allowedOrigins, string? mcpToken)
        {
            McpWebsocketsServer server = new McpWebsocketsServer(hostname, port, McpPath)
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion,
                AuthenticationHandler = CreateAuthenticationHandler(mcpToken)
            };

            ApplyAllowedOrigins(server.OriginPolicy, allowedOrigins);
            return server;
        }

        internal static McpTcpServer CreateTcpServer(IPAddress address, int port)
        {
            return new McpTcpServer(address, port)
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion
            };
        }

        /// <summary>
        /// Adds browser origins allowed in addition to loopback origins. "*" allows any origin.
        /// </summary>
        internal static void ApplyAllowedOrigins(OriginPolicy policy, IEnumerable<string>? allowedOrigins)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (allowedOrigins == null) return;

            foreach (string origin in allowedOrigins)
            {
                if (!String.IsNullOrWhiteSpace(origin)) policy.AllowedOrigins.Add(origin.Trim().TrimEnd('/'));
            }
        }

        /// <summary>
        /// Returns a handler that requires "Authorization: Bearer &lt;token&gt;", or null when no token is configured
        /// (null, empty, or whitespace, so a blank value never becomes an empty secret).
        /// A missing token gets a bare Bearer challenge; a wrong one adds error="invalid_token" (RFC 6750 section 3.1).
        /// </summary>
        internal static Func<HttpListenerRequest, Task<AuthenticationResult>>? CreateAuthenticationHandler(string? mcpToken)
        {
            if (String.IsNullOrWhiteSpace(mcpToken)) return null;

            byte[] expected = Encoding.UTF8.GetBytes(mcpToken);

            return request =>
            {
                string? authorization = request.Headers["Authorization"];

                if (IsAuthorized(authorization, expected))
                {
                    return Task.FromResult(new AuthenticationResult
                    {
                        IsAuthenticated = true,
                        Principal = TokenPrincipal
                    });
                }

                return Task.FromResult(AuthenticationResult.BearerChallenge(
                    null,
                    String.IsNullOrWhiteSpace(authorization) ? null : "invalid_token",
                    null,
                    "Missing or invalid bearer token."));
            };
        }

        /// <summary>
        /// Validates an Authorization header value of the form "Bearer &lt;token&gt;" in constant time.
        /// </summary>
        internal static bool IsAuthorized(string? authorizationHeader, byte[] expected)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (String.IsNullOrWhiteSpace(authorizationHeader)) return false;

            const string scheme = "Bearer ";
            string value = authorizationHeader.Trim();
            if (!value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return false;

            byte[] presented = Encoding.UTF8.GetBytes(value.Substring(scheme.Length).Trim());
            return CryptographicOperations.FixedTimeEquals(presented, expected);
        }
    }
}
