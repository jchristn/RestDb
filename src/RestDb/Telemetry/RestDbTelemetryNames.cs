namespace RestDb.Telemetry
{
    /// <summary>
    /// Every telemetry name RestDb emits: the meter and activity source, instrument names, attribute (label) keys,
    /// and the bounded value sets used for labels. These strings are public contract consumed by Grafana dashboards
    /// and alert rules (see TELEMETRY.md); treat any change as a breaking change.
    /// Thread safety: constants only; safe for concurrent use.
    /// </summary>
    public static class RestDbTelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the RestDb <see cref="System.Diagnostics.Metrics.Meter"/> and <see cref="System.Diagnostics.ActivitySource"/>.
        /// </summary>
        public const string SourceName = "RestDb";

        /// <summary>
        /// Name of the Watson webserver meter and activity source (HTTP layer).
        /// </summary>
        public const string WatsonSourceName = "Watson";

        /// <summary>
        /// Name of the Npgsql driver meter (PostgreSQL connection pool metrics).
        /// </summary>
        public const string NpgsqlMeterName = "Npgsql";

        /// <summary>
        /// Name of the MySqlConnector driver meter (MySQL connection pool metrics).
        /// </summary>
        public const string MySqlConnectorMeterName = "MySqlConnector";

        #endregion

        #region Instruments

        /// <summary>
        /// Counter of API operations handled, by operation, outcome, and status code.
        /// </summary>
        public const string ApiRequests = "restdb.api.requests";

        /// <summary>
        /// Histogram of API operation duration in seconds, by operation and outcome.
        /// </summary>
        public const string ApiRequestDuration = "restdb.api.request.duration";

        /// <summary>
        /// Counter of failed API operations, by operation and error type.
        /// </summary>
        public const string ApiErrors = "restdb.api.errors";

        /// <summary>
        /// Up/down counter of API operations currently executing, by operation.
        /// </summary>
        public const string ApiActiveRequests = "restdb.api.active_requests";

        /// <summary>
        /// Counter of API workflow stage executions, by operation, stage, and outcome.
        /// </summary>
        public const string ApiStages = "restdb.api.stages";

        /// <summary>
        /// Histogram of API workflow stage duration in seconds, by operation, stage, and outcome.
        /// </summary>
        public const string ApiStageDuration = "restdb.api.stage.duration";

        /// <summary>
        /// Counter of authentication decisions, by outcome and credential type.
        /// </summary>
        public const string AuthDecisions = "restdb.auth.decisions";

        /// <summary>
        /// Counter of database client operations, by database system, database, operation, and outcome.
        /// </summary>
        public const string DbOperations = "restdb.db.client.operations";

        /// <summary>
        /// Histogram of database client operation duration in seconds (connection open plus execution).
        /// </summary>
        public const string DbOperationDuration = "restdb.db.client.operation.duration";

        /// <summary>
        /// Histogram of database connection open duration in seconds. Includes time waiting on the provider's connection pool.
        /// </summary>
        public const string DbConnectionOpenDuration = "restdb.db.client.connection.open.duration";

        /// <summary>
        /// Counter of rows returned to RestDb by database client operations.
        /// </summary>
        public const string DbRowsReturned = "restdb.db.client.rows_returned";

        /// <summary>
        /// Counter of database transactions, by outcome (commit or rollback).
        /// </summary>
        public const string DbTransactions = "restdb.db.client.transactions";

        /// <summary>
        /// Counter of records written through the record APIs (insert), by database system and database.
        /// </summary>
        public const string RecordsWritten = "restdb.records.written";

        /// <summary>
        /// Counter of runtime configuration changes (settings and context), by kind, action, and outcome.
        /// </summary>
        public const string ConfigChanges = "restdb.config.changes";

        /// <summary>
        /// Histogram of runtime configuration change duration in seconds.
        /// </summary>
        public const string ConfigChangeDuration = "restdb.config.change.duration";

        /// <summary>
        /// Gauge of the Unix time in seconds of the last successful configuration load, by kind.
        /// </summary>
        public const string ConfigLastChangeTimestamp = "restdb.config.last_change.timestamp";

        /// <summary>
        /// Gauge that is 1 when a settings change requires a process restart to take effect.
        /// </summary>
        public const string ConfigRestartRequired = "restdb.config.restart_required";

        /// <summary>
        /// Gauge of the number of configured databases, by database system.
        /// </summary>
        public const string ConfigDatabases = "restdb.config.databases";

        /// <summary>
        /// Gauge of the number of configured API keys.
        /// </summary>
        public const string ConfigApiKeys = "restdb.config.api_keys";

        /// <summary>
        /// Gauge that is 1 when API key authentication is required.
        /// </summary>
        public const string ConfigAuthenticationRequired = "restdb.config.authentication_required";

        /// <summary>
        /// Gauge that is always 1, labeled with the running RestDb version.
        /// </summary>
        public const string BuildInfo = "restdb.build.info";

        #endregion

        #region Attributes

        /// <summary>
        /// Attribute key for the bounded RestDb API operation name (see the Operation* constants).
        /// </summary>
        public const string AttributeOperation = "restdb.operation";

        /// <summary>
        /// Attribute key for the API workflow stage name (see the Stage* constants).
        /// </summary>
        public const string AttributeStage = "restdb.stage";

        /// <summary>
        /// Attribute key for an operation outcome (see the Outcome* constants).
        /// </summary>
        public const string AttributeOutcome = "outcome";

        /// <summary>
        /// OpenTelemetry attribute key for the error type: an exception type name or an HTTP status code.
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// OpenTelemetry attribute key for the HTTP response status code.
        /// </summary>
        public const string AttributeHttpStatusCode = "http.response.status_code";

        /// <summary>
        /// OpenTelemetry attribute key for the HTTP route template.
        /// </summary>
        public const string AttributeHttpRoute = "http.route";

        /// <summary>
        /// OpenTelemetry attribute key for the database system (sqlite, postgresql, mysql, microsoft.sql_server).
        /// </summary>
        public const string AttributeDbSystem = "db.system.name";

        /// <summary>
        /// OpenTelemetry attribute key for the configured database name. Bounded by restdb.json.
        /// </summary>
        public const string AttributeDbNamespace = "db.namespace";

        /// <summary>
        /// OpenTelemetry attribute key for the database operation (see the DbOperation* constants).
        /// </summary>
        public const string AttributeDbOperation = "db.operation.name";

        /// <summary>
        /// OpenTelemetry attribute key for the database server host. Span-only.
        /// </summary>
        public const string AttributeServerAddress = "server.address";

        /// <summary>
        /// OpenTelemetry attribute key for the database server port. Span-only.
        /// </summary>
        public const string AttributeServerPort = "server.port";

        /// <summary>
        /// Attribute key for the number of statements in a database batch. Span-only.
        /// </summary>
        public const string AttributeDbStatementCount = "db.operation.batch.size";

        /// <summary>
        /// Attribute key for rows returned by a database operation. Span-only.
        /// </summary>
        public const string AttributeDbRows = "db.response.returned_rows";

        /// <summary>
        /// Attribute key for the connection open time in seconds. Span-only.
        /// </summary>
        public const string AttributeDbConnectSeconds = "restdb.db.connect.duration";

        /// <summary>
        /// Attribute key for the authentication decision outcome (see the Auth* constants).
        /// </summary>
        public const string AttributeAuthOutcome = "restdb.auth.outcome";

        /// <summary>
        /// Attribute key for the credential type presented (header, bearer, or none).
        /// </summary>
        public const string AttributeCredentialType = "restdb.auth.credential_type";

        /// <summary>
        /// Attribute key for the configuration kind (settings or context).
        /// </summary>
        public const string AttributeConfigKind = "restdb.config.kind";

        /// <summary>
        /// Attribute key for the configuration action (reload or update).
        /// </summary>
        public const string AttributeConfigAction = "restdb.config.action";

        /// <summary>
        /// Attribute key for the database name on spans (high cardinality on requests for unknown databases). Span-only.
        /// </summary>
        public const string AttributeRequestedDatabase = "restdb.request.database";

        /// <summary>
        /// Attribute key for the table name on spans. Span-only.
        /// </summary>
        public const string AttributeRequestedTable = "restdb.request.table";

        /// <summary>
        /// OpenTelemetry attribute key for the service version.
        /// </summary>
        public const string AttributeServiceVersion = "service.version";

        #endregion

        #region Outcomes

        /// <summary>
        /// Outcome: the operation succeeded.
        /// </summary>
        public const string OutcomeSuccess = "success";

        /// <summary>
        /// Outcome: the request was rejected as a client error (4xx).
        /// </summary>
        public const string OutcomeClientError = "client_error";

        /// <summary>
        /// Outcome: the operation failed on the server (5xx or exception).
        /// </summary>
        public const string OutcomeError = "error";

        #endregion

        #region Operations

        /// <summary>Operation: process start-up (initial settings and context load, database initialization).</summary>
        public const string OperationStartup = "startup";

        /// <summary>Operation: GET / (status page).</summary>
        public const string OperationRoot = "root";

        /// <summary>Operation: GET /favicon.ico and /robots.txt.</summary>
        public const string OperationStatic = "static";

        /// <summary>Operation: request that matched no API route.</summary>
        public const string OperationUnknown = "unknown";

        /// <summary>Operation: GET /_databaseclients.</summary>
        public const string OperationDatabaseClients = "database_clients.list";

        /// <summary>Operation: GET /_settings.</summary>
        public const string OperationSettingsRead = "settings.read";

        /// <summary>Operation: PUT /_settings.</summary>
        public const string OperationSettingsUpdate = "settings.update";

        /// <summary>Operation: POST /_settings/reload.</summary>
        public const string OperationSettingsReload = "settings.reload";

        /// <summary>Operation: GET /_context.</summary>
        public const string OperationContextRead = "context.read";

        /// <summary>Operation: PUT /_context.</summary>
        public const string OperationContextUpdate = "context.update";

        /// <summary>Operation: POST /_context/reload.</summary>
        public const string OperationContextReload = "context.reload";

        /// <summary>Operation: GET /_context/{database}.</summary>
        public const string OperationDatabaseContextRead = "database_context.read";

        /// <summary>Operation: PUT /_context/{database}.</summary>
        public const string OperationDatabaseContextUpdate = "database_context.update";

        /// <summary>Operation: GET /_context/{database}/{table}.</summary>
        public const string OperationTableContextRead = "table_context.read";

        /// <summary>Operation: PUT /_context/{database}/{table}.</summary>
        public const string OperationTableContextUpdate = "table_context.update";

        /// <summary>Operation: GET /_databases.</summary>
        public const string OperationDatabasesList = "databases.list";

        /// <summary>Operation: GET /{database}.</summary>
        public const string OperationDatabaseRead = "database.read";

        /// <summary>Operation: GET /{database}/{table}[/{id}].</summary>
        public const string OperationTableSelect = "table.select";

        /// <summary>Operation: GET /{database}/{table}?_describe.</summary>
        public const string OperationTableDescribe = "table.describe";

        /// <summary>Operation: PUT /{database}/{table} (expression search).</summary>
        public const string OperationTableSearch = "table.search";

        /// <summary>Operation: PUT /{database}/{table}/{id} (update by primary key).</summary>
        public const string OperationTableUpdate = "table.update";

        /// <summary>Operation: POST /{database} (create table).</summary>
        public const string OperationTableCreate = "table.create";

        /// <summary>Operation: POST /{database}?raw (raw SQL).</summary>
        public const string OperationRawQuery = "raw_query";

        /// <summary>Operation: POST /{database}/{table} (insert).</summary>
        public const string OperationTableInsert = "table.insert";

        /// <summary>Operation: DELETE /{database}/{table}[/{id}] (delete rows).</summary>
        public const string OperationTableDelete = "table.delete";

        /// <summary>Operation: DELETE /{database}/{table}?_truncate.</summary>
        public const string OperationTableTruncate = "table.truncate";

        /// <summary>Operation: DELETE /{database}/{table}?_drop.</summary>
        public const string OperationTableDrop = "table.drop";

        #endregion

        #region Stages

        /// <summary>Stage: resolve the table name and describe its columns (two database round trips).</summary>
        public const string StageResolveTable = "resolve_table";

        /// <summary>Stage: list or describe tables for database metadata.</summary>
        public const string StageDescribe = "describe";

        /// <summary>Stage: parse the request body (JSON, expression, or column list).</summary>
        public const string StageParseRequest = "parse_request";

        /// <summary>Stage: execute the record or schema query against the database.</summary>
        public const string StageQuery = "query";

        /// <summary>Stage: merge database and table context into the response.</summary>
        public const string StageEnrichContext = "enrich_context";

        /// <summary>Stage: serialize the result to JSON.</summary>
        public const string StageSerialize = "serialize";

        /// <summary>Stage: apply a settings or context change (including persistence to disk and database reinitialization).</summary>
        public const string StageApplyConfig = "apply_config";

        #endregion

        #region Database-Operations

        /// <summary>Database operation: open a connection to verify connectivity at startup or reload.</summary>
        public const string DbOperationInitialize = "initialize";

        /// <summary>Database operation: list tables.</summary>
        public const string DbOperationListTables = "list_tables";

        /// <summary>Database operation: describe a table.</summary>
        public const string DbOperationDescribeTable = "describe_table";

        /// <summary>Database operation: select rows.</summary>
        public const string DbOperationSelect = "select";

        /// <summary>Database operation: insert one row.</summary>
        public const string DbOperationInsert = "insert";

        /// <summary>Database operation: insert many rows in a transaction.</summary>
        public const string DbOperationInsertMultiple = "insert_multiple";

        /// <summary>Database operation: update rows.</summary>
        public const string DbOperationUpdate = "update";

        /// <summary>Database operation: delete rows.</summary>
        public const string DbOperationDelete = "delete";

        /// <summary>Database operation: create a table.</summary>
        public const string DbOperationCreateTable = "create_table";

        /// <summary>Database operation: truncate (clear) a table.</summary>
        public const string DbOperationClearTable = "clear_table";

        /// <summary>Database operation: drop a table.</summary>
        public const string DbOperationDropTable = "drop_table";

        /// <summary>Database operation: caller-supplied raw SQL. The statement text is never recorded.</summary>
        public const string DbOperationRaw = "raw";

        /// <summary>Database operation: unclassified query.</summary>
        public const string DbOperationQuery = "query";

        #endregion

        #region Auth

        /// <summary>Authentication outcome: the key was valid and permits the HTTP method.</summary>
        public const string AuthAllowed = "allowed";

        /// <summary>Authentication outcome: no key in the configured header or Authorization bearer token.</summary>
        public const string AuthMissingCredentials = "missing_credentials";

        /// <summary>Authentication outcome: the presented key is not configured.</summary>
        public const string AuthUnknownKey = "unknown_key";

        /// <summary>Authentication outcome: the key is valid but not permitted for the HTTP method.</summary>
        public const string AuthMethodNotPermitted = "method_not_permitted";

        /// <summary>Authentication outcome: authentication is required but no API keys are configured.</summary>
        public const string AuthNoKeysConfigured = "no_keys_configured";

        /// <summary>Credential type: API key in the configured header.</summary>
        public const string CredentialHeader = "api_key_header";

        /// <summary>Credential type: Authorization bearer token.</summary>
        public const string CredentialBearer = "bearer";

        /// <summary>Credential type: none presented.</summary>
        public const string CredentialNone = "none";

        #endregion

        #region Config

        /// <summary>Configuration kind: restdb.json server settings.</summary>
        public const string ConfigKindSettings = "settings";

        /// <summary>Configuration kind: context.json database and table context.</summary>
        public const string ConfigKindContext = "context";

        /// <summary>Configuration action: reload from disk.</summary>
        public const string ConfigActionReload = "reload";

        /// <summary>Configuration action: update through the API.</summary>
        public const string ConfigActionUpdate = "update";

        /// <summary>Configuration action: initial load at startup.</summary>
        public const string ConfigActionStartup = "startup";

        #endregion
    }
}
