# Telemetry

RestDb and RestDb.McpServer ship with built-in observability. Both processes emit metrics and traces through the .NET base class library (`System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`), and each process hosts one [Radiant](https://www.nuget.org/packages/Radiant) host that collects them, exports traces (and metrics) over OTLP, and serves a Prometheus scrape endpoint. The bundled Docker Compose stack adds Prometheus, Tempo, and Grafana with five provisioned dashboards.

The goal is operational: using only the dashboards and traces, an on-call engineer can tell where the time went and what failed, without reading the source or attaching a debugger.

## Contents

- [What is instrumented](#what-is-instrumented)
- [Sources](#sources)
- [Enabling and configuring](#enabling-and-configuring)
- [Subscribing from your own host](#subscribing-from-your-own-host)
- [Metrics catalog: RestDb](#metrics-catalog-restdb)
- [Metrics catalog: RestDb.McpServer](#metrics-catalog-restdbmcpserver)
- [Metrics from Watson, drivers, and the runtime](#metrics-from-watson-drivers-and-the-runtime)
- [Spans catalog](#spans-catalog)
- [Label values](#label-values)
- [Observability stack](#observability-stack)
- [Dashboard map](#dashboard-map)
- [Recommended alerts](#recommended-alerts)
- [Privacy and cardinality rules](#privacy-and-cardinality-rules)
- [Known limits](#known-limits)

## What is instrumented

| Area | Where | Telemetry |
| --- | --- | --- |
| HTTP layer | Watson 7.2 webserver | Watson's built-in `http.server.*` and `watson.*` metrics and one server span per request. RestDb routes every request through Watson's default route, so RestDb names Watson's span with the route template (`GET /{database}/{table}`) and adds `restdb.operation`. |
| API operations | `src/RestDb/RestDbServer.cs` dispatch, `RestDbServer.Telemetry.cs` | Per-operation counter, duration histogram, in-flight gauge, error counter, and an `api {operation}` span for all 24 operations. |
| Workflow stages | `DatabaseManager`, storage `Implementations`, handlers, configuration | Per-stage counter and histogram plus a `stage:{name}` span: `resolve_table`, `describe`, `parse_request`, `query`, `enrich_context`, `serialize`, `apply_config`. |
| Database calls | `src/RestDb/Storage/AdoDatabaseDriverBase.cs` | Client span per call, operation counter and histogram by system, database, and operation, connection-open histogram (includes pool wait), rows returned, transaction commits and rollbacks, provider error type and SQLSTATE. |
| Authentication | `src/RestDb/Classes/AuthManager.cs` | Decision counter by outcome and credential type, stamped on the request span. |
| Configuration lifecycle | `src/RestDb/RestDbServer.Configuration.cs` | Change counter and histogram for start-up, reload, and update of settings and context; last-change timestamps; restart-required flag; safe configuration gauges. |
| Domain | `src/RestDb/Storage/Implementations/RecordMethods.cs` | Records written. |
| MCP tool calls | `src/RestDb.McpServer/Classes/RestMcpToolRegistrar.cs` | Root server span per tool call, counter and histogram by tool, transport, invocation style, and outcome, in-flight gauge. |
| MCP to RestDb calls | `src/RestDb.McpServer/Classes/RestMcpRestProxy.cs` | Client span per RestDb request with W3C `traceparent` propagation, counter and histogram by method, route template, and outcome. |
| MCP connections | `src/RestDb.McpServer/RestMcpServer.cs` | Connection counter and open-connection gauge per transport. |
| Build and runtime | both processes | Build-info gauges, .NET runtime and process metrics (Radiant), driver pool metrics (Npgsql, MySqlConnector), HttpClient pool metrics (`System.Net.Http`). |

RestDb has no background workers or queues, so logs stay on the existing syslog and console logging module and are not shipped to Loki. Failed requests log their `trace_id` so a log line leads straight to the trace.

## Sources

| Process | Meter names subscribed | Activity sources subscribed | Default service name |
| --- | --- | --- | --- |
| RestDb | `RestDb`, `Watson`, `Npgsql`, `MySqlConnector`, plus Radiant runtime and process meters | `RestDb`, `Watson` | `restdb` |
| RestDb.McpServer | `RestDb.McpServer`, `System.Net.Http`, plus Radiant runtime and process meters | `RestDb.McpServer` | `restdb-mcp` |

All names live in one constants class per project: `RestDb.Telemetry.RestDbTelemetryNames` and `RestDb.McpServer.Telemetry.McpTelemetryNames`. Treat them as public contract.

## Enabling and configuring

Telemetry is on by default and best-effort: if the host cannot start (for example the scrape port is taken) the service keeps serving, logs the reason, and the instrumentation stays inert. Instruments cost effectively nothing when nothing is listening.

### RestDb (`restdb.json`)

```json
"Telemetry": {
  "Enable": true,
  "ServiceName": "restdb",
  "OtlpEnable": true,
  "OtlpEndpoint": "http://127.0.0.1:4317",
  "OtlpProtocol": "grpc",
  "PrometheusEnable": true,
  "PrometheusHostname": "127.0.0.1",
  "PrometheusPort": 9464,
  "TraceSamplingRatio": 1.0
}
```

| Key | Default | Meaning |
| --- | --- | --- |
| `Enable` | `true` | Start the telemetry host. `false` collects and exports nothing. |
| `ServiceName` | `restdb` | `service.name` on every metric and span. |
| `OtlpEnable` | `true` | Push traces and metrics to an OTLP collector. |
| `OtlpEndpoint` | `http://127.0.0.1:4317` | Collector endpoint. Use 4317 with `grpc`, 4318 with `httpprotobuf`. |
| `OtlpProtocol` | `grpc` | `grpc` or `httpprotobuf`. |
| `PrometheusEnable` | `true` | Serve `GET /metrics` on its own port (not the API port). Anonymous: keep it on an internal network. |
| `PrometheusHostname` | `127.0.0.1` | Host to bind. The listener only answers requests whose `Host` header matches, so in a container use the DNS name the scraper targets (`restdb` in Compose). `+`, `*`, and `0.0.0.0` are rejected by the exporter. |
| `PrometheusPort` | `9464` | Scrape port, 1 to 65535. |
| `TraceSamplingRatio` | `1.0` | Head sampling ratio 0.0 to 1.0, parent-based. |

Telemetry settings take effect at process start. Existing `restdb.json` files without a `Telemetry` section get the defaults. Watson's own telemetry (`Settings.Telemetry.Enable`, `EnableMetrics`, `EnableTraces`, `PropagateContext`) is set explicitly to on at start-up.

### RestDb.McpServer (environment variables or flags)

| Environment variable | Flag | Default | Meaning |
| --- | --- | --- | --- |
| `RESTDB_MCP_TELEMETRY_ENABLE` | `--no-telemetry` | `true` | Start the telemetry host. |
| `RESTDB_MCP_TELEMETRY_SERVICE_NAME` | | `restdb-mcp` | `service.name`. |
| `RESTDB_MCP_OTLP_ENABLE` | `--no-otlp` | `true` | Push to an OTLP collector. |
| `RESTDB_MCP_OTLP_ENDPOINT` | `--otlp-endpoint <url>` | `http://127.0.0.1:4317` | Collector endpoint. |
| `RESTDB_MCP_OTLP_PROTOCOL` | | `grpc` | `grpc` or `httpprotobuf`. |
| `RESTDB_MCP_PROMETHEUS_ENABLE` | `--no-prometheus` | on in network mode, off with `--stdio` | Serve `GET /metrics`. Off for stdio because agent clients may start several stdio instances that would contend for one port. `--prometheus-port` or `--prometheus-host` turn it on explicitly. |
| `RESTDB_MCP_PROMETHEUS_HOST` | `--prometheus-host <host>` | `127.0.0.1` | Host to bind (same `Host` header rule as RestDb; `mcp` in Compose). |
| `RESTDB_MCP_PROMETHEUS_PORT` | `--prometheus-port <port>` | `9465` | Scrape port. |

Radiant never writes to the console, so the stdio transport stays clean.

## Subscribing from your own host

The instrumentation is plain BCL, so any listener can consume it. With Radiant:

```csharp
RadiantSettings settings = new RadiantSettings("my-host");
settings.Sources.AddMeter("RestDb");
settings.Sources.AddMeter("Watson");
settings.Sources.AddActivitySource("RestDb");
settings.Sources.AddActivitySource("Watson");
using (RadiantHost host = RadiantHost.Start(settings)) { /* ... */ }
```

With the OpenTelemetry SDK directly: `.AddMeter("RestDb", "Watson")` and `.AddSource("RestDb", "Watson")` (or `RestDb.McpServer` and `System.Net.Http` for the MCP server). In tests, a `MeterListener` and an `ActivityListener` on the same names are enough (see `src/RestDb.Test.Shared/TelemetryCapture.cs`).

Histograms carry bucket advice in seconds (`0.0005` to `60`), so exporters that honor instrument advice use sensible boundaries without views. Quantiles are computed in Grafana from buckets, never in-process.

## Metrics catalog: RestDb

Prometheus names are shown as the OpenTelemetry Prometheus exporter writes them (dots become underscores; counters gain `_total`; seconds gain `_seconds`). All instruments are on the `RestDb` meter.

| Instrument | Prometheus name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- | --- |
| `restdb.api.requests` | `restdb_api_requests_total` | Counter | `{request}` | `restdb_operation`, `outcome`, `http_response_status_code` | API operations handled. |
| `restdb.api.request.duration` | `restdb_api_request_duration_seconds` | Histogram | `s` | `restdb_operation`, `outcome` | API operation duration. |
| `restdb.api.errors` | `restdb_api_errors_total` | Counter | `{error}` | `restdb_operation`, `error_type` | Failed operations; `error_type` is the exception type for 500s or the status code for handled 4xx and 5xx. |
| `restdb.api.active_requests` | `restdb_api_active_requests` | UpDownCounter | `{request}` | `restdb_operation` | Operations in flight. |
| `restdb.api.stages` | `restdb_api_stages_total` | Counter | `{stage}` | `restdb_operation`, `restdb_stage`, `outcome` | Workflow stage executions. |
| `restdb.api.stage.duration` | `restdb_api_stage_duration_seconds` | Histogram | `s` | `restdb_operation`, `restdb_stage`, `outcome` | Workflow stage duration. Stages can nest (`describe` runs inside `enrich_context`). |
| `restdb.auth.decisions` | `restdb_auth_decisions_total` | Counter | `{decision}` | `restdb_auth_outcome`, `restdb_auth_credential_type` | Authentication decisions. |
| `restdb.db.client.operations` | `restdb_db_client_operations_total` | Counter | `{operation}` | `db_system_name`, `db_namespace`, `db_operation_name`, `outcome`, `error_type` | Database calls. |
| `restdb.db.client.operation.duration` | `restdb_db_client_operation_duration_seconds` | Histogram | `s` | `db_system_name`, `db_namespace`, `db_operation_name`, `outcome`, `error_type` | Database call duration, connection open included. |
| `restdb.db.client.connection.open.duration` | `restdb_db_client_connection_open_duration_seconds` | Histogram | `s` | `db_system_name`, `db_namespace`, `outcome` | Connection open time including provider pool wait. |
| `restdb.db.client.rows_returned` | `restdb_db_client_rows_returned_total` | Counter | `{row}` | `db_system_name`, `db_namespace`, `db_operation_name` | Rows read back from the database. |
| `restdb.db.client.transactions` | `restdb_db_client_transactions_total` | Counter | `{transaction}` | `db_system_name`, `db_namespace`, `outcome` (`commit`, `rollback`) | Batch transactions (inserts). |
| `restdb.records.written` | `restdb_records_written_total` | Counter | `{record}` | `db_system_name`, `db_namespace` | Records inserted through the record APIs. |
| `restdb.config.changes` | `restdb_config_changes_total` | Counter | `{change}` | `restdb_config_kind`, `restdb_config_action`, `outcome` | Settings and context start-up, reload, and update. |
| `restdb.config.change.duration` | `restdb_config_change_duration_seconds` | Histogram | `s` | `restdb_config_kind`, `restdb_config_action`, `outcome` | Configuration change duration (settings changes reinitialize every database). |
| `restdb.config.last_change.timestamp` | `restdb_config_last_change_timestamp_seconds` | Gauge | `s` | `restdb_config_kind` | Unix time of the last successful load. |
| `restdb.config.restart_required` | `restdb_config_restart_required` | Gauge | | | 1 after a listener host, port, or SSL change that needs a restart. |
| `restdb.config.databases` | `restdb_config_databases` | Gauge | `{database}` | `db_system_name` | Configured databases. |
| `restdb.config.api_keys` | `restdb_config_api_keys` | Gauge | `{key}` | | Configured API keys (count only). |
| `restdb.config.authentication_required` | `restdb_config_authentication_required` | Gauge | | | 1 when authentication is required. |
| `restdb.build.info` | `restdb_build_info` | Gauge | | `service_version` | Always 1. |

## Metrics catalog: RestDb.McpServer

All instruments are on the `RestDb.McpServer` meter.

| Instrument | Prometheus name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- | --- |
| `restdb.mcp.tool.calls` | `restdb_mcp_tool_calls_total` | Counter | `{call}` | `gen_ai_tool_name`, `restdb_mcp_transport`, `restdb_mcp_invocation`, `outcome`, `error_type` | Tool invocations. |
| `restdb.mcp.tool.duration` | `restdb_mcp_tool_duration_seconds` | Histogram | `s` | same as above | Tool invocation duration. |
| `restdb.mcp.tool.active` | `restdb_mcp_tool_active` | UpDownCounter | `{call}` | `restdb_mcp_transport` | Tool calls in flight. |
| `restdb.mcp.downstream.requests` | `restdb_mcp_downstream_requests_total` | Counter | `{request}` | `http_request_method`, `url_template`, `outcome`, `http_response_status_code`, `error_type` | Requests to the RestDb API. |
| `restdb.mcp.downstream.duration` | `restdb_mcp_downstream_duration_seconds` | Histogram | `s` | `http_request_method`, `url_template`, `outcome` | RestDb API latency seen by the MCP server. |
| `restdb.mcp.connections` | `restdb_mcp_connections_total` | Counter | `{connection}` | `restdb_mcp_transport` | Client connections accepted (Voltaic connect events). |
| `restdb.mcp.connections.active` | `restdb_mcp_connections_active` | UpDownCounter | `{connection}` | `restdb_mcp_transport` | Client connections open. |
| `restdb.mcp.build.info` | `restdb_mcp_build_info` | Gauge | | `service_version`, `restdb_mcp_mode` | Always 1. |
| `restdb.mcp.config.token_required` | `restdb_mcp_config_token_required` | Gauge | | | 1 when an MCP bearer token is required (the token itself is never exported). |
| `restdb.mcp.config.allowed_origins` | `restdb_mcp_config_allowed_origins` | Gauge | `{origin}` | | Extra browser origins allowed. |

## Metrics from Watson, drivers, and the runtime

| Source | Examples | Notes |
| --- | --- | --- |
| Watson (`Watson` meter) | `http_server_request_duration_seconds`, `http_server_active_requests`, `http_server_request_body_size_bytes`, `http_server_response_body_size_bytes`, `watson_server_up`, `watson_server_uptime_seconds`, `watson_server_connections_active`, `watson_server_exceptions_total` | Labels `http_request_method`, `http_response_status_code`. No `http_route` label because RestDb uses Watson's default route; use `restdb_api_*` for per-endpoint views. See Watson's TELEMETRY.md. |
| Npgsql (`Npgsql` meter) | `db_client_connection_count` (`db_client_connection_state`, `db_client_connection_pool_name`) | PostgreSQL pool usage. Present only with a PostgreSQL database configured. |
| MySqlConnector (`MySqlConnector` meter) | `db_client_connections_usage` (`state`, `pool_name`), `db_client_connections_pending_requests` | MySQL pool usage and waiters. |
| HttpClient (`System.Net.Http` meter, MCP server) | `http_client_request_duration_seconds`, `http_client_open_connections`, `http_client_request_time_in_queue_seconds` | MCP to RestDb connection pool. |
| Radiant process | `process_memory_usage_bytes`, `process_thread_count`, `process_uptime_seconds` | Both processes. |
| .NET runtime | net8.0: `process_runtime_dotnet_gc_collections_count_total`, `process_runtime_dotnet_gc_heap_size_bytes`, `process_runtime_dotnet_thread_pool_queue_length`, ... ; net9.0 and later: `dotnet_gc_collections_total`, `dotnet_gc_last_collection_heap_size_bytes`, `dotnet_thread_pool_queue_length_total`, ... | The runtime instrumentation switches to the built-in `System.Runtime` meter on .NET 9 and later. The dashboards query both families with `or`. |

## Spans catalog

| Span name | Kind | Source | Parent | Key attributes |
| --- | --- | --- | --- | --- |
| `{METHOD} {route template}` (for example `GET /{database}/{table}`) | Server | Watson | inbound `traceparent`, else root | `http.request.method`, `http.route`, `http.response.status_code`, `restdb.operation`, `restdb.auth.outcome`, `restdb.auth.credential_type`, plus Watson's client and size attributes |
| `api {operation}` (for example `api table.select`) | Internal | RestDb | Watson span | `restdb.operation`, `http.route`, `http.response.status_code`, `restdb.request.database`, `restdb.request.table`, `error.type` |
| `stage:{stage}` (for example `stage:resolve_table`) | Internal | RestDb | operation or enclosing stage | `restdb.stage`, `restdb.operation`, `error.type` |
| `{db.system.name} {db.operation.name}` (for example `sqlite select`, `postgresql insert_multiple`) | Client | RestDb | stage span | `db.system.name`, `db.namespace`, `db.operation.name`, `server.address`, `server.port`, `db.operation.batch.size`, `db.response.returned_rows`, `restdb.db.connect.duration`, `db.transaction.outcome`, `db.response.status_code` (SQLSTATE or provider code), `error.type` |
| `tools/call {tool}` or `{tool}` (direct JSON-RPC method) | Server | RestDb.McpServer | root (MCP transports carry no trace context) | `mcp.method.name`, `gen_ai.tool.name`, `gen_ai.operation.name`, `restdb.mcp.transport`, `restdb.mcp.invocation`, `outcome`, `error.type` |
| `restdb {METHOD} {route template}` | Client | RestDb.McpServer | tool span | `http.request.method`, `url.template`, `server.address`, `server.port`, `http.response.status_code`, `error.type` |

CORS preflight requests (`OPTIONS`) are answered by Watson's preflight hook and appear as `OPTIONS {route template}` Watson spans with no `api` span.

Span status is set explicitly: `Error` on exceptions and 5xx (and, for client spans, any 4xx), `Ok` otherwise. Failures attach an `exception` event with `exception.type` and `exception.stacktrace`. Start-up database checks appear as `{system} initialize` spans.

Because the MCP proxy injects `traceparent`, one MCP tool call is one trace across both services:

```
tools/call restdb_inspect_database              restdb-mcp
  restdb GET /{database}                        restdb-mcp
    GET /{database}                             restdb (Watson)
      api database.read                         restdb
        stage:describe                          restdb
          sqlite list_tables                    restdb
```

## Label values

| Label | Values |
| --- | --- |
| `restdb_operation` | `startup`, `root`, `static`, `unknown`, `database_clients.list`, `databases.list`, `database.read`, `settings.read`, `settings.update`, `settings.reload`, `context.read`, `context.update`, `context.reload`, `database_context.read`, `database_context.update`, `table_context.read`, `table_context.update`, `table.select`, `table.describe`, `table.search`, `table.update`, `table.create`, `table.insert`, `table.delete`, `table.truncate`, `table.drop`, `raw_query` |
| `restdb_stage` | `resolve_table`, `describe`, `parse_request`, `query`, `enrich_context`, `serialize`, `apply_config` |
| `outcome` | `success`, `client_error`, `error` (API and stages); `success`, `error` (database); `commit`, `rollback` (transactions); `success`, `downstream_error`, `error` (MCP) |
| `db_system_name` | `sqlite`, `postgresql`, `mysql`, `microsoft.sql_server` |
| `db_operation_name` | `initialize`, `list_tables`, `describe_table`, `select`, `insert`, `insert_multiple`, `update`, `delete`, `create_table`, `clear_table`, `drop_table`, `raw` |
| `db_namespace` | configured database names from `restdb.json` (never request input) |
| `restdb_auth_outcome` | `allowed`, `missing_credentials`, `unknown_key`, `method_not_permitted`, `no_keys_configured` |
| `restdb_auth_credential_type` | `api_key_header`, `bearer`, `none` |
| `restdb_config_kind` / `restdb_config_action` | `settings`, `context` / `startup`, `reload`, `update` |
| `restdb_mcp_transport` | `http`, `tcp`, `websocket`, `stdio` |
| `restdb_mcp_invocation` | `tool`, `method` |
| `url_template` | `/`, `/_databases`, `/_databaseclients`, `/_settings`, `/_settings/reload`, `/_context`, `/_context/reload`, `/_context/{database}`, `/_context/{database}/{table}`, `/{database}`, `/{database}/{table}`, `/{database}/{table}/{id}`, `/(other)` |
| `error_type` | exception full type name (for example `Microsoft.Data.Sqlite.SqliteException`, `System.Net.Http.HttpRequestException`) or an HTTP status code |

## Observability stack

`Docker/compose.yaml` (and the mirrored `Docker/factory/compose.yaml`) brings up the application plus:

| Service | Image | Host port | Role |
| --- | --- | --- | --- |
| Prometheus | `prom/prometheus:v3.5.4` | 9090 | Scrapes `restdb:9464` and `mcp:9465` (compose network only). |
| Tempo | `grafana/tempo:2.6.1` | 3200, 4317, 4318 | Receives OTLP traces, serves the query API. |
| Grafana | `grafana/grafana-oss:13.0.2` | 3000 | Provisioned datasources (`prometheus`, `tempo` UIDs) and the `RestDb` dashboard folder from `assets/grafana`. |

Every host port can be changed with an environment variable (`RESTDB_PROMETHEUS_HOST_PORT`, `RESTDB_TEMPO_HOST_PORT`, `RESTDB_OTLP_GRPC_HOST_PORT`, `RESTDB_OTLP_HTTP_HOST_PORT`, `RESTDB_GRAFANA_HOST_PORT`, plus the existing RestDb, dashboard, and MCP ports). Healthchecks gate start-up: RestDb and the MCP server wait for Tempo, and Grafana waits for Prometheus and Tempo.

Notes:

- `Docker/prometheus.yaml` scrapes with `PrometheusText0.0.4` and `metric_name_validation_scheme: legacy`. Prometheus 3 otherwise negotiates OpenMetrics with UTF-8 names, and the OpenTelemetry exporter would then expose dotted names (`restdb.api.requests`) that the dashboards and alerts do not use.
- Tempo accepts traces only. Radiant pushes metrics and traces to the same OTLP endpoint, so metric pushes to Tempo are refused and dropped; Prometheus scraping is the metrics path. Point `OtlpEndpoint` at an OpenTelemetry Collector to route OTLP metrics to another backend.
- Grafana uses `admin` / `admin` locally. Set `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` for any shared or hosted deployment, and do not publish Prometheus, Tempo, or the scrape ports on a public interface.

The RestDb dashboard's workspace page ends with an External services card that links to Grafana, Prometheus, Tempo, and the MCP endpoint, shows Grafana's default credentials, and probes each service from the browser. Override the URLs at build time with `VITE_GRAFANA_URL`, `VITE_PROMETHEUS_URL`, `VITE_TEMPO_URL`, `VITE_MCP_URL`, and the credential text with `VITE_GRAFANA_CREDENTIALS`.

## Dashboard map

Dashboards live in `assets/grafana/` and load into the Grafana folder `RestDb`. Each links to the others.

| Dashboard | UID | Use it to answer |
| --- | --- | --- |
| RestDb / Overview | `restdb-overview` | Is RestDb up? Traffic, 5xx ratio, API, database, and MCP p95, auth denials, recent failed and slow traces (Tempo), running versions. Start here. |
| RestDb / API | `restdb-api` | Which endpoint is slow or failing? (In Compose, most `root` traffic is the container healthcheck probing `GET /`.) Watson HTTP rate, latency, body sizes, exceptions; per-operation rate, p95, slowest operations, outcomes and status codes, errors by type; per-stage p95, stage time share, stage failures. Filter by operation. |
| RestDb / Database | `restdb-database` | Is the database the cause? Operation rate, p95, errors by type, slowest calls, connection-open p95 (pool waits), open failures, transactions, rows, driver pools, initialization failures. Filter by database. |
| RestDb / MCP | `restdb-mcp` | Are agents succeeding? Tool rate, error ratio, p95 by tool, failures by type, transport split, downstream RestDb calls by route, open connections, HttpClient pool. Filter by tool. |
| RestDb / Runtime and Configuration | `restdb-runtime` | What changed? Restart required, time since settings and context changes, change history and duration, auth decisions, memory, GC, exceptions, thread pool, lock contention, Watson connections. |

## Recommended alerts

```yaml
groups:
  - name: restdb
    rules:
      - alert: RestDbDown
        expr: up{job="restdb"} == 0
        for: 2m
        labels: { severity: critical }
        annotations: { summary: "RestDb metrics endpoint is not answering" }

      - alert: RestDbHighErrorRatio
        expr: |
          sum(rate(restdb_api_requests_total{outcome="error"}[5m]))
            / clamp_min(sum(rate(restdb_api_requests_total[5m])), 1e-9) > 0.05
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "More than 5% of RestDb API operations fail with 5xx" }

      - alert: RestDbSlowOperation
        expr: |
          histogram_quantile(0.95, sum by (le, restdb_operation) (rate(restdb_api_request_duration_seconds_bucket[5m]))) > 2
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "p95 of {{ $labels.restdb_operation }} is above 2s" }

      - alert: RestDbDatabaseErrors
        expr: sum by (db_namespace, error_type) (rate(restdb_db_client_operations_total{outcome="error",db_operation_name!="raw"}[5m])) > 0
        for: 5m
        labels: { severity: warning }
        annotations: { summary: "Database {{ $labels.db_namespace }} is failing with {{ $labels.error_type }}" }

      - alert: RestDbConnectionPoolPressure
        expr: |
          histogram_quantile(0.95, sum by (le, db_namespace) (rate(restdb_db_client_connection_open_duration_seconds_bucket[5m]))) > 0.5
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "Opening connections to {{ $labels.db_namespace }} takes over 500ms (pool exhaustion or network)" }

      - alert: RestDbAuthFailureSpike
        expr: sum(rate(restdb_auth_decisions_total{restdb_auth_outcome=~"unknown_key|missing_credentials"}[5m])) > 1
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "Sustained authentication failures (misconfigured client or credential guessing)" }

      - alert: RestDbRestartRequired
        expr: max(restdb_config_restart_required) == 1
        for: 30m
        labels: { severity: info }
        annotations: { summary: "A listener settings change is waiting for a RestDb restart" }

      - alert: RestDbConfigChangeFailed
        expr: sum(increase(restdb_config_changes_total{outcome="error"}[15m])) > 0
        labels: { severity: warning }
        annotations: { summary: "A settings or context reload or update failed" }

      - alert: RestDbMcpDown
        expr: up{job="restdb-mcp"} == 0
        for: 2m
        labels: { severity: critical }
        annotations: { summary: "RestDb.McpServer metrics endpoint is not answering" }

      - alert: RestDbMcpToolFailures
        expr: |
          sum by (gen_ai_tool_name) (rate(restdb_mcp_tool_calls_total{outcome="error"}[10m])) > 0
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "MCP tool {{ $labels.gen_ai_tool_name }} is throwing (handler or network failures)" }

      - alert: RestDbMcpCannotReachRestDb
        expr: sum(rate(restdb_mcp_downstream_requests_total{error_type=~".*HttpRequestException|.*TaskCanceledException"}[5m])) > 0
        for: 5m
        labels: { severity: critical }
        annotations: { summary: "The MCP server cannot reach the RestDb API" }
```

`raw_query` failures are excluded from the database error alert because they usually reflect caller SQL, not a broken database; watch them on the API dashboard instead.

## Privacy and cardinality rules

- Metric labels are bounded: operation, stage, outcome, status code, database system, configured database name, route template, tool name, transport, and exception type. Request database names, table names, ids, query strings, and principals never become labels.
- Request database and table names appear only as span attributes (`restdb.request.database`, `restdb.request.table`).
- SQL text, parameter values, row data, request and response bodies, API keys, bearer tokens, the MCP token, and database passwords are never recorded anywhere. Exception messages are not recorded either, because database errors can echo row values; the exception type, stack trace, and SQLSTATE identify the failure, and the HTTP response and server log carry the message.
- Instrumentation never throws into request handling and never blocks on export.

## Known limits

- Watson's HTTP metrics carry no route label because RestDb dispatches through Watson's default route. Per-endpoint views use `restdb_api_*`; traces carry the route template.
- MCP transports carry no trace context, so each tool call starts a new trace (the RestDb side joins it through `traceparent`). Protocol-level JSON-RPC rejections (unknown tool, invalid arguments, session errors) happen inside Voltaic before a tool handler runs and are not counted by `restdb_mcp_tool_calls_total`.
- Microsoft.Data.SqlClient 7.1 and Microsoft.Data.Sqlite expose no connection-pool meter; for those systems use the connection-open histogram as the pool-pressure signal.
- Metrics are per process and reset on restart; Prometheus keeps history.
