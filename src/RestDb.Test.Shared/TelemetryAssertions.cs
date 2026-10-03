namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using RestDb;
using RestDb.Storage;
using RestDb.Telemetry;

/// <summary>
/// Proves the RestDb server emits its documented telemetry: API operations, workflow stages, database client calls,
/// authentication decisions, configuration changes, gauges, host lifecycle, and the end-to-end Prometheus export.
/// </summary>
internal static class TelemetryAssertions
{
    private static readonly string[] Meters = { RestDbTelemetryNames.SourceName };
    private static readonly string[] Sources = { RestDbTelemetryNames.SourceName };

    public static void NoListenerPathDoesNotThrow()
    {
        RestDbTelemetry.RecordOperation("probe", 200, null, 0.01);
        RestDbTelemetry.RecordOperation("probe", 500, "System.Exception", 0.01);
        RestDbTelemetry.AdjustActiveOperations("probe", 1);
        RestDbTelemetry.AdjustActiveOperations("probe", -1);
        RestDbTelemetry.RecordStage(null, "probe", RestDbTelemetryNames.OutcomeSuccess, 0.01);
        RestDbTelemetry.RecordAuthDecision(RestDbTelemetryNames.AuthAllowed, RestDbTelemetryNames.CredentialHeader);
        RestDbTelemetry.RecordDbOperation("sqlite", "probe", "select", null, 0.01, 3);
        RestDbTelemetry.RecordConnectionOpen("sqlite", "probe", false, 0.01);
        RestDbTelemetry.RecordTransaction("sqlite", "probe", false);
        RestDbTelemetry.RecordRecordsWritten(null, 2);
        RestDbTelemetry.RecordConfigChange(RestDbTelemetryNames.ConfigKindSettings, RestDbTelemetryNames.ConfigActionReload, false, 0.01);
        RestDbTelemetry.UpdateConfigurationState(null, DateTime.MinValue, DateTime.UtcNow, false);
        RestDbTelemetry.MarkFailed(null, new InvalidOperationException("probe"));

        using (ApiOperationScope scope = RestDbTelemetry.BeginOperation(null!, "GET", null!))
        {
            scope.Fail(null!);
        }

        using (DbClientScope scope = RestDbTelemetry.BeginDbOperation(null!, null!, 0))
        {
            scope.ConnectionOpened(Stopwatch.GetTimestamp(), false);
            scope.Transaction(false);
        }

        int value = RestDbTelemetry.RunStage("probe", () => 7);
        TestAssert.Equal(7, value);

        bool thrown = false;
        try
        {
            RestDbTelemetry.RunStage<int>("probe", () => throw new InvalidOperationException("stage failure"));
        }
        catch (InvalidOperationException e)
        {
            thrown = e.Message == "stage failure";
        }

        TestAssert.True(thrown, "RunStage must rethrow the original exception unchanged.");

        using (TelemetryHost disabled = TelemetryHost.Start(new TelemetrySettings { Enable = false }))
        {
            TestAssert.False(disabled.IsRunning, "A disabled telemetry host must be inert.");
            TestAssert.Null(disabled.StartupError);
        }

        using (TelemetryHost none = TelemetryHost.Start(null!))
        {
            TestAssert.False(none.IsRunning, "A null-settings telemetry host must be inert.");
        }
    }

    public static async Task ApiOperationEmitsMetricsAndNestedSpansAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        string operation = "test.op." + Guid.NewGuid().ToString("N").Substring(0, 8);

        using (ApiOperationScope scope = RestDbTelemetry.BeginOperation(operation, "GET", "/{database}/{table}"))
        {
            TestAssert.Equal(operation, RestDbTelemetry.CurrentOperation, "The operation must flow to nested stages.");
            int rows = await RestDbTelemetry.RunStageAsync(RestDbTelemetryNames.StageQuery, () => Task.FromResult(3)).ConfigureAwait(false);
            TestAssert.Equal(3, rows);
            scope.Complete(200);
        }

        TestAssert.Null(RestDbTelemetry.CurrentOperation, "The operation must be cleared when the scope ends.");

        CapturedMeasurement request = Single(capture, RestDbTelemetryNames.ApiRequests, RestDbTelemetryNames.AttributeOperation, operation);
        TestAssert.True(request.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeSuccess), "Expected outcome=success.");
        TestAssert.True(request.Has(RestDbTelemetryNames.AttributeHttpStatusCode, "200"), "Expected status code 200.");

        CapturedMeasurement duration = Single(capture, RestDbTelemetryNames.ApiRequestDuration, RestDbTelemetryNames.AttributeOperation, operation);
        TestAssert.Equal("s", duration.Unit, "Durations must use the UCUM seconds unit.");
        TestAssert.True(duration.Value >= 0, "Duration must be non-negative.");

        List<CapturedMeasurement> active = capture.Measurements(RestDbTelemetryNames.ApiActiveRequests)
            .Where(m => m.Has(RestDbTelemetryNames.AttributeOperation, operation)).ToList();
        TestAssert.Equal(2, active.Count, "Expected one increment and one decrement of the in-flight gauge.");
        TestAssert.Equal(0d, active.Sum(m => m.Value), "The in-flight gauge must return to zero.");

        TestAssert.Equal(0, capture.Measurements(RestDbTelemetryNames.ApiErrors).Count(m => m.Has(RestDbTelemetryNames.AttributeOperation, operation)),
            "A successful operation must not count as an error.");

        CapturedMeasurement stage = Single(capture, RestDbTelemetryNames.ApiStageDuration, RestDbTelemetryNames.AttributeOperation, operation);
        TestAssert.True(stage.Has(RestDbTelemetryNames.AttributeStage, RestDbTelemetryNames.StageQuery), "Expected the query stage to be labeled.");

        Activity operationSpan = SingleSpan(capture, "api " + operation);
        Activity stageSpan = capture.Spans().Single(a => a.DisplayName == "stage:query" && (string?)a.GetTagItem(RestDbTelemetryNames.AttributeOperation) == operation);
        TestAssert.Equal(operationSpan.SpanId, stageSpan.ParentSpanId, "The stage span must be a child of the operation span.");
        TestAssert.Equal(ActivityStatusCode.Ok, operationSpan.Status);
        TestAssert.Equal("/{database}/{table}", (string?)operationSpan.GetTagItem(RestDbTelemetryNames.AttributeHttpRoute));
    }

    public static void ApiOperationFailurePathsAreRecorded()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        string failing = "test.fail." + Guid.NewGuid().ToString("N").Substring(0, 8);
        string rejected = "test.reject." + Guid.NewGuid().ToString("N").Substring(0, 8);

        using (ApiOperationScope scope = RestDbTelemetry.BeginOperation(failing, "POST", "/{database}"))
        {
            scope.Fail(new InvalidOperationException("secret row value 'joel@example.com'"));
        }

        using (ApiOperationScope scope = RestDbTelemetry.BeginOperation(rejected, "GET", "/{database}/{table}"))
        {
            scope.Complete(404);
        }

        CapturedMeasurement failed = Single(capture, RestDbTelemetryNames.ApiRequests, RestDbTelemetryNames.AttributeOperation, failing);
        TestAssert.True(failed.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeError), "Expected outcome=error.");
        TestAssert.True(failed.Has(RestDbTelemetryNames.AttributeHttpStatusCode, "500"), "An escaped exception is a 500.");

        CapturedMeasurement failedError = Single(capture, RestDbTelemetryNames.ApiErrors, RestDbTelemetryNames.AttributeOperation, failing);
        TestAssert.True(failedError.Has(RestDbTelemetryNames.AttributeErrorType, "System.InvalidOperationException"), "error.type must be the exception type.");

        CapturedMeasurement rejectedRequest = Single(capture, RestDbTelemetryNames.ApiRequests, RestDbTelemetryNames.AttributeOperation, rejected);
        TestAssert.True(rejectedRequest.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeClientError), "A 404 is a client_error.");
        CapturedMeasurement rejectedError = Single(capture, RestDbTelemetryNames.ApiErrors, RestDbTelemetryNames.AttributeOperation, rejected);
        TestAssert.True(rejectedError.Has(RestDbTelemetryNames.AttributeErrorType, "404"), "error.type for a handled failure is the status code.");

        Activity failedSpan = SingleSpan(capture, "api " + failing);
        TestAssert.Equal(ActivityStatusCode.Error, failedSpan.Status);
        ActivityEvent exceptionEvent = failedSpan.Events.Single(e => e.Name == "exception");
        string allTags = String.Join("|", exceptionEvent.Tags.Select(t => t.Key + "=" + t.Value));
        TestAssert.Contains("System.InvalidOperationException", allTags, StringComparison.Ordinal);
        TestAssert.DoesNotContain("joel@example.com", allTags, StringComparison.Ordinal, "Exception messages (which can echo row data) must not be recorded.");

        Activity rejectedSpan = SingleSpan(capture, "api " + rejected);
        TestAssert.Equal(ActivityStatusCode.Ok, rejectedSpan.Status, "A 4xx is not a server-side span error.");
    }

    public static async Task StageFailureIsRecordedAndRethrownAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        string operation = "test.stage." + Guid.NewGuid().ToString("N").Substring(0, 8);
        bool rethrown = false;

        using (ApiOperationScope scope = RestDbTelemetry.BeginOperation(operation, "GET", "/"))
        {
            try
            {
                await RestDbTelemetry.RunStageAsync(RestDbTelemetryNames.StageResolveTable, () => Task.FromException(new TimeoutException("db down"))).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                rethrown = true;
            }

            scope.Complete(200);
        }

        TestAssert.True(rethrown, "The stage wrapper must rethrow.");
        CapturedMeasurement stage = Single(capture, RestDbTelemetryNames.ApiStages, RestDbTelemetryNames.AttributeOperation, operation);
        TestAssert.True(stage.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeError), "Expected stage outcome=error.");
        Activity stageSpan = capture.Spans().Single(a => a.DisplayName == "stage:resolve_table" && (string?)a.GetTagItem(RestDbTelemetryNames.AttributeOperation) == operation);
        TestAssert.Equal(ActivityStatusCode.Error, stageSpan.Status);
        TestAssert.Equal("System.TimeoutException", (string?)stageSpan.GetTagItem(RestDbTelemetryNames.AttributeErrorType));
    }

    public static async Task DatabaseClientEmitsMetricsAndSpansAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        string namespaceName = "telemetry_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string file = Path.Combine(Path.GetTempPath(), namespaceName + ".db");
        Database database = new Database { Name = namespaceName, Type = DbTypeEnum.Sqlite, Filename = file };

        try
        {
            using (DatabaseDriverBase driver = DatabaseDriverFactory.Create(database))
            {
                await driver.InitializeAsync().ConfigureAwait(false);

                List<Column> columns = new List<Column>
                {
                    new Column { Name = "id", Type = "int", Nullable = false, PrimaryKey = true },
                    new Column { Name = "name", Type = "nvarchar", MaxLength = 32, Nullable = true }
                };

                await driver.Schema.CreateTableAsync("widgets", columns).ConfigureAwait(false);
                await driver.Records.InsertAsync("widgets", columns, new Dictionary<string, object> { { "id", 1 }, { "name", "alpha" } }).ConfigureAwait(false);
                await driver.Records.InsertMultipleAsync("widgets", columns, new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "id", 2 }, { "name", "beta" } },
                    new Dictionary<string, object> { { "id", 3 }, { "name", "gamma" } }
                }).ConfigureAwait(false);
                DataTable rows = await driver.Records.SelectAsync("widgets", columns, null, null, false, null, null, null).ConfigureAwait(false);
                TestAssert.Equal(3, rows.Rows.Count);
                List<string> tables = await driver.Schema.ListTablesAsync().ConfigureAwait(false);
                TestAssert.True(tables.Contains("widgets"));
            }
        }
        finally
        {
            DeleteSqlite(file);
        }

        List<CapturedMeasurement> operations = capture.Measurements(RestDbTelemetryNames.DbOperations)
            .Where(m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName)).ToList();

        foreach (string op in new[]
        {
            RestDbTelemetryNames.DbOperationInitialize,
            RestDbTelemetryNames.DbOperationCreateTable,
            RestDbTelemetryNames.DbOperationInsert,
            RestDbTelemetryNames.DbOperationInsertMultiple,
            RestDbTelemetryNames.DbOperationSelect,
            RestDbTelemetryNames.DbOperationListTables
        })
        {
            TestAssert.Contains(operations, m => m.Has(RestDbTelemetryNames.AttributeDbOperation, op)
                && m.Has(RestDbTelemetryNames.AttributeDbSystem, "sqlite")
                && m.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeSuccess),
                "Expected a successful " + op + " database operation.");
        }

        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.DbRowsReturned), m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName)
            && m.Has(RestDbTelemetryNames.AttributeDbOperation, RestDbTelemetryNames.DbOperationSelect) && m.Value == 3,
            "Expected 3 rows returned by the select.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.DbConnectionOpenDuration), m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName),
            "Expected connection open timings.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.DbTransactions), m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName)
            && m.Has(RestDbTelemetryNames.AttributeOutcome, "commit"), "Expected a committed transaction.");
        TestAssert.Equal(3d, capture.Measurements(RestDbTelemetryNames.RecordsWritten)
            .Where(m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName)).Sum(m => m.Value), "Expected 3 records written.");

        List<Activity> spans = capture.Spans().Where(a => (string?)a.GetTagItem(RestDbTelemetryNames.AttributeDbNamespace) == namespaceName).ToList();
        Activity select = spans.Single(a => a.DisplayName == "sqlite select");
        TestAssert.Equal(ActivityKind.Client, select.Kind);
        TestAssert.Equal(3, (int?)select.GetTagItem(RestDbTelemetryNames.AttributeDbRows));
        TestAssert.NotNull(select.GetTagItem(RestDbTelemetryNames.AttributeDbConnectSeconds), "Expected the connect duration on the span.");

        foreach (Activity span in spans)
        {
            foreach (KeyValuePair<string, string?> tag in span.Tags)
            {
                TestAssert.DoesNotContain("FROM", tag.Value, StringComparison.Ordinal, "SQL text must never be recorded (" + tag.Key + ").");
                TestAssert.DoesNotContain("INSERT INTO", tag.Value, StringComparison.OrdinalIgnoreCase, "SQL text must never be recorded (" + tag.Key + ").");
                TestAssert.DoesNotContain("widgets", tag.Value, StringComparison.Ordinal, "Statement text must never be recorded (" + tag.Key + ").");
                TestAssert.DoesNotContain("alpha", tag.Value, StringComparison.OrdinalIgnoreCase, "Row values must never be recorded (" + tag.Key + ").");
            }
        }
    }

    public static async Task DatabaseClientFailurePathIsRecordedAsync()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);
        string namespaceName = "telemetry_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string file = Path.Combine(Path.GetTempPath(), namespaceName + ".db");
        Database database = new Database { Name = namespaceName, Type = DbTypeEnum.Sqlite, Filename = file };
        bool rawFailed = false;
        bool batchFailed = false;

        try
        {
            using (DatabaseDriverBase driver = DatabaseDriverFactory.Create(database))
            {
                try
                {
                    await driver.RawSql.QueryAsync("SELECT * FROM no_such_table").ConfigureAwait(false);
                }
                catch (Exception)
                {
                    rawFailed = true;
                }

                List<Column> columns = new List<Column> { new Column { Name = "missing", Type = "int", Nullable = true } };
                try
                {
                    await driver.Records.InsertMultipleAsync("no_such_table", columns, new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { { "missing", 1 } }
                    }).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    batchFailed = true;
                }
            }
        }
        finally
        {
            DeleteSqlite(file);
        }

        TestAssert.True(rawFailed && batchFailed, "Both database calls should have failed.");

        CapturedMeasurement raw = capture.Measurements(RestDbTelemetryNames.DbOperations).Single(m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName)
            && m.Has(RestDbTelemetryNames.AttributeDbOperation, RestDbTelemetryNames.DbOperationRaw));
        TestAssert.True(raw.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeError), "Expected outcome=error.");
        TestAssert.True(raw.Has(RestDbTelemetryNames.AttributeErrorType, "Microsoft.Data.Sqlite.SqliteException"), "error.type must be the provider exception type.");

        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.DbTransactions), m => m.Has(RestDbTelemetryNames.AttributeDbNamespace, namespaceName)
            && m.Has(RestDbTelemetryNames.AttributeOutcome, "rollback"), "Expected the failed batch to roll back.");

        Activity span = capture.Spans().Single(a => a.DisplayName == "sqlite raw" && (string?)a.GetTagItem(RestDbTelemetryNames.AttributeDbNamespace) == namespaceName);
        TestAssert.Equal(ActivityStatusCode.Error, span.Status);
        TestAssert.NotNull(span.GetTagItem("db.response.status_code"), "Expected the provider error code on the span.");
        TestAssert.True(span.Events.Any(e => e.Name == "exception"), "Expected an exception event.");
    }

    public static void AuthConfigAndGaugesAreRecorded()
    {
        using TelemetryCapture capture = new TelemetryCapture(Meters, Sources);

        RestDbTelemetry.RecordAuthDecision(RestDbTelemetryNames.AuthUnknownKey, RestDbTelemetryNames.CredentialBearer);
        RestDbTelemetry.RecordConfigChange(RestDbTelemetryNames.ConfigKindContext, RestDbTelemetryNames.ConfigActionUpdate, true, 0.002);

        Settings settings = new Settings();
        settings.Server.RequireAuthentication = true;
        settings.ApiKeys.Add(new ApiKey { Key = "a" });
        settings.ApiKeys.Add(new ApiKey { Key = "b" });
        settings.Databases.Add(new Database { Name = "one", Type = DbTypeEnum.Sqlite });
        settings.Databases.Add(new Database { Name = "two", Type = DbTypeEnum.Postgresql });
        settings.Databases.Add(new Database { Name = "three", Type = DbTypeEnum.Postgresql });
        RestDbTelemetry.UpdateConfigurationState(settings, DateTime.UtcNow, DateTime.UtcNow, false);
        capture.CollectObservables();

        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.AuthDecisions), m => m.Has(RestDbTelemetryNames.AttributeAuthOutcome, RestDbTelemetryNames.AuthUnknownKey)
            && m.Has(RestDbTelemetryNames.AttributeCredentialType, RestDbTelemetryNames.CredentialBearer), "Expected the auth decision counter.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.ConfigChanges), m => m.Has(RestDbTelemetryNames.AttributeConfigKind, RestDbTelemetryNames.ConfigKindContext)
            && m.Has(RestDbTelemetryNames.AttributeConfigAction, RestDbTelemetryNames.ConfigActionUpdate)
            && m.Has(RestDbTelemetryNames.AttributeOutcome, RestDbTelemetryNames.OutcomeSuccess), "Expected the config change counter.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.BuildInfo), m => m.Value == 1 && m.Has(RestDbTelemetryNames.AttributeServiceVersion, RestDbTelemetry.Version),
            "Expected the build-info gauge.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.ConfigDatabases), m => m.Value == 2 && m.Has(RestDbTelemetryNames.AttributeDbSystem, "postgresql"),
            "Expected the database count gauge by system.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.ConfigApiKeys), m => m.Value == 2, "Expected the API key count gauge.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.ConfigAuthenticationRequired), m => m.Value == 1, "Expected the authentication-required gauge.");
        TestAssert.Contains(capture.Measurements(RestDbTelemetryNames.ConfigLastChangeTimestamp), m => m.Has(RestDbTelemetryNames.AttributeConfigKind, RestDbTelemetryNames.ConfigKindSettings)
            && m.Value > 1_600_000_000, "Expected the last-change timestamp gauge.");
    }

    public static void RouteTemplatesAreBounded()
    {
        TestAssert.Equal("/", RestDbServer.RouteTemplate(Array.Empty<string>()));
        TestAssert.Equal("/_databases", RestDbServer.RouteTemplate(new[] { "_databases" }));
        TestAssert.Equal("/_settings/reload", RestDbServer.RouteTemplate(new[] { "_settings", "reload" }));
        TestAssert.Equal("/_context/{database}/{table}", RestDbServer.RouteTemplate(new[] { "_context", "customers", "orders" }));
        TestAssert.Equal("/{database}", RestDbServer.RouteTemplate(new[] { "customers" }));
        TestAssert.Equal("/{database}/{table}", RestDbServer.RouteTemplate(new[] { "customers", "orders" }));
        TestAssert.Equal("/{database}/{table}/{id}", RestDbServer.RouteTemplate(new[] { "customers", "orders", "42" }));
        TestAssert.Equal("/(other)", RestDbServer.RouteTemplate(new[] { "a", "b", "c", "d" }));
    }

    public static void WatsonServerSpanIsNamedWithRouteTemplate()
    {
        using ActivitySource watson = new ActivitySource(RestDbTelemetryNames.WatsonSourceName);
        using TelemetryCapture capture = new TelemetryCapture(Meters, new[] { RestDbTelemetryNames.WatsonSourceName });

        using (Activity? server = watson.StartActivity("GET", ActivityKind.Server))
        {
            TestAssert.NotNull(server, "Expected the test listener to sample the Watson span.");
            RestDbTelemetry.TagServerSpan("GET", "/{database}/{table}", null);
            TestAssert.Equal("GET /{database}/{table}", server!.DisplayName);
            TestAssert.Equal("/{database}/{table}", (string?)server.GetTagItem(RestDbTelemetryNames.AttributeHttpRoute));

            // A route Watson already resolved is never overwritten.
            RestDbTelemetry.TagServerSpan("GET", "/{database}", RestDbTelemetryNames.OperationDatabaseRead);
            TestAssert.Equal("GET /{database}/{table}", server.DisplayName);
            TestAssert.Equal(RestDbTelemetryNames.OperationDatabaseRead, (string?)server.GetTagItem(RestDbTelemetryNames.AttributeOperation));
        }

        RestDbTelemetry.TagServerSpan("GET", "/", null);
    }

    /// <summary>
    /// The Radiant host subscribes to RestDb's own sources and to the meters its dependencies emit, including
    /// SyslogLogging (2.3+), and a RestDb log entry is recorded on the SyslogLogging meter.
    /// </summary>
    public static async Task RadiantSubscribesToDependencyMetersAsync()
    {
        Radiant.RadiantSettings radiant = TelemetryHost.BuildRadiantSettings(new TelemetrySettings());
        foreach (string meter in new[]
        {
            RestDbTelemetryNames.SourceName,
            RestDbTelemetryNames.WatsonSourceName,
            RestDbTelemetryNames.NpgsqlMeterName,
            RestDbTelemetryNames.MySqlConnectorMeterName,
            RestDbTelemetryNames.SyslogLoggingMeterName
        })
        {
            TestAssert.Contains(radiant.Sources.MeterNames, m => m == meter, "Expected the Radiant host to subscribe to meter '" + meter + "'.");
        }

        TestAssert.Contains(radiant.Sources.ActivitySourceNames, a => a == RestDbTelemetryNames.SourceName, "Expected the RestDb activity source.");
        TestAssert.Contains(radiant.Sources.ActivitySourceNames, a => a == RestDbTelemetryNames.WatsonSourceName, "Expected the Watson activity source.");
        TestAssert.Equal(SyslogLogging.SyslogLoggingTelemetry.MeterName, RestDbTelemetryNames.SyslogLoggingMeterName, "The SyslogLogging meter name must match the library's.");

        using TelemetryCapture capture = new TelemetryCapture(new[] { RestDbTelemetryNames.SyslogLoggingMeterName }, Array.Empty<string>());
        int syslogPort = ReserveLoopbackPort();
        using (SyslogLogging.LoggingModule logging = new SyslogLogging.LoggingModule("127.0.0.1", syslogPort, false))
        {
            logging.Info("restdb telemetry probe");
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (capture.Measurements(SyslogLogging.SyslogLoggingTelemetry.EntriesMetric).Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        TestAssert.True(capture.Measurements(SyslogLogging.SyslogLoggingTelemetry.EntriesMetric).Count > 0, "Expected a log entry on the SyslogLogging meter.");
    }

    public static async Task TelemetryHostServesPrometheusAndReleasesPortAsync()
    {
        int port = RestDbLiveApiSession.GetFreeTcpPort();
        TelemetrySettings settings = new TelemetrySettings { OtlpEnable = false, PrometheusPort = port };
        string operation = "test.host." + Guid.NewGuid().ToString("N").Substring(0, 8);

        using (TelemetryHost host = TelemetryHost.Start(settings))
        {
            TestAssert.True(host.IsRunning, "Expected the telemetry host to start: " + host.StartupError);
            TestAssert.Equal("http://127.0.0.1:" + port + "/metrics", host.ScrapeUrl);

            RestDbTelemetry.RecordOperation(operation, 200, null, 0.01);
            string text = await ScrapeUntilAsync(new Uri(host.ScrapeUrl!), "restdb_operation=\"" + operation + "\"").ConfigureAwait(false);
            AssertSeries(text, "restdb_api_requests_total", "restdb_operation", operation, "outcome", "success");
            AssertSeries(text, "restdb_api_request_duration_seconds_bucket", "restdb_operation", operation);
        }

        using (TelemetryHost again = TelemetryHost.Start(settings))
        {
            TestAssert.True(again.IsRunning, "Disposing the host must release the scrape port: " + again.StartupError);
        }

        TcpListener blocker = new TcpListener(IPAddress.Loopback, port);
        blocker.Start();
        try
        {
            using (TelemetryHost blocked = TelemetryHost.Start(settings))
            {
                TestAssert.False(blocked.IsRunning, "A host whose port is taken must not run.");
                TestAssert.NotNull(blocked.StartupError, "A failed start must report why instead of throwing.");
            }
        }
        finally
        {
            blocker.Stop();
        }
    }

    public static async Task LiveServerExportsTelemetryAsync()
    {
        int prometheusPort = RestDbLiveApiSession.GetFreeTcpPort();
        await using RestDbLiveApiSession session = await RestDbLiveApiSession.StartAsync(
            RestDbTestRuntime.Configuration,
            requireAuthentication: true,
            prometheusPort: prometheusPort).ConfigureAwait(false);

        string db = session.DatabaseName;
        await SendAsync(session, HttpMethod.Get, "/_databases", true).ConfigureAwait(false);
        await SendAsync(session, HttpMethod.Get, "/" + db, true).ConfigureAwait(false);
        await SendAsync(session, HttpMethod.Get, "/" + db + "/no_such_table_" + Guid.NewGuid().ToString("N").Substring(0, 6), true).ConfigureAwait(false);
        await SendAsync(session, HttpMethod.Get, "/_databases", false).ConfigureAwait(false);
        await SendAsync(session, HttpMethod.Post, "/" + db + "?raw", true, "SELECT * FROM no_such_table_telemetry").ConfigureAwait(false);
        await SendAsync(session, HttpMethod.Post, "/_context/reload", true).ConfigureAwait(false);

        using (HttpRequestMessage wrongKey = new HttpRequestMessage(HttpMethod.Get, "/_databases"))
        {
            wrongKey.Headers.Add(session.ApiKeyHeader, "not-a-configured-key");
            using HttpClient client = new HttpClient { BaseAddress = session.BaseAddress };
            using HttpResponseMessage response = await client.SendAsync(wrongKey).ConfigureAwait(false);
            TestAssert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        string text = await ScrapeUntilAsync(session.PrometheusUrl!, "restdb_auth_outcome=\"unknown_key\"").ConfigureAwait(false);

        AssertSeries(text, "restdb_api_requests_total", "restdb_operation", "databases.list", "outcome", "success");
        AssertSeries(text, "restdb_api_requests_total", "restdb_operation", "database.read", "outcome", "success");
        AssertSeries(text, "restdb_api_requests_total", "restdb_operation", "table.select", "outcome", "client_error", "http_response_status_code", "404");
        AssertSeries(text, "restdb_api_requests_total", "restdb_operation", "raw_query", "outcome", "error");
        AssertSeries(text, "restdb_api_errors_total", "restdb_operation", "raw_query");
        AssertSeries(text, "restdb_api_stage_duration_seconds_bucket", "restdb_operation", "table.select", "restdb_stage", "resolve_table");
        AssertSeries(text, "restdb_db_client_operations_total", "db_operation_name", "raw", "outcome", "error");
        AssertSeries(text, "restdb_db_client_operation_duration_seconds_bucket", "db_operation_name", "list_tables");
        AssertSeries(text, "restdb_db_client_connection_open_duration_seconds_bucket", "db_namespace", db);
        AssertSeries(text, "restdb_auth_decisions_total", "restdb_auth_outcome", "allowed", "restdb_auth_credential_type", "api_key_header");
        AssertSeries(text, "restdb_auth_decisions_total", "restdb_auth_outcome", "missing_credentials");
        AssertSeries(text, "restdb_auth_decisions_total", "restdb_auth_outcome", "unknown_key");
        AssertSeries(text, "restdb_config_changes_total", "restdb_config_kind", "context", "restdb_config_action", "reload");
        AssertSeries(text, "restdb_config_changes_total", "restdb_config_kind", "settings", "restdb_config_action", "startup");
        AssertSeries(text, "restdb_build_info");
        AssertSeries(text, "restdb_config_authentication_required");
        AssertSeries(text, "http_server_request_duration_seconds_bucket");
        AssertSeries(text, "watson_server_up");
        AssertSeries(text, "process_uptime_seconds");
        TestAssert.True(
            text.Contains("process_runtime_dotnet_gc_collections_count_total", StringComparison.Ordinal)
                || text.Contains("dotnet_gc_collections_total", StringComparison.Ordinal),
            "Expected .NET runtime GC series (process_runtime_dotnet_* on net8.0, dotnet_* on net9.0+).");
        TestAssert.DoesNotContain("no_such_table", text, StringComparison.Ordinal, "Table names from requests must never become metric labels.");
    }

    internal static void AssertSeries(string exposition, string metric, params string[] labelPairs)
    {
        string[] lines = exposition.Split('\n');
        foreach (string line in lines)
        {
            if (!line.StartsWith(metric + "{", StringComparison.Ordinal) && !line.StartsWith(metric + " ", StringComparison.Ordinal)) continue;

            bool matches = true;
            for (int i = 0; i + 1 < labelPairs.Length; i += 2)
            {
                if (line.IndexOf(labelPairs[i] + "=\"" + labelPairs[i + 1] + "\"", StringComparison.Ordinal) < 0)
                {
                    matches = false;
                    break;
                }
            }

            if (matches) return;
        }

        throw new InvalidOperationException("Expected a " + metric + " series with " + String.Join(", ", labelPairs) + " in the Prometheus exposition.");
    }

    internal static async Task<string> ScrapeUntilAsync(Uri url, string expectedFragment)
    {
        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        string last = String.Empty;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                last = await client.GetStringAsync(url).ConfigureAwait(false);
                if (last.Contains(expectedFragment, StringComparison.Ordinal)) return last;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Timed out waiting for '" + expectedFragment + "' at " + url + ". Last scrape:" + Environment.NewLine + last);
    }

    private static async Task SendAsync(RestDbLiveApiSession session, HttpMethod method, string path, bool authenticated, string? body = null)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "text/plain");

        if (authenticated)
        {
            using HttpResponseMessage response = await session.Client.SendAsync(request).ConfigureAwait(false);
        }
        else
        {
            using HttpClient anonymous = new HttpClient { BaseAddress = session.BaseAddress };
            using HttpResponseMessage response = await anonymous.SendAsync(request).ConfigureAwait(false);
        }
    }

    private static CapturedMeasurement Single(TelemetryCapture capture, string instrument, string key, string value)
    {
        List<CapturedMeasurement> matches = capture.Measurements(instrument).Where(m => m.Has(key, value)).ToList();
        TestAssert.Equal(1, matches.Count, "Expected exactly one " + instrument + " measurement with " + key + "=" + value + ".");
        return matches[0];
    }

    private static Activity SingleSpan(TelemetryCapture capture, string name)
    {
        List<Activity> matches = capture.Spans().Where(a => a.DisplayName == name).ToList();
        TestAssert.Equal(1, matches.Count, "Expected exactly one span named '" + name + "'.");
        return matches[0];
    }

    private static void DeleteSqlite(string file)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (string path in new[] { file, file + "-wal", file + "-shm" })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
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
