namespace RestDb.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.McpServer.Classes;
    using RestDb.McpServer.Registrations;
    using Voltaic.Mcp;

    internal static class RestMcpServer
    {

        private static async Task<int> Main(string[] args)
        {
            RestMcpServerSettings settings = RestMcpServerSettings.FromArgs(args);

            if (settings.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            if (settings.InstallAgentDefinitions)
            {
                return await RestMcpInstallHelper.RunInstallAsync(settings).ConfigureAwait(false);
            }

            using RestMcpRestProxy proxy = new RestMcpRestProxy(settings);
            List<RestMcpToolDefinition> tools = RestMcpToolCatalog.Build(proxy);

            if (settings.StdioOnly)
            {
                return await RunStdioAsync(tools).ConfigureAwait(false);
            }

            return await RunNetworkServersAsync(settings, tools).ConfigureAwait(false);
        }

        private static async Task<int> RunStdioAsync(List<RestMcpToolDefinition> tools)
        {
            using McpServer server = new McpServer()
            {
                ServerName = RestMcpTransportFactory.ServerName,
                ServerVersion = RestMcpTransportFactory.ServerVersion
            };

            RestMcpToolRegistrar.Register(server, tools);

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
            };

            await server.RunAsync(tokenSource.Token).ConfigureAwait(false);
            return 0;
        }

        private static async Task<int> RunNetworkServersAsync(RestMcpServerSettings settings, List<RestMcpToolDefinition> tools)
        {
            // Voltaic enforces access on every transport: browser Origin validation and loopback-only clients on
            // HTTP and WebSocket, the optional MCP token on HTTP and WebSocket, and strict framing on TCP.
            using McpHttpServer httpServer = RestMcpTransportFactory.CreateHttpServer(settings.HttpHostname, settings.HttpPort, settings.AllowedOrigins, settings.McpToken);
            using McpTcpServer tcpServer = RestMcpTransportFactory.CreateTcpServer(IPAddress.Parse(settings.TcpHostname), settings.TcpPort);
            using McpWebsocketsServer wsServer = RestMcpTransportFactory.CreateWebSocketServer(settings.WebSocketHostname, settings.WebSocketPort, settings.AllowedOrigins, settings.McpToken);

            httpServer.Log += (sender, message) => Console.WriteLine("[MCP HTTP] " + message);
            tcpServer.Log += (sender, message) => Console.WriteLine("[MCP TCP] " + message);
            wsServer.Log += (sender, message) => Console.WriteLine("[MCP WS] " + message);

            RestMcpToolRegistrar.Register(httpServer, tools);
            RestMcpToolRegistrar.Register(tcpServer, tools);
            RestMcpToolRegistrar.Register(wsServer, tools);

            CancellationTokenSource tokenSource = new CancellationTokenSource();

            void StopAll()
            {
                httpServer.Stop();
                tcpServer.Stop();
                wsServer.Stop();
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
                StopAll();
            };

            Console.WriteLine("Rest MCP server");
            Console.WriteLine("HTTP:       http://" + settings.HttpHostname + ":" + settings.HttpPort + RestMcpTransportFactory.McpPath);
            Console.WriteLine("TCP:        tcp://" + settings.TcpHostname + ":" + settings.TcpPort);
            Console.WriteLine("WebSocket:  ws://" + settings.WebSocketHostname + ":" + settings.WebSocketPort + RestMcpTransportFactory.McpPath);
            Console.WriteLine("Proxy:      " + settings.RestDbServerUrl);
            Console.WriteLine("Origins:    loopback" + (settings.AllowedOrigins.Count > 0 ? ", " + String.Join(", ", settings.AllowedOrigins) : String.Empty));
            Console.WriteLine("MCP token:  " + (String.IsNullOrWhiteSpace(settings.McpToken) ? "not configured" : "required (HTTP, WebSocket)"));
            Console.WriteLine();

            try
            {
                Task httpTask = Task.Run(async () => await httpServer.StartAsync(tokenSource.Token).ConfigureAwait(false));
                Task tcpTask = Task.Run(async () => await tcpServer.StartAsync(tokenSource.Token).ConfigureAwait(false));
                Task wsTask = Task.Run(async () => await wsServer.StartAsync(tokenSource.Token).ConfigureAwait(false));

                await Task.WhenAll(httpTask, tcpTask, wsTask).ConfigureAwait(false);
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("RestDb.McpServer failed: " + ex.Message);
                return 1;
            }
            finally
            {
                StopAll();
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("RestDb.McpServer");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RestDb.McpServer [options]");
            Console.WriteLine("  RestDb.McpServer install [options]");
            Console.WriteLine();
            Console.WriteLine("Server options:");
            Console.WriteLine("  --server-url <url>       RestDb API base URL (default: http://localhost:8000)");
            Console.WriteLine("  --api-key <value>        RestDb API key to forward downstream");
            Console.WriteLine("  --api-key-header <name>  RestDb API key header name (default: x-api-key)");
            Console.WriteLine("  --bearer-token <value>   Bearer token to forward downstream");
            Console.WriteLine("  --http-host <hostname>   HTTP MCP listener host (default: localhost; + for all)");
            Console.WriteLine("  --http-port <port>       HTTP MCP listener port (default: 8010)");
            Console.WriteLine("  --tcp-host <hostname>    TCP MCP listener host (default: 127.0.0.1; 0.0.0.0 for all)");
            Console.WriteLine("  --tcp-port <port>        TCP MCP listener port (default: 8011)");
            Console.WriteLine("  --ws-host <hostname>     WebSocket MCP listener host (default: localhost; + for all)");
            Console.WriteLine("  --ws-port <port>         WebSocket MCP listener port (default: 8012)");
            Console.WriteLine("  --allowed-origins <list> Extra browser origins allowed on HTTP/WebSocket, comma separated");
            Console.WriteLine("                           (loopback origins are always allowed; * allows any origin)");
            Console.WriteLine("  --mcp-token <value>      Bearer token MCP clients must send on HTTP/WebSocket");
            Console.WriteLine("  --stdio                  Run stdio MCP transport only");
            Console.WriteLine();
            Console.WriteLine("Install options:");
            Console.WriteLine("  install                  Configure Claude Code, Codex, Gemini CLI, and Cursor");
            Console.WriteLine("  --dry-run                Preview generated config without writing files");
            Console.WriteLine("  --yes, -y                Write all supported client configs without prompting");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  RestDb.McpServer --server-url http://localhost:8000 --api-key default");
            Console.WriteLine("  RestDb.McpServer --stdio --server-url http://localhost:8000 --api-key default");
            Console.WriteLine("  RestDb.McpServer install --yes");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - install only writes MCP client definitions.");
            Console.WriteLine("  - Configure downstream RestDb auth on the RestDb.McpServer process itself");
            Console.WriteLine("    using --api-key / --bearer-token or RESTDB_MCP_* environment variables.");
            Console.WriteLine("  - --mcp-token authenticates MCP clients to this server and is never sent to RestDb.");
            Console.WriteLine("    Environment: RESTDB_MCP_TOKEN, RESTDB_MCP_ALLOWED_ORIGINS.");
        }
    }
}
