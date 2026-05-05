namespace RestDb.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.McpServer.Classes;
    using RestDb.McpServer.Registrations;
    using Voltaic;

    internal static class RestMcpServer
    {
        private const string ServerName = "RestDb.McpServer";
        private const string ServerVersion = "2.0.7";

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
            using McpServer server = new McpServer(includeDefaultMethods: true)
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion
            };

            RegisterTools(
                tools,
                (name, description, schema, handler) => server.RegisterTool(name, description, schema, handler),
                (name, handler) => server.RegisterMethod(name, handler));

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
            using McpHttpServer httpServer = new McpHttpServer(settings.HttpHostname, settings.HttpPort, "/rpc", "/events", includeDefaultMethods: true, mcpPath: "/mcp")
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion
            };

            using McpTcpServer tcpServer = new McpTcpServer(IPAddress.Parse(settings.TcpHostname), settings.TcpPort, includeDefaultMethods: true)
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion
            };

            using McpWebsocketsServer wsServer = new McpWebsocketsServer(settings.WebSocketHostname, settings.WebSocketPort, "/mcp", includeDefaultMethods: true)
            {
                ServerName = ServerName,
                ServerVersion = ServerVersion
            };

            httpServer.Log += (sender, message) => Console.WriteLine("[MCP HTTP] " + message);
            tcpServer.Log += (sender, message) => Console.WriteLine("[MCP TCP] " + message);
            wsServer.Log += (sender, message) => Console.WriteLine("[MCP WS] " + message);

            RegisterTools(
                tools,
                (name, description, schema, handler) => httpServer.RegisterTool(name, description, schema, handler),
                (name, handler) => httpServer.RegisterMethod(name, handler));

            RegisterMethodOnlyTools(tcpServer, tools);
            RegisterMethodOnlyTools(wsServer, tools);

            CancellationTokenSource tokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
                httpServer.Stop();
                tcpServer.Stop();
                wsServer.Stop();
            };

            Console.WriteLine("Rest MCP server");
            Console.WriteLine("HTTP:       http://" + settings.HttpHostname + ":" + settings.HttpPort + "/mcp");
            Console.WriteLine("TCP:        tcp://" + settings.TcpHostname + ":" + settings.TcpPort);
            Console.WriteLine("WebSocket:  ws://" + settings.WebSocketHostname + ":" + settings.WebSocketPort + "/mcp");
            Console.WriteLine("Proxy:      " + settings.RestDbServerUrl);
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
            Console.WriteLine("  --http-host <hostname>   HTTP MCP listener host (default: +)");
            Console.WriteLine("  --http-port <port>       HTTP MCP listener port (default: 8010)");
            Console.WriteLine("  --tcp-host <hostname>    TCP MCP listener host (default: 0.0.0.0)");
            Console.WriteLine("  --tcp-port <port>        TCP MCP listener port (default: 8011)");
            Console.WriteLine("  --ws-host <hostname>     WebSocket MCP listener host (default: +)");
            Console.WriteLine("  --ws-port <port>         WebSocket MCP listener port (default: 8012)");
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
            Console.WriteLine("  RestDb.McpServer install --api-key default --yes");
        }

        private static void RegisterTools(
            IEnumerable<RestMcpToolDefinition> tools,
            Action<string, string, object, Func<JsonElement?, CancellationToken, Task<object>>> registerTool,
            Action<string, Func<JsonElement?, CancellationToken, Task<object>>> registerMethod)
        {
            foreach (RestMcpToolDefinition tool in tools)
            {
                registerTool(tool.Name, tool.Description, tool.InputSchema, tool.Handler);
                registerMethod(tool.Name, tool.Handler);
            }
        }

        private static void RegisterMethodOnlyTools(McpTcpServer server, List<RestMcpToolDefinition> tools)
        {
            foreach (RestMcpToolDefinition tool in tools)
            {
                server.RegisterMethod(tool.Name, tool.Handler);
            }

            server.RegisterMethod("tools/list", (Func<JsonElement?, object>)(args => new
            {
                tools = tools.Select(tool => new
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = tool.InputSchema
                }).ToArray()
            }));
        }

        private static void RegisterMethodOnlyTools(McpWebsocketsServer server, List<RestMcpToolDefinition> tools)
        {
            foreach (RestMcpToolDefinition tool in tools)
            {
                server.RegisterMethod(tool.Name, tool.Handler);
            }

            server.RegisterMethod("tools/list", (Func<JsonElement?, object>)(args => new
            {
                tools = tools.Select(tool => new
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = tool.InputSchema
                }).ToArray()
            }));
        }
    }
}
