namespace RestDb.Telemetry
{
    using System;
    using Radiant;

    /// <summary>
    /// Owns the process's single Radiant host: subscribes to the RestDb, Watson, Npgsql, and MySqlConnector meters and
    /// the RestDb and Watson activity sources, exports over OTLP, and serves the Prometheus scrape endpoint.
    /// Start-up is best-effort: if the host cannot start (for example the scrape port is taken) RestDb keeps serving
    /// without telemetry export and <see cref="StartupError"/> says why. Dispose on shutdown to flush exporters.
    /// Thread safety: create and dispose from one thread; <see cref="IsRunning"/> is safe to read concurrently.
    /// </summary>
    internal sealed class TelemetryHost : IDisposable
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
        /// Why the host did not start, or null when it started or was disabled by configuration.
        /// </summary>
        internal string StartupError { get; private set; } = null;

        /// <summary>
        /// Prometheus scrape URL when the endpoint is enabled and running, otherwise null.
        /// </summary>
        internal string ScrapeUrl { get; private set; } = null;

        #endregion

        #region Private-Members

        private RadiantHost _Host = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        private TelemetryHost()
        {
        }

        /// <summary>
        /// Start the telemetry host. Never throws.
        /// </summary>
        /// <param name="settings">Telemetry settings. Null or disabled yields an inert host.</param>
        /// <returns>Telemetry host. Never null.</returns>
        internal static TelemetryHost Start(TelemetrySettings settings)
        {
            TelemetryHost ret = new TelemetryHost();
            if (settings == null || !settings.Enable) return ret;

            try
            {
                RadiantSettings radiant = BuildRadiantSettings(settings);
                ret._Host = RadiantHost.Start(radiant);
                if (settings.PrometheusEnable) ret.ScrapeUrl = radiant.Prometheus.ToScrapeUrl();
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
        /// Build the Radiant settings for the supplied telemetry settings.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <returns>Radiant settings.</returns>
        internal static RadiantSettings BuildRadiantSettings(TelemetrySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            RadiantSettings radiant = new RadiantSettings(settings.ServiceName);
            radiant.Sources.AddMeter(RestDbTelemetryNames.SourceName);
            radiant.Sources.AddMeter(RestDbTelemetryNames.WatsonSourceName);
            radiant.Sources.AddMeter(RestDbTelemetryNames.NpgsqlMeterName);
            radiant.Sources.AddMeter(RestDbTelemetryNames.MySqlConnectorMeterName);
            radiant.Sources.AddMeter(RestDbTelemetryNames.SyslogLoggingMeterName);
            radiant.Sources.AddActivitySource(RestDbTelemetryNames.SourceName);
            radiant.Sources.AddActivitySource(RestDbTelemetryNames.WatsonSourceName);

            radiant.Metrics.IncludeRuntime = true;
            radiant.Metrics.IncludeProcess = true;
            radiant.Traces.SamplingRatio = settings.TraceSamplingRatio;
            radiant.Traces.PropagateContext = true;

            // RestDb has no background work, so logs stay on the existing syslog/console module rather than an OTLP log pipeline.
            radiant.Logs.Enable = false;

            radiant.Otlp.Enable = settings.OtlpEnable;
            radiant.Otlp.Endpoint = settings.OtlpEndpoint;
            radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpProtocolEnum.HttpProtobuf
                : OtlpProtocolEnum.Grpc;

            radiant.Prometheus.Enable = settings.PrometheusEnable;
            radiant.Prometheus.Hostname = settings.PrometheusHostname;
            radiant.Prometheus.Port = settings.PrometheusPort;
            radiant.Prometheus.Path = "/metrics";
            return radiant;
        }

        /// <summary>
        /// Flush and dispose the Radiant host, releasing the scrape port. Never throws.
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
