namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RestDb.McpServer.Classes;
using RestDb.McpServer.Telemetry;
using Voltaic.Mcp;

/// <summary>
/// Proves RestDb.McpServer emits its documented telemetry: tool invocations per transport and outcome, RestDb API
/// client spans with W3C trace-context propagation, downstream failure paths, configuration gauges, and host lifecycle.
/// </summary>
internal static class McpTelemetryAssertions
{
    private static readonly string[] Meters = { McpTelemetryNames.SourceName };
    private static readonly string[] Sources = { McpTelemetryNames.SourceName };

    public static async Task NoListenerPathDoesNotThrowAsync()
    {
        object result = await McpTelemetry.InvokeToolAsync("probe", McpTelemetryNames.TransportTcp, McpTelemetryNames.InvocationTool,
            () => Task.FromResult<object>("ok")).ConfigureAwait(false);
        TestAssert.Equal("ok", result as string);

        McpTelemetry.CompleteDownstream(null, HttpMethod.Get, "/", 0, new HttpRequestException("probe"), 0.01);
        McpTelemetry.CompleteDownstream(null, HttpMethod.Get, "/", 503, null, 0.01);
        McpTelemetry.RecordConnection(McpTelemetryNames.TransportHttp, true);
        McpTelemetry.RecordConnection(McpTelemetryNames.TransportHttp, false);
        McpTelemetry.InjectTraceContext(null!);
        McpTelemetry.UpdateConfigurationState(null!);

        using (McpTelemetryHost disabled = McpTelemetryHost.Start(new RestMcpServerSettings { TelemetryEnable = false }))
        {
            TestAssert.False(disabled.IsRunning, "A disabled MCP telemetry host must be inert.");
        }
    }

    public static async Task ToolCallsOverTcpEmitMetricsAndSpansAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        int port = ReserveLoopbackPort();
        using McpTcpServer server = RestMcpTransportFactory.CreateTcpServer(IPAddress.Loopback, port);
        RestMcpToolRegistrar.Register(server, McpTestTools.Build());

        using CancellationTokenSource tokenSource = new CancellationTokenSource();
        Task serverTask = Task.Run(() => server.StartAsync(tokenSource.Token));

        try
        {
            using McpTcpClient client = new McpTcpClient();
            await ConnectWithRetryAsync(() => client.ConnectAsync("127.0.0.1", port)).ConfigureAwait(false);

            await client.CallAsync<JsonElement>("tools/call", new { name = McpTestTools.DownstreamOkToolName, arguments = new { } }).ConfigureAwait(false);
            await client.CallAsync<JsonElement>("tools/call", new { name = McpTestTools.DownstreamNotFoundToolName, arguments = new { } }).ConfigureAwait(false);
            await client.CallAsync<JsonElement>(McpTestTools.DownstreamOkToolName, new { }).ConfigureAwait(false);

            try
            {
                await client.CallAsync<JsonElement>("tools/call", new { name = McpTestTools.FailToolName, arguments = new { } }).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The transport surfaces the handler failure as an RPC error; the telemetry is what is under test.
            }
        }
        finally
        {
            server.Stop();
            tokenSource.Cancel();
            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        IReadOnlyList<CapturedMeasurement> calls = capture.Measurements(McpTelemetryNames.ToolCalls);
        TestAssert.Contains(calls, m => m.Has(McpTelemetryNames.AttributeToolName, McpTestTools.DownstreamOkToolName)
            && m.Has(McpTelemetryNames.AttributeTransport, McpTelemetryNames.TransportTcp)
            && m.Has(McpTelemetryNames.AttributeInvocation, McpTelemetryNames.InvocationTool)
            && m.Has(McpTelemetryNames.AttributeOutcome, McpTelemetryNames.OutcomeSuccess), "Expected a successful tools/call over TCP.");
        TestAssert.Contains(calls, m => m.Has(McpTelemetryNames.AttributeToolName, McpTestTools.DownstreamOkToolName)
            && m.Has(McpTelemetryNames.AttributeInvocation, McpTelemetryNames.InvocationMethod), "Expected the direct method invocation.");
        TestAssert.Contains(calls, m => m.Has(McpTelemetryNames.AttributeToolName, McpTestTools.DownstreamNotFoundToolName)
            && m.Has(McpTelemetryNames.AttributeOutcome, McpTelemetryNames.OutcomeDownstreamError)
            && m.Has(McpTelemetryNames.AttributeErrorType, "404"), "Expected a downstream_error with error.type=404.");
        TestAssert.Contains(calls, m => m.Has(McpTelemetryNames.AttributeToolName, McpTestTools.FailToolName)
            && m.Has(McpTelemetryNames.AttributeOutcome, McpTelemetryNames.OutcomeError)
            && m.Has(McpTelemetryNames.AttributeErrorType, "System.InvalidOperationException"), "Expected the handler failure as outcome=error.");

        TestAssert.Contains(capture.Measurements(McpTelemetryNames.ToolDuration), m => m.Has(McpTelemetryNames.AttributeToolName, McpTestTools.DownstreamOkToolName)
            && m.Unit == "s", "Expected the tool duration histogram in seconds.");
        TestAssert.Equal(0d, capture.Measurements(McpTelemetryNames.ToolActive).Sum(m => m.Value), "The in-flight gauge must return to zero.");

        List<Activity> spans = capture.Spans().ToList();
        Activity ok = spans.First(a => a.DisplayName == "tools/call " + McpTestTools.DownstreamOkToolName);
        TestAssert.Equal(ActivityKind.Server, ok.Kind);
        TestAssert.Equal(ActivityStatusCode.Ok, ok.Status);
        TestAssert.Equal(McpTelemetryNames.TransportTcp, (string?)ok.GetTagItem(McpTelemetryNames.AttributeTransport));

        Activity failed = spans.First(a => a.DisplayName == "tools/call " + McpTestTools.FailToolName);
        TestAssert.Equal(ActivityStatusCode.Error, failed.Status);
        TestAssert.True(failed.Events.Any(e => e.Name == "exception"), "Expected an exception event on the failed tool span.");
        string eventTags = String.Join("|", failed.Events.SelectMany(e => e.Tags).Select(t => t.Key + "=" + t.Value));
        TestAssert.DoesNotContain(McpTestTools.FailureMessage, eventTags, StringComparison.Ordinal, "Exception messages must not be recorded.");

        TestAssert.True(spans.Any(a => a.DisplayName == McpTestTools.DownstreamOkToolName), "Expected a span for the direct method call.");
    }

    public static async Task ProxyPropagatesTraceContextAndRecordsDownstreamAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        int port = ReserveLoopbackPort();
        string prefix = "http://127.0.0.1:" + port + "/";
        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        List<string?> traceparents = new List<string?>();
        Task serve = Task.Run(async () =>
        {
            for (int i = 0; i < 2; i++)
            {
                HttpListenerContext ctx = await listener.GetContextAsync().ConfigureAwait(false);
                lock (traceparents) traceparents.Add(ctx.Request.Headers["traceparent"]);
                ctx.Response.StatusCode = ctx.Request.Url!.AbsolutePath.EndsWith("/missing", StringComparison.Ordinal) ? 404 : 200;
                byte[] body = Encoding.UTF8.GetBytes("[]");
                ctx.Response.ContentType = "application/json";
                await ctx.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                ctx.Response.Close();
            }
        });

        RestMcpServerSettings settings = new RestMcpServerSettings { RestDbServerUrl = prefix.TrimEnd('/') };
        using RestMcpRestProxy proxy = new RestMcpRestProxy(settings);

        RestMcpResponse ok = (RestMcpResponse)await McpTelemetry.InvokeToolAsync("restdb_test_proxy", McpTelemetryNames.TransportHttp, McpTelemetryNames.InvocationTool,
            async () => await proxy.SendAsync(HttpMethod.Get, "/inventory/widgets?_describe=true", null, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
        RestMcpResponse missing = await proxy.SendAsync(HttpMethod.Get, "/inventory/widgets/missing", null, CancellationToken.None).ConfigureAwait(false);
        await serve.ConfigureAwait(false);
        listener.Stop();

        TestAssert.True(ok.Success, "Expected the first proxied call to succeed.");
        TestAssert.False(missing.Success, "Expected the second proxied call to be a 404.");

        Activity tool = capture.Spans().Single(a => a.DisplayName == "tools/call restdb_test_proxy");
        Activity client = capture.Spans().Single(a => a.DisplayName == "restdb GET /{database}/{table}");
        TestAssert.Equal(ActivityKind.Client, client.Kind);
        TestAssert.Equal(tool.SpanId, client.ParentSpanId, "The RestDb client span must be a child of the tool span.");
        TestAssert.Equal("/{database}/{table}", (string?)client.GetTagItem(McpTelemetryNames.AttributeUrlTemplate));
        TestAssert.Equal(200, (int?)client.GetTagItem(McpTelemetryNames.AttributeHttpStatusCode));

        string? propagated = traceparents[0];
        TestAssert.NotNull(propagated, "The proxy must send a traceparent header to RestDb.");
        TestAssert.Contains(client.TraceId.ToHexString(), propagated, StringComparison.Ordinal, "traceparent must carry the trace id.");
        TestAssert.Contains(client.SpanId.ToHexString(), propagated, StringComparison.Ordinal, "traceparent must name the client span as the parent.");

        Activity notFound = capture.Spans().Single(a => a.DisplayName == "restdb GET /{database}/{table}/{id}");
        TestAssert.Equal(ActivityStatusCode.Error, notFound.Status, "A 4xx from RestDb is a client-span error.");

        IReadOnlyList<CapturedMeasurement> requests = capture.Measurements(McpTelemetryNames.DownstreamRequests);
        TestAssert.Contains(requests, m => m.Has(McpTelemetryNames.AttributeUrlTemplate, "/{database}/{table}")
            && m.Has(McpTelemetryNames.AttributeOutcome, McpTelemetryNames.OutcomeSuccess)
            && m.Has(McpTelemetryNames.AttributeHttpStatusCode, "200"), "Expected the successful downstream request.");
        TestAssert.Contains(requests, m => m.Has(McpTelemetryNames.AttributeUrlTemplate, "/{database}/{table}/{id}")
            && m.Has(McpTelemetryNames.AttributeOutcome, McpTelemetryNames.OutcomeDownstreamError)
            && m.Has(McpTelemetryNames.AttributeErrorType, "404"), "Expected the 404 downstream request.");
        TestAssert.False(requests.Any(m => m.Tags.Values.Any(v => v != null && (v.Contains("inventory", StringComparison.Ordinal) || v.Contains("widgets", StringComparison.Ordinal)))),
            "Database and table names must never become metric labels.");
    }

    public static async Task ProxyNetworkFailureIsRecordedAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        int port = ReserveLoopbackPort();
        RestMcpServerSettings settings = new RestMcpServerSettings { RestDbServerUrl = "http://127.0.0.1:" + port };
        using RestMcpRestProxy proxy = new RestMcpRestProxy(settings);
        bool threw = false;

        try
        {
            await proxy.SendAsync(HttpMethod.Post, "/_settings/reload", null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            threw = true;
        }

        TestAssert.True(threw, "Expected a connection failure.");
        CapturedMeasurement request = capture.Measurements(McpTelemetryNames.DownstreamRequests).Single(m => m.Has(McpTelemetryNames.AttributeUrlTemplate, "/_settings/reload"));
        TestAssert.True(request.Has(McpTelemetryNames.AttributeOutcome, McpTelemetryNames.OutcomeError), "Expected outcome=error.");
        TestAssert.True(request.Has(McpTelemetryNames.AttributeErrorType, "System.Net.Http.HttpRequestException"), "Expected error.type to be the exception type.");
        Activity span = capture.Spans().Single(a => a.DisplayName == "restdb POST /_settings/reload");
        TestAssert.Equal(ActivityStatusCode.Error, span.Status);
    }

    public static void RouteTemplatesAndSettingsAreBounded()
    {
        TestAssert.Equal("/", RestDbRouteTemplate.FromPath("/"));
        TestAssert.Equal("/_databases", RestDbRouteTemplate.FromPath("/_databases"));
        TestAssert.Equal("/_context/{database}", RestDbRouteTemplate.FromPath("/_context/customers"));
        TestAssert.Equal("/_context/reload", RestDbRouteTemplate.FromPath("/_context/reload"));
        TestAssert.Equal("/{database}", RestDbRouteTemplate.FromPath("/customers?raw"));
        TestAssert.Equal("/{database}/{table}/{id}", RestDbRouteTemplate.FromPath("customers/orders/42?_debug=true"));
        TestAssert.Equal("/(other)", RestDbRouteTemplate.FromPath("/a/b/c/d"));

        RestMcpServerSettings network = RestMcpServerSettings.FromArgs(Array.Empty<string>());
        TestAssert.True(network.TelemetryEnable, "Telemetry is on by default.");
        TestAssert.True(network.EffectivePrometheusEnable, "The scrape endpoint is on by default in network mode.");
        TestAssert.Equal("127.0.0.1", network.PrometheusHostname, "The scrape endpoint binds loopback by default.");
        TestAssert.Equal("http://127.0.0.1:4317", network.OtlpEndpoint);

        RestMcpServerSettings stdio = RestMcpServerSettings.FromArgs(new[] { "--stdio" });
        TestAssert.False(stdio.EffectivePrometheusEnable, "The scrape endpoint is off by default in stdio mode.");

        RestMcpServerSettings flags = RestMcpServerSettings.FromArgs(new[] { "--stdio", "--prometheus-port", "19999", "--otlp-endpoint", "http://tempo:4317", "--no-otlp" });
        TestAssert.True(flags.EffectivePrometheusEnable, "An explicit --prometheus-port enables the endpoint.");
        TestAssert.Equal(19999, flags.PrometheusPort);
        TestAssert.Equal("http://tempo:4317", flags.OtlpEndpoint);
        TestAssert.False(flags.OtlpEnable);
        TestAssert.False(RestMcpServerSettings.FromArgs(new[] { "--no-telemetry" }).TelemetryEnable);
    }

    public static async Task HostServesPrometheusAndGaugesAsync()
    {
        int port = ReserveLoopbackPort();
        RestMcpServerSettings settings = new RestMcpServerSettings
        {
            OtlpEnable = false,
            PrometheusEnable = true,
            PrometheusPort = port,
            McpToken = "secret-token-value",
            AllowedOrigins = new List<string> { "https://app.example" }
        };

        using (McpTelemetryHost host = McpTelemetryHost.Start(settings))
        {
            TestAssert.True(host.IsRunning, "Expected the MCP telemetry host to start: " + host.StartupError);
            await McpTelemetry.InvokeToolAsync("restdb_test_scrape", McpTelemetryNames.TransportWebSocket, McpTelemetryNames.InvocationTool,
                () => Task.FromResult<object>("ok")).ConfigureAwait(false);

            string text = await TelemetryAssertions.ScrapeUntilAsync(new Uri(host.ScrapeUrl!), "restdb_test_scrape").ConfigureAwait(false);
            TelemetryAssertions.AssertSeries(text, "restdb_mcp_tool_calls_total", "gen_ai_tool_name", "restdb_test_scrape", "restdb_mcp_transport", "websocket", "outcome", "success");
            TelemetryAssertions.AssertSeries(text, "restdb_mcp_tool_duration_seconds_bucket", "gen_ai_tool_name", "restdb_test_scrape");
            TelemetryAssertions.AssertSeries(text, "restdb_mcp_build_info", "restdb_mcp_mode", "network");
            TelemetryAssertions.AssertSeries(text, "restdb_mcp_config_token_required");
            TelemetryAssertions.AssertSeries(text, "restdb_mcp_config_allowed_origins");
            TestAssert.DoesNotContain("secret-token-value", text, StringComparison.Ordinal, "Secrets must never be exported.");
            TestAssert.Contains("restdb_mcp_config_token_required{otel_scope_name=\"RestDb.McpServer\"", text, StringComparison.Ordinal);
        }

        using (McpTelemetryHost again = McpTelemetryHost.Start(settings))
        {
            TestAssert.True(again.IsRunning, "Disposing the host must release the scrape port: " + again.StartupError);
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
