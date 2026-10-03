namespace RestDb.McpServer.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.McpServer.Classes;
    using Voltaic.Mcp;

    /// <summary>
    /// RestDb.McpServer's instrumentation: one <see cref="Meter"/> and one <see cref="ActivitySource"/>, both named
    /// <see cref="McpTelemetryNames.SourceName"/>, plus typed helpers for tool invocations, RestDb API calls, and
    /// connection events. Emission rides the base class library only and is effectively free until a listener subscribes.
    /// Every member is best-effort: telemetry failures never reach tool handling.
    /// Thread safety: all members are safe for concurrent use.
    /// </summary>
    internal static class McpTelemetry
    {
        #region Internal-Members

        /// <summary>
        /// The RestDb.McpServer version stamped on the meter, the activity source, and the build-info gauge.
        /// </summary>
        internal static readonly string Version = ResolveVersion();

        /// <summary>
        /// The RestDb.McpServer activity source.
        /// </summary>
        internal static readonly ActivitySource Source = new ActivitySource(McpTelemetryNames.SourceName, Version);

        /// <summary>
        /// The RestDb.McpServer meter.
        /// </summary>
        internal static readonly Meter Meter = new Meter(McpTelemetryNames.SourceName, Version);

        #endregion

        #region Private-Members

        private static readonly double[] _LatencyBuckets = new double[]
        {
            0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60
        };

        private static readonly Counter<long> _ToolCalls = Meter.CreateCounter<long>(
            McpTelemetryNames.ToolCalls, "{call}", "MCP tool invocations, by tool, transport, invocation style, and outcome.");

        private static readonly Histogram<double> _ToolDuration = Meter.CreateHistogram<double>(
            McpTelemetryNames.ToolDuration, "s", "MCP tool invocation duration, by tool, transport, invocation style, and outcome.",
            null, new InstrumentAdvice<double> { HistogramBucketBoundaries = _LatencyBuckets });

        private static readonly UpDownCounter<long> _ToolActive = Meter.CreateUpDownCounter<long>(
            McpTelemetryNames.ToolActive, "{call}", "MCP tool invocations currently executing, by transport.");

        private static readonly Counter<long> _DownstreamRequests = Meter.CreateCounter<long>(
            McpTelemetryNames.DownstreamRequests, "{request}", "Requests to the RestDb API, by method, route template, outcome, and status code.");

        private static readonly Histogram<double> _DownstreamDuration = Meter.CreateHistogram<double>(
            McpTelemetryNames.DownstreamDuration, "s", "RestDb API request duration seen by the MCP server, by method, route template, and outcome.",
            null, new InstrumentAdvice<double> { HistogramBucketBoundaries = _LatencyBuckets });

        private static readonly Counter<long> _Connections = Meter.CreateCounter<long>(
            McpTelemetryNames.Connections, "{connection}", "MCP client connections accepted, by transport.");

        private static readonly UpDownCounter<long> _ConnectionsActive = Meter.CreateUpDownCounter<long>(
            McpTelemetryNames.ConnectionsActive, "{connection}", "MCP client connections currently open, by transport.");

        private static string _Mode = McpTelemetryNames.ModeNetwork;
        private static int _TokenRequired = 0;
        private static int _AllowedOrigins = 0;

        #endregion

        #region Constructors-and-Factories

        static McpTelemetry()
        {
            try
            {
                Meter.CreateObservableGauge<long>(
                    McpTelemetryNames.BuildInfo,
                    () => new Measurement<long>(
                        1,
                        new KeyValuePair<string, object?>(McpTelemetryNames.AttributeServiceVersion, Version),
                        new KeyValuePair<string, object?>(McpTelemetryNames.AttributeMode, Volatile.Read(ref _Mode))),
                    null,
                    "Always 1; labeled with the running version and mode.");

                Meter.CreateObservableGauge<int>(
                    McpTelemetryNames.ConfigTokenRequired,
                    () => Volatile.Read(ref _TokenRequired),
                    null,
                    "1 when MCP clients must present a bearer token on HTTP and WebSocket.");

                Meter.CreateObservableGauge<int>(
                    McpTelemetryNames.ConfigAllowedOrigins,
                    () => Volatile.Read(ref _AllowedOrigins),
                    "{origin}",
                    "Browser origins allowed in addition to loopback.");
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Publish the safe configuration state read by the configuration gauges.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        internal static void UpdateConfigurationState(RestMcpServerSettings settings)
        {
            if (settings == null) return;
            Volatile.Write(ref _Mode, settings.StdioOnly ? McpTelemetryNames.ModeStdio : McpTelemetryNames.ModeNetwork);
            Volatile.Write(ref _TokenRequired, String.IsNullOrWhiteSpace(settings.McpToken) ? 0 : 1);
            Volatile.Write(ref _AllowedOrigins, settings.AllowedOrigins?.Count ?? 0);
        }

        /// <summary>
        /// Run a tool handler inside a root span "tools/call {tool}" (or "{tool}" for a direct method call) and record
        /// the tool counter and duration histogram. A <see cref="RestMcpResponse"/> with Success false is recorded as a
        /// downstream_error; an exception as an error and rethrown.
        /// </summary>
        /// <param name="toolName">Tool name from the catalog.</param>
        /// <param name="transport">Transport (http, tcp, websocket, stdio).</param>
        /// <param name="invocation">Invocation style (tool or method).</param>
        /// <param name="handler">The tool handler.</param>
        /// <returns>The handler's raw result.</returns>
        internal static async Task<object> InvokeToolAsync(string toolName, string transport, string invocation, Func<Task<object>> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = null;
            string outcome = McpTelemetryNames.OutcomeSuccess;
            string? errorType = null;

            try
            {
                bool isTool = invocation == McpTelemetryNames.InvocationTool;
                activity = Source.StartActivity(isTool ? "tools/call " + toolName : toolName, ActivityKind.Server);
                if (activity != null)
                {
                    activity.SetTag(McpTelemetryNames.AttributeMcpMethod, isTool ? "tools/call" : toolName);
                    activity.SetTag(McpTelemetryNames.AttributeToolName, toolName);
                    activity.SetTag(McpTelemetryNames.AttributeGenAiOperation, "execute_tool");
                    activity.SetTag(McpTelemetryNames.AttributeTransport, transport);
                    activity.SetTag(McpTelemetryNames.AttributeInvocation, invocation);
                }

                _ToolActive.Add(1, new KeyValuePair<string, object?>(McpTelemetryNames.AttributeTransport, transport));
            }
            catch (Exception)
            {
                // best-effort
            }

            try
            {
                object result = await handler().ConfigureAwait(false);

                if (result is RestMcpResponse response && !response.Success)
                {
                    outcome = McpTelemetryNames.OutcomeDownstreamError;
                    errorType = response.StatusCode > 0 ? response.StatusCode.ToString() : "_OTHER";
                }
                else if (result is McpToolCallResult toolResult && toolResult.IsError == true)
                {
                    outcome = McpTelemetryNames.OutcomeDownstreamError;
                    errorType = "tool_error";
                }

                return result;
            }
            catch (Exception e)
            {
                outcome = McpTelemetryNames.OutcomeError;
                errorType = ErrorType(e);
                MarkFailed(activity, e);
                throw;
            }
            finally
            {
                try
                {
                    double seconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;

                    TagList tags = new TagList();
                    tags.Add(McpTelemetryNames.AttributeToolName, toolName);
                    tags.Add(McpTelemetryNames.AttributeTransport, transport);
                    tags.Add(McpTelemetryNames.AttributeInvocation, invocation);
                    tags.Add(McpTelemetryNames.AttributeOutcome, outcome);
                    if (errorType != null) tags.Add(McpTelemetryNames.AttributeErrorType, errorType);

                    _ToolCalls.Add(1, tags);
                    _ToolDuration.Record(seconds, tags);
                    _ToolActive.Add(-1, new KeyValuePair<string, object?>(McpTelemetryNames.AttributeTransport, transport));

                    if (activity != null)
                    {
                        activity.SetTag(McpTelemetryNames.AttributeOutcome, outcome);
                        if (activity.Status == ActivityStatusCode.Unset)
                        {
                            if (outcome == McpTelemetryNames.OutcomeSuccess) activity.SetStatus(ActivityStatusCode.Ok);
                            else activity.SetStatus(ActivityStatusCode.Error, errorType);
                        }

                        if (errorType != null) activity.SetTag(McpTelemetryNames.AttributeErrorType, errorType);
                        activity.Dispose();
                    }
                }
                catch (Exception)
                {
                    // best-effort
                }
            }
        }

        /// <summary>
        /// Start the client span for a RestDb API call, named "restdb {METHOD} {route template}". Returns null when no
        /// listener is sampling.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="urlTemplate">Route template.</param>
        /// <param name="requestUri">Absolute request URI (host and port go on the span; the path does not).</param>
        /// <returns>Activity or null.</returns>
        internal static Activity? StartDownstreamActivity(HttpMethod method, string urlTemplate, Uri? requestUri)
        {
            try
            {
                Activity? activity = Source.StartActivity("restdb " + method.Method + " " + urlTemplate, ActivityKind.Client);
                if (activity != null)
                {
                    activity.SetTag(McpTelemetryNames.AttributeHttpMethod, method.Method);
                    activity.SetTag(McpTelemetryNames.AttributeUrlTemplate, urlTemplate);
                    if (requestUri != null)
                    {
                        activity.SetTag(McpTelemetryNames.AttributeServerAddress, requestUri.Host);
                        activity.SetTag(McpTelemetryNames.AttributeServerPort, requestUri.Port);
                    }
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Inject W3C trace context (traceparent, tracestate) for the current activity into an outbound request, so the
        /// RestDb server's Watson span joins the same trace.
        /// </summary>
        /// <param name="request">Outbound request.</param>
        internal static void InjectTraceContext(HttpRequestMessage request)
        {
            if (request == null) return;

            try
            {
                Activity? current = Activity.Current;
                if (current == null) return;

                DistributedContextPropagator.Current.Inject(current, request, static (carrier, key, value) =>
                {
                    if (carrier is HttpRequestMessage message && !String.IsNullOrEmpty(key))
                    {
                        message.Headers.Remove(key);
                        message.Headers.TryAddWithoutValidation(key, value);
                    }
                });
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a completed RestDb API call and finish its span.
        /// </summary>
        /// <param name="activity">Client span. May be null.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="urlTemplate">Route template.</param>
        /// <param name="statusCode">Status code, or 0 when no response arrived.</param>
        /// <param name="exception">Exception when the call failed without a response, otherwise null.</param>
        /// <param name="seconds">Duration in seconds.</param>
        internal static void CompleteDownstream(Activity? activity, HttpMethod method, string urlTemplate, int statusCode, Exception? exception, double seconds)
        {
            try
            {
                string outcome;
                string? errorType = null;

                if (exception != null)
                {
                    outcome = McpTelemetryNames.OutcomeError;
                    errorType = ErrorType(exception);
                    MarkFailed(activity, exception);
                }
                else if (statusCode >= 400)
                {
                    outcome = McpTelemetryNames.OutcomeDownstreamError;
                    errorType = statusCode.ToString();
                }
                else
                {
                    outcome = McpTelemetryNames.OutcomeSuccess;
                }

                TagList counterTags = new TagList();
                counterTags.Add(McpTelemetryNames.AttributeHttpMethod, method.Method);
                counterTags.Add(McpTelemetryNames.AttributeUrlTemplate, urlTemplate);
                counterTags.Add(McpTelemetryNames.AttributeOutcome, outcome);
                if (statusCode > 0) counterTags.Add(McpTelemetryNames.AttributeHttpStatusCode, statusCode);
                if (errorType != null) counterTags.Add(McpTelemetryNames.AttributeErrorType, errorType);
                _DownstreamRequests.Add(1, counterTags);

                TagList durationTags = new TagList();
                durationTags.Add(McpTelemetryNames.AttributeHttpMethod, method.Method);
                durationTags.Add(McpTelemetryNames.AttributeUrlTemplate, urlTemplate);
                durationTags.Add(McpTelemetryNames.AttributeOutcome, outcome);
                _DownstreamDuration.Record(seconds, durationTags);

                if (activity != null)
                {
                    if (statusCode > 0) activity.SetTag(McpTelemetryNames.AttributeHttpStatusCode, statusCode);
                    if (errorType != null) activity.SetTag(McpTelemetryNames.AttributeErrorType, errorType);

                    // Client spans are errors on any 4xx or 5xx (OpenTelemetry HTTP client convention).
                    if (activity.Status == ActivityStatusCode.Unset)
                    {
                        if (outcome == McpTelemetryNames.OutcomeSuccess) activity.SetStatus(ActivityStatusCode.Ok);
                        else activity.SetStatus(ActivityStatusCode.Error, errorType);
                    }

                    activity.Dispose();
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Count a client connection opening or closing on a transport.
        /// </summary>
        /// <param name="transport">Transport.</param>
        /// <param name="opened">True when a client connected, false when it disconnected.</param>
        internal static void RecordConnection(string transport, bool opened)
        {
            try
            {
                KeyValuePair<string, object?> tag = new KeyValuePair<string, object?>(McpTelemetryNames.AttributeTransport, transport);
                if (opened) _Connections.Add(1, tag);
                _ConnectionsActive.Add(opened ? 1 : -1, tag);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Return the bounded error type for an exception: its full type name.
        /// </summary>
        /// <param name="e">Exception. May be null.</param>
        /// <returns>Error type.</returns>
        internal static string ErrorType(Exception? e)
        {
            if (e == null) return "_OTHER";
            return e.GetType().FullName ?? e.GetType().Name;
        }

        #endregion

        #region Private-Methods

        private static void MarkFailed(Activity? activity, Exception e)
        {
            if (activity == null) return;

            try
            {
                string errorType = ErrorType(e);
                activity.SetStatus(ActivityStatusCode.Error, errorType);
                activity.SetTag(McpTelemetryNames.AttributeErrorType, errorType);
                ActivityTagsCollection tags = new ActivityTagsCollection
                {
                    { "exception.type", errorType },
                    { "exception.stacktrace", e.StackTrace ?? String.Empty }
                };
                activity.AddEvent(new ActivityEvent("exception", tags: tags));
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(McpTelemetry).Assembly;
                string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!String.IsNullOrWhiteSpace(version))
                {
                    int plus = version.IndexOf('+');
                    return plus > 0 ? version.Substring(0, plus) : version;
                }

                return assembly.GetName().Version?.ToString() ?? "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        #endregion
    }
}
