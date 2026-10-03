namespace RestDb.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// RestDb's application-level instrumentation: one <see cref="Meter"/> and one <see cref="ActivitySource"/>, both
    /// named <see cref="RestDbTelemetryNames.SourceName"/>, plus typed recording helpers. Emission rides the base class
    /// library only, so it costs effectively nothing until a listener (the Radiant host, or a test listener) subscribes.
    /// Every member is best-effort: a telemetry failure never propagates to request handling.
    /// Thread safety: all members are safe for concurrent use.
    /// </summary>
    internal static class RestDbTelemetry
    {
        #region Internal-Members

        /// <summary>
        /// The RestDb version stamped on the meter, the activity source, and the build-info gauge.
        /// </summary>
        internal static readonly string Version = ResolveVersion();

        /// <summary>
        /// The RestDb activity source.
        /// </summary>
        internal static readonly ActivitySource Source = new ActivitySource(RestDbTelemetryNames.SourceName, Version);

        /// <summary>
        /// The RestDb meter.
        /// </summary>
        internal static readonly Meter Meter = new Meter(RestDbTelemetryNames.SourceName, Version);

        /// <summary>
        /// The API operation executing on the current async flow, or null outside an operation.
        /// Stages recorded deeper in the call stack read it to label their measurements.
        /// </summary>
        internal static string CurrentOperation
        {
            get
            {
                return _CurrentOperation.Value;
            }
            set
            {
                _CurrentOperation.Value = value;
            }
        }

        #endregion

        #region Private-Members

        private static readonly double[] _LatencyBuckets = new double[]
        {
            0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60
        };

        private static readonly AsyncLocal<string> _CurrentOperation = new AsyncLocal<string>();

        private static readonly Counter<long> _ApiRequests = Meter.CreateCounter<long>(
            RestDbTelemetryNames.ApiRequests, "{request}", "API operations handled, by operation, outcome, and status code.");

        private static readonly Histogram<double> _ApiRequestDuration = CreateLatencyHistogram(
            RestDbTelemetryNames.ApiRequestDuration, "API operation duration, by operation and outcome.");

        private static readonly Counter<long> _ApiErrors = Meter.CreateCounter<long>(
            RestDbTelemetryNames.ApiErrors, "{error}", "Failed API operations, by operation and error type.");

        private static readonly UpDownCounter<long> _ApiActiveRequests = Meter.CreateUpDownCounter<long>(
            RestDbTelemetryNames.ApiActiveRequests, "{request}", "API operations currently executing, by operation.");

        private static readonly Counter<long> _ApiStages = Meter.CreateCounter<long>(
            RestDbTelemetryNames.ApiStages, "{stage}", "API workflow stage executions, by operation, stage, and outcome.");

        private static readonly Histogram<double> _ApiStageDuration = CreateLatencyHistogram(
            RestDbTelemetryNames.ApiStageDuration, "API workflow stage duration, by operation, stage, and outcome.");

        private static readonly Counter<long> _AuthDecisions = Meter.CreateCounter<long>(
            RestDbTelemetryNames.AuthDecisions, "{decision}", "Authentication decisions, by outcome and credential type.");

        private static readonly Counter<long> _DbOperations = Meter.CreateCounter<long>(
            RestDbTelemetryNames.DbOperations, "{operation}", "Database client operations, by database system, database, operation, and outcome.");

        private static readonly Histogram<double> _DbOperationDuration = CreateLatencyHistogram(
            RestDbTelemetryNames.DbOperationDuration, "Database client operation duration including connection open, by database system, database, operation, and outcome.");

        private static readonly Histogram<double> _DbConnectionOpenDuration = CreateLatencyHistogram(
            RestDbTelemetryNames.DbConnectionOpenDuration, "Database connection open duration including connection pool wait, by database system, database, and outcome.");

        private static readonly Counter<long> _DbRowsReturned = Meter.CreateCounter<long>(
            RestDbTelemetryNames.DbRowsReturned, "{row}", "Rows returned by database client operations, by database system, database, and operation.");

        private static readonly Counter<long> _DbTransactions = Meter.CreateCounter<long>(
            RestDbTelemetryNames.DbTransactions, "{transaction}", "Database transactions, by database system, database, and outcome (commit or rollback).");

        private static readonly Counter<long> _RecordsWritten = Meter.CreateCounter<long>(
            RestDbTelemetryNames.RecordsWritten, "{record}", "Records inserted through the record APIs, by database system and database.");

        private static readonly Counter<long> _ConfigChanges = Meter.CreateCounter<long>(
            RestDbTelemetryNames.ConfigChanges, "{change}", "Runtime configuration changes, by kind, action, and outcome.");

        private static readonly Histogram<double> _ConfigChangeDuration = CreateLatencyHistogram(
            RestDbTelemetryNames.ConfigChangeDuration, "Runtime configuration change duration, by kind, action, and outcome.");

        private static Settings _ObservedSettings = null;
        private static long _SettingsLoadedUnixMs = 0;
        private static long _ContextLoadedUnixMs = 0;
        private static int _RestartRequired = 0;

        #endregion

        #region Constructors-and-Factories

        static RestDbTelemetry()
        {
            try
            {
                Meter.CreateObservableGauge<long>(
                    RestDbTelemetryNames.BuildInfo,
                    () => new Measurement<long>(1, new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeServiceVersion, Version)),
                    null,
                    "Always 1; labeled with the running RestDb version.");

                Meter.CreateObservableGauge<double>(
                    RestDbTelemetryNames.ConfigLastChangeTimestamp,
                    ObserveLastChange,
                    "s",
                    "Unix time of the last successful settings or context load, by kind.");

                Meter.CreateObservableGauge<int>(
                    RestDbTelemetryNames.ConfigRestartRequired,
                    () => Volatile.Read(ref _RestartRequired),
                    null,
                    "1 when a settings change (listener host, port, or SSL) requires a process restart to take effect.");

                Meter.CreateObservableGauge<int>(
                    RestDbTelemetryNames.ConfigDatabases,
                    ObserveDatabases,
                    "{database}",
                    "Configured databases, by database system.");

                Meter.CreateObservableGauge<int>(
                    RestDbTelemetryNames.ConfigApiKeys,
                    () => Volatile.Read(ref _ObservedSettings)?.ApiKeys?.Count ?? 0,
                    "{key}",
                    "Configured API keys.");

                Meter.CreateObservableGauge<int>(
                    RestDbTelemetryNames.ConfigAuthenticationRequired,
                    () => (Volatile.Read(ref _ObservedSettings)?.Server?.RequireAuthentication ?? false) ? 1 : 0,
                    null,
                    "1 when API key authentication is required.");
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Begin an API operation. Dispose the returned scope when the operation completes.
        /// </summary>
        /// <param name="operation">Bounded operation name from <see cref="RestDbTelemetryNames"/>.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="routeTemplate">Route template, for example /{database}/{table}.</param>
        /// <returns>Operation scope. Never null.</returns>
        internal static ApiOperationScope BeginOperation(string operation, string method, string routeTemplate)
        {
            return new ApiOperationScope(operation, method, routeTemplate);
        }

        /// <summary>
        /// Label Watson's per-request server span (the current activity, when it is Watson's) with the route template and,
        /// when known, the operation. RestDb routes everything through Watson's default route, so without this the span
        /// is named only by its method.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="routeTemplate">Route template. May be null.</param>
        /// <param name="operation">Operation name. May be null.</param>
        internal static void TagServerSpan(string method, string routeTemplate, string operation)
        {
            try
            {
                Activity server = Activity.Current;
                if (server == null || server.Source.Name != RestDbTelemetryNames.WatsonSourceName) return;

                if (!String.IsNullOrEmpty(operation)) server.SetTag(RestDbTelemetryNames.AttributeOperation, operation);

                if (!String.IsNullOrEmpty(routeTemplate) && server.GetTagItem(RestDbTelemetryNames.AttributeHttpRoute) == null)
                {
                    server.SetTag(RestDbTelemetryNames.AttributeHttpRoute, routeTemplate);
                    server.DisplayName = method + " " + routeTemplate;
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Begin a database client operation. Dispose the returned scope when the operation completes.
        /// </summary>
        /// <param name="database">Database settings. May be null.</param>
        /// <param name="operation">Bounded database operation name.</param>
        /// <param name="statementCount">Statements in the operation.</param>
        /// <returns>Database client scope. Never null.</returns>
        internal static DbClientScope BeginDbOperation(Database database, string operation, int statementCount)
        {
            return new DbClientScope(database, operation, statementCount);
        }

        /// <summary>
        /// Run an asynchronous API workflow stage, timing it and recording a child span.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="stage">Bounded stage name.</param>
        /// <param name="work">Work to run.</param>
        /// <returns>The work's result.</returns>
        internal static async Task<T> RunStageAsync<T>(string stage, Func<Task<T>> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            StageScope scope = new StageScope(stage);
            try
            {
                T result = await work().ConfigureAwait(false);
                scope.Complete();
                return result;
            }
            catch (Exception e)
            {
                scope.Fail(e);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }

        /// <summary>
        /// Run an asynchronous API workflow stage that returns no value.
        /// </summary>
        /// <param name="stage">Bounded stage name.</param>
        /// <param name="work">Work to run.</param>
        /// <returns>Task.</returns>
        internal static async Task RunStageAsync(string stage, Func<Task> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            StageScope scope = new StageScope(stage);
            try
            {
                await work().ConfigureAwait(false);
                scope.Complete();
            }
            catch (Exception e)
            {
                scope.Fail(e);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }

        /// <summary>
        /// Run a synchronous API workflow stage.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="stage">Bounded stage name.</param>
        /// <param name="work">Work to run.</param>
        /// <returns>The work's result.</returns>
        internal static T RunStage<T>(string stage, Func<T> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            StageScope scope = new StageScope(stage);
            try
            {
                T result = work();
                scope.Complete();
                return result;
            }
            catch (Exception e)
            {
                scope.Fail(e);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }

        /// <summary>
        /// Record a completed API operation.
        /// </summary>
        /// <param name="operation">Operation name.</param>
        /// <param name="statusCode">Response status code.</param>
        /// <param name="errorType">Error type when the operation failed, otherwise null.</param>
        /// <param name="seconds">Duration in seconds.</param>
        internal static void RecordOperation(string operation, int statusCode, string errorType, double seconds)
        {
            try
            {
                string outcome = OutcomeFromStatus(statusCode, errorType);

                TagList counterTags = new TagList();
                counterTags.Add(RestDbTelemetryNames.AttributeOperation, operation);
                counterTags.Add(RestDbTelemetryNames.AttributeOutcome, outcome);
                counterTags.Add(RestDbTelemetryNames.AttributeHttpStatusCode, statusCode);
                _ApiRequests.Add(1, counterTags);

                TagList durationTags = new TagList();
                durationTags.Add(RestDbTelemetryNames.AttributeOperation, operation);
                durationTags.Add(RestDbTelemetryNames.AttributeOutcome, outcome);
                _ApiRequestDuration.Record(seconds, durationTags);

                if (outcome != RestDbTelemetryNames.OutcomeSuccess)
                {
                    TagList errorTags = new TagList();
                    errorTags.Add(RestDbTelemetryNames.AttributeOperation, operation);
                    errorTags.Add(RestDbTelemetryNames.AttributeErrorType, errorType ?? statusCode.ToString());
                    _ApiErrors.Add(1, errorTags);
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Adjust the count of in-flight API operations.
        /// </summary>
        /// <param name="operation">Operation name.</param>
        /// <param name="delta">+1 at start, -1 at end.</param>
        internal static void AdjustActiveOperations(string operation, long delta)
        {
            try
            {
                _ApiActiveRequests.Add(delta, new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeOperation, operation));
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a completed API workflow stage.
        /// </summary>
        /// <param name="operation">Operation name, or null outside an operation.</param>
        /// <param name="stage">Stage name.</param>
        /// <param name="outcome">Outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        internal static void RecordStage(string operation, string stage, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList();
                tags.Add(RestDbTelemetryNames.AttributeOperation, operation ?? RestDbTelemetryNames.OperationUnknown);
                tags.Add(RestDbTelemetryNames.AttributeStage, stage);
                tags.Add(RestDbTelemetryNames.AttributeOutcome, outcome);
                _ApiStages.Add(1, tags);
                _ApiStageDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record an authentication decision and stamp it on the current span.
        /// </summary>
        /// <param name="outcome">Outcome from the Auth* constants.</param>
        /// <param name="credentialType">Credential type from the Credential* constants.</param>
        internal static void RecordAuthDecision(string outcome, string credentialType)
        {
            try
            {
                _AuthDecisions.Add(
                    1,
                    new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeAuthOutcome, outcome),
                    new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeCredentialType, credentialType));

                Activity current = Activity.Current;
                if (current != null)
                {
                    current.SetTag(RestDbTelemetryNames.AttributeAuthOutcome, outcome);
                    current.SetTag(RestDbTelemetryNames.AttributeCredentialType, credentialType);
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a completed database client operation.
        /// </summary>
        /// <param name="dbSystem">Database system.</param>
        /// <param name="dbNamespace">Configured database name.</param>
        /// <param name="operation">Database operation name.</param>
        /// <param name="errorType">Error type on failure, otherwise null.</param>
        /// <param name="seconds">Duration in seconds.</param>
        /// <param name="rows">Rows returned, or -1 when unknown.</param>
        internal static void RecordDbOperation(string dbSystem, string dbNamespace, string operation, string errorType, double seconds, int rows)
        {
            try
            {
                string outcome = errorType == null ? RestDbTelemetryNames.OutcomeSuccess : RestDbTelemetryNames.OutcomeError;

                TagList tags = new TagList();
                tags.Add(RestDbTelemetryNames.AttributeDbSystem, dbSystem);
                tags.Add(RestDbTelemetryNames.AttributeDbNamespace, dbNamespace);
                tags.Add(RestDbTelemetryNames.AttributeDbOperation, operation);
                tags.Add(RestDbTelemetryNames.AttributeOutcome, outcome);
                if (errorType != null) tags.Add(RestDbTelemetryNames.AttributeErrorType, errorType);

                _DbOperations.Add(1, tags);
                _DbOperationDuration.Record(seconds, tags);

                if (rows > 0)
                {
                    TagList rowTags = new TagList();
                    rowTags.Add(RestDbTelemetryNames.AttributeDbSystem, dbSystem);
                    rowTags.Add(RestDbTelemetryNames.AttributeDbNamespace, dbNamespace);
                    rowTags.Add(RestDbTelemetryNames.AttributeDbOperation, operation);
                    _DbRowsReturned.Add(rows, rowTags);
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a database connection open.
        /// </summary>
        /// <param name="dbSystem">Database system.</param>
        /// <param name="dbNamespace">Configured database name.</param>
        /// <param name="success">Whether the open succeeded.</param>
        /// <param name="seconds">Duration in seconds.</param>
        internal static void RecordConnectionOpen(string dbSystem, string dbNamespace, bool success, double seconds)
        {
            try
            {
                TagList tags = new TagList();
                tags.Add(RestDbTelemetryNames.AttributeDbSystem, dbSystem);
                tags.Add(RestDbTelemetryNames.AttributeDbNamespace, dbNamespace);
                tags.Add(RestDbTelemetryNames.AttributeOutcome, success ? RestDbTelemetryNames.OutcomeSuccess : RestDbTelemetryNames.OutcomeError);
                _DbConnectionOpenDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a database transaction outcome.
        /// </summary>
        /// <param name="dbSystem">Database system.</param>
        /// <param name="dbNamespace">Configured database name.</param>
        /// <param name="committed">True for commit, false for rollback.</param>
        internal static void RecordTransaction(string dbSystem, string dbNamespace, bool committed)
        {
            try
            {
                TagList tags = new TagList();
                tags.Add(RestDbTelemetryNames.AttributeDbSystem, dbSystem);
                tags.Add(RestDbTelemetryNames.AttributeDbNamespace, dbNamespace);
                tags.Add(RestDbTelemetryNames.AttributeOutcome, committed ? "commit" : "rollback");
                _DbTransactions.Add(1, tags);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record records written through the record APIs.
        /// </summary>
        /// <param name="database">Database settings. May be null.</param>
        /// <param name="count">Records written.</param>
        internal static void RecordRecordsWritten(Database database, long count)
        {
            if (count < 1) return;

            try
            {
                TagList tags = new TagList();
                tags.Add(RestDbTelemetryNames.AttributeDbSystem, DbSystemName(database));
                tags.Add(RestDbTelemetryNames.AttributeDbNamespace, database?.Name ?? "(unknown)");
                _RecordsWritten.Add(count, tags);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a runtime configuration change.
        /// </summary>
        /// <param name="kind">Configuration kind (settings or context).</param>
        /// <param name="action">Configuration action (startup, reload, or update).</param>
        /// <param name="success">Whether the change succeeded.</param>
        /// <param name="seconds">Duration in seconds.</param>
        internal static void RecordConfigChange(string kind, string action, bool success, double seconds)
        {
            try
            {
                TagList tags = new TagList();
                tags.Add(RestDbTelemetryNames.AttributeConfigKind, kind);
                tags.Add(RestDbTelemetryNames.AttributeConfigAction, action);
                tags.Add(RestDbTelemetryNames.AttributeOutcome, success ? RestDbTelemetryNames.OutcomeSuccess : RestDbTelemetryNames.OutcomeError);
                _ConfigChanges.Add(1, tags);
                _ConfigChangeDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Publish the configuration state read by the configuration gauges.
        /// </summary>
        /// <param name="settings">Active settings. May be null.</param>
        /// <param name="settingsLoadedUtc">When settings were last loaded.</param>
        /// <param name="contextLoadedUtc">When context was last loaded.</param>
        /// <param name="restartRequired">Whether a pending change requires a restart.</param>
        internal static void UpdateConfigurationState(Settings settings, DateTime settingsLoadedUtc, DateTime contextLoadedUtc, bool restartRequired)
        {
            Volatile.Write(ref _ObservedSettings, settings);
            Interlocked.Exchange(ref _SettingsLoadedUnixMs, ToUnixMs(settingsLoadedUtc));
            Interlocked.Exchange(ref _ContextLoadedUnixMs, ToUnixMs(contextLoadedUtc));
            if (restartRequired) Interlocked.Exchange(ref _RestartRequired, 1);
        }

        /// <summary>
        /// Map a database type to its OpenTelemetry db.system.name value.
        /// </summary>
        /// <param name="database">Database settings. May be null.</param>
        /// <returns>sqlite, postgresql, mysql, microsoft.sql_server, or other_sql.</returns>
        internal static string DbSystemName(Database database)
        {
            if (database == null) return "other_sql";
            return DbSystemName(database.Type);
        }

        /// <summary>
        /// Map a database type to its OpenTelemetry db.system.name value.
        /// </summary>
        /// <param name="type">Database type.</param>
        /// <returns>sqlite, postgresql, mysql, microsoft.sql_server, or other_sql.</returns>
        internal static string DbSystemName(DbTypeEnum type)
        {
            switch (type)
            {
                case DbTypeEnum.Sqlite: return "sqlite";
                case DbTypeEnum.Postgresql: return "postgresql";
                case DbTypeEnum.Mysql: return "mysql";
                case DbTypeEnum.SqlServer: return "microsoft.sql_server";
                default: return "other_sql";
            }
        }

        /// <summary>
        /// Return the bounded error type for an exception: its full type name.
        /// </summary>
        /// <param name="e">Exception. May be null.</param>
        /// <returns>Error type.</returns>
        internal static string ErrorType(Exception e)
        {
            if (e == null) return "_OTHER";
            return e.GetType().FullName ?? e.GetType().Name;
        }

        /// <summary>
        /// Mark an activity as failed and attach an exception event. The exception message is deliberately omitted
        /// because database error text can echo row values; the type and stack trace identify the failure.
        /// </summary>
        /// <param name="activity">Activity. May be null.</param>
        /// <param name="e">Exception. May be null.</param>
        internal static void MarkFailed(Activity activity, Exception e)
        {
            if (activity == null) return;

            try
            {
                string errorType = ErrorType(e);
                activity.SetStatus(ActivityStatusCode.Error, errorType);
                activity.SetTag(RestDbTelemetryNames.AttributeErrorType, errorType);

                if (e != null)
                {
                    ActivityTagsCollection tags = new ActivityTagsCollection
                    {
                        { "exception.type", errorType },
                        { "exception.stacktrace", e.StackTrace ?? String.Empty }
                    };
                    activity.AddEvent(new ActivityEvent("exception", tags: tags));
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Map a status code and error type to an outcome.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="errorType">Error type when an exception escaped, otherwise null.</param>
        /// <returns>Outcome.</returns>
        internal static string OutcomeFromStatus(int statusCode, string errorType)
        {
            if (errorType != null || statusCode >= 500) return RestDbTelemetryNames.OutcomeError;
            if (statusCode >= 400) return RestDbTelemetryNames.OutcomeClientError;
            return RestDbTelemetryNames.OutcomeSuccess;
        }

        #endregion

        #region Private-Methods

        private static Histogram<double> CreateLatencyHistogram(string name, string description)
        {
            return Meter.CreateHistogram<double>(
                name,
                "s",
                description,
                null,
                new InstrumentAdvice<double> { HistogramBucketBoundaries = _LatencyBuckets });
        }

        private static IEnumerable<Measurement<double>> ObserveLastChange()
        {
            List<Measurement<double>> ret = new List<Measurement<double>>();
            long settingsMs = Interlocked.Read(ref _SettingsLoadedUnixMs);
            long contextMs = Interlocked.Read(ref _ContextLoadedUnixMs);

            if (settingsMs > 0)
            {
                ret.Add(new Measurement<double>(settingsMs / 1000.0, new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeConfigKind, RestDbTelemetryNames.ConfigKindSettings)));
            }

            if (contextMs > 0)
            {
                ret.Add(new Measurement<double>(contextMs / 1000.0, new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeConfigKind, RestDbTelemetryNames.ConfigKindContext)));
            }

            return ret;
        }

        private static IEnumerable<Measurement<int>> ObserveDatabases()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            Settings settings = Volatile.Read(ref _ObservedSettings);

            if (settings?.Databases != null)
            {
                foreach (Database database in settings.Databases)
                {
                    if (database == null) continue;
                    string system = DbSystemName(database);
                    counts.TryGetValue(system, out int current);
                    counts[system] = current + 1;
                }
            }

            List<Measurement<int>> ret = new List<Measurement<int>>();
            foreach (KeyValuePair<string, int> count in counts)
            {
                ret.Add(new Measurement<int>(count.Value, new KeyValuePair<string, object>(RestDbTelemetryNames.AttributeDbSystem, count.Key)));
            }

            return ret;
        }

        private static long ToUnixMs(DateTime utc)
        {
            if (utc == DateTime.MinValue) return 0;
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        }

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(RestDbTelemetry).Assembly;
                AssemblyInformationalVersionAttribute info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = info?.InformationalVersion;

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
