namespace RestDb.McpServer.Telemetry
{
    using System;
    using Radiant;
    using RestDb.McpServer.Classes;

    /// <summary>
    /// Owns the process's single Radiant host: subscribes to the RestDb.McpServer and System.Net.Http meters and the
    /// RestDb.McpServer activity source, exports over OTLP, and optionally serves a Prometheus scrape endpoint.
    /// Start-up is best-effort: when the host cannot start the server runs without telemetry export and
    /// <see cref="StartupError"/> says why. Radiant never writes to the console, so the stdio transport stays clean.
    /// Dispose on shutdown to flush exporters.
    /// Thread safety: create and dispose from one thread.
    /// </summary>
    internal sealed class McpTelemetryHost : IDisposable
    {
        #region Internal-Members

        /// <summary>
        /// True when a live Radiant pipeline is running.
        /// </summary>
        internal bool IsRunning
        {
            get
            {
                return _Host != null && _Host.IsEnabled;
            }
        }

        /// <summary>
        /// Why the host did not start, or null.
        /// </summary>
        internal string? StartupError { get; private set; } = null;

        /// <summary>
        /// Prometheus scrape URL when enabled and running, otherwise null.
        /// </summary>
        internal string? ScrapeUrl { get; private set; } = null;

        #endregion

        #region Private-Members

        private RadiantHost? _Host = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        private McpTelemetryHost()
        {
        }

        /// <summary>
        /// Start the telemetry host. Never throws.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <returns>Telemetry host. Never null.</returns>
        internal static McpTelemetryHost Start(RestMcpServerSettings settings)
        {
            McpTelemetryHost ret = new McpTelemetryHost();
            if (settings == null) return ret;

            McpTelemetry.UpdateConfigurationState(settings);
            if (!settings.TelemetryEnable) return ret;

            try
            {
                RadiantSettings radiant = BuildRadiantSettings(settings);
                ret._Host = RadiantHost.Start(radiant);
                if (radiant.Prometheus.Enable) ret.ScrapeUrl = radiant.Prometheus.ToScrapeUrl();
            }
            catch (Exception e)
            {
                ret._Host = null;
                Exception root = e;
                while (root.InnerException != null) root = root.InnerException;
                ret.StartupError = root == e
                    ? e.GetType().Name + ": " + e.Message
                    : e.GetType().Name + ": " + e.Message + " Cause: " + root.GetType().Name + ": " + root.Message;
            }

            return ret;
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Build the Radiant settings for the supplied server settings.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <returns>Radiant settings.</returns>
        internal static RadiantSettings BuildRadiantSettings(RestMcpServerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            RadiantSettings radiant = new RadiantSettings(settings.TelemetryServiceName);
            radiant.Sources.AddMeter(McpTelemetryNames.SourceName);
            radiant.Sources.AddMeter(McpTelemetryNames.HttpClientMeterName);
            radiant.Sources.AddMeter(McpTelemetryNames.VoltaicSourceName);
            radiant.Sources.AddActivitySource(McpTelemetryNames.SourceName);
            radiant.Sources.AddActivitySource(McpTelemetryNames.VoltaicSourceName);

            radiant.Metrics.IncludeRuntime = true;
            radiant.Metrics.IncludeProcess = true;
            radiant.Traces.PropagateContext = true;
            radiant.Logs.Enable = false;

            radiant.Otlp.Enable = settings.OtlpEnable;
            radiant.Otlp.Endpoint = settings.OtlpEndpoint;
            radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpProtocolEnum.HttpProtobuf
                : OtlpProtocolEnum.Grpc;

            radiant.Prometheus.Enable = settings.EffectivePrometheusEnable;
            radiant.Prometheus.Hostname = settings.PrometheusHostname;
            radiant.Prometheus.Port = settings.PrometheusPort;
            radiant.Prometheus.Path = "/metrics";
            return radiant;
        }

        /// <summary>
        /// Flush and dispose the Radiant host. Never throws.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                _Host?.Dispose();
            }
            catch (Exception)
            {
                // best-effort
            }

            _Host = null;
        }

        #endregion
    }
}
