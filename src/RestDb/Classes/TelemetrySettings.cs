namespace RestDb
{
    using System;

    /// <summary>
    /// Telemetry settings (the Telemetry section of restdb.json). RestDb always emits metrics and traces through the
    /// .NET Meter and ActivitySource named "RestDb" (plus Watson's "Watson" HTTP instrumentation); these settings control
    /// the in-process Radiant host that collects them and exports traces over OTLP and metrics over a Prometheus scrape
    /// endpoint. Defaults bind loopback only (127.0.0.1).
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the telemetry host is started. Default true. When false nothing is collected or exported, and
        /// instrumentation remains effectively free.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Service name stamped as service.name on every metric and span. Default restdb.
        /// </summary>
        public string ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                _ServiceName = String.IsNullOrWhiteSpace(value) ? "restdb" : value;
            }
        }

        /// <summary>
        /// Whether traces (and metrics) are pushed to an OTLP collector such as Tempo or an OpenTelemetry Collector. Default true.
        /// </summary>
        public bool OtlpEnable { get; set; } = true;

        /// <summary>
        /// OTLP collector endpoint. Use the gRPC port (4317) with protocol "grpc", or the HTTP port (4318) with protocol
        /// "httpprotobuf". Default http://127.0.0.1:4317.
        /// </summary>
        public string OtlpEndpoint
        {
            get
            {
                return _OtlpEndpoint;
            }
            set
            {
                _OtlpEndpoint = String.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:4317" : value;
            }
        }

        /// <summary>
        /// OTLP protocol: "grpc" (default) or "httpprotobuf".
        /// </summary>
        public string OtlpProtocol
        {
            get
            {
                return _OtlpProtocol;
            }
            set
            {
                _OtlpProtocol = String.IsNullOrWhiteSpace(value) ? "grpc" : value;
            }
        }

        /// <summary>
        /// Whether the in-process Prometheus scrape endpoint is served. Default true. It listens on its own port, not the
        /// API listener, and is anonymous by design: keep it on an internal network.
        /// </summary>
        public bool PrometheusEnable { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus endpoint binds. Default 127.0.0.1. The listener only answers requests whose Host header
        /// matches this value, so inside a container use the DNS name the scraper targets (for example "restdb" in Docker
        /// Compose). Wildcards (+, *) and 0.0.0.0 are rejected by the underlying exporter.
        /// </summary>
        public string PrometheusHostname
        {
            get
            {
                return _PrometheusHostname;
            }
            set
            {
                _PrometheusHostname = String.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value;
            }
        }

        /// <summary>
        /// Port of the Prometheus endpoint. Default 9464. Minimum 1, maximum 65535.
        /// </summary>
        public int PrometheusPort
        {
            get
            {
                return _PrometheusPort;
            }
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(PrometheusPort), "PrometheusPort must be between 1 and 65535.");
                _PrometheusPort = value;
            }
        }

        /// <summary>
        /// Head-based trace sampling ratio. Default 1.0 (sample everything). Minimum 0.0 (sample nothing new), maximum 1.0.
        /// Parent-based: a sampled inbound traceparent is always honored.
        /// </summary>
        public double TraceSamplingRatio
        {
            get
            {
                return _TraceSamplingRatio;
            }
            set
            {
                if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(TraceSamplingRatio), "TraceSamplingRatio must be between 0.0 and 1.0.");
                _TraceSamplingRatio = value;
            }
        }

        #endregion

        #region Private-Members

        private string _ServiceName = "restdb";
        private string _OtlpEndpoint = "http://127.0.0.1:4317";
        private string _OtlpProtocol = "grpc";
        private string _PrometheusHostname = "127.0.0.1";
        private int _PrometheusPort = 9464;
        private double _TraceSamplingRatio = 1.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetrySettings()
        {

        }

        #endregion
    }
}
