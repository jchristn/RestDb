namespace RestDb.Telemetry
{
    using System;
    using System.Data.Common;
    using System.Diagnostics;

    /// <summary>
    /// Times one outbound database call: opens a client span named "{db.system.name} {db.operation.name}" and records
    /// the database operation counter, duration histogram, connection-open histogram, and rows-returned counter.
    /// The SQL text and parameter values are never recorded. Call <see cref="Complete"/> or <see cref="Fail"/>, then
    /// dispose. Best-effort: never throws.
    /// Thread safety: an instance belongs to one async flow; do not share it across threads.
    /// </summary>
    internal sealed class DbClientScope : IDisposable
    {
        #region Private-Members

        private readonly string _DbSystem;
        private readonly string _DbNamespace;
        private readonly string _Operation;
        private readonly long _StartTimestamp;
        private readonly Activity _Activity;
        private string _ErrorType = null;
        private int _Rows = -1;
        private bool _Completed = false;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Begin a database client operation.
        /// </summary>
        /// <param name="database">Database settings. May be null.</param>
        /// <param name="operation">Bounded database operation name.</param>
        /// <param name="statementCount">Statements in the operation.</param>
        internal DbClientScope(Database database, string operation, int statementCount)
        {
            _DbSystem = RestDbTelemetry.DbSystemName(database);
            _DbNamespace = database?.Name ?? "(unknown)";
            _Operation = String.IsNullOrEmpty(operation) ? RestDbTelemetryNames.DbOperationQuery : operation;
            _StartTimestamp = Stopwatch.GetTimestamp();

            try
            {
                _Activity = RestDbTelemetry.Source.StartActivity(_DbSystem + " " + _Operation, ActivityKind.Client);
                if (_Activity != null)
                {
                    _Activity.SetTag(RestDbTelemetryNames.AttributeDbSystem, _DbSystem);
                    _Activity.SetTag(RestDbTelemetryNames.AttributeDbNamespace, _DbNamespace);
                    _Activity.SetTag(RestDbTelemetryNames.AttributeDbOperation, _Operation);
                    if (statementCount > 1) _Activity.SetTag(RestDbTelemetryNames.AttributeDbStatementCount, statementCount);

                    if (database != null && !String.IsNullOrWhiteSpace(database.Hostname))
                    {
                        _Activity.SetTag(RestDbTelemetryNames.AttributeServerAddress, database.Hostname);
                        if (database.Port.HasValue) _Activity.SetTag(RestDbTelemetryNames.AttributeServerPort, database.Port.Value);
                    }
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Record the connection open (including any connection pool wait).
        /// </summary>
        /// <param name="startTimestamp">Stopwatch timestamp taken before the open began.</param>
        /// <param name="success">Whether the open succeeded.</param>
        internal void ConnectionOpened(long startTimestamp, bool success)
        {
            try
            {
                double seconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
                RestDbTelemetry.RecordConnectionOpen(_DbSystem, _DbNamespace, success, seconds);
                _Activity?.SetTag(RestDbTelemetryNames.AttributeDbConnectSeconds, seconds);
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Record a transaction outcome.
        /// </summary>
        /// <param name="committed">True for commit, false for rollback.</param>
        internal void Transaction(bool committed)
        {
            RestDbTelemetry.RecordTransaction(_DbSystem, _DbNamespace, committed);
            _Activity?.SetTag("db.transaction.outcome", committed ? "commit" : "rollback");
        }

        /// <summary>
        /// Mark the operation complete.
        /// </summary>
        /// <param name="rows">Rows returned, or -1 when not applicable.</param>
        internal void Complete(int rows)
        {
            _Completed = true;
            _Rows = rows;
        }

        /// <summary>
        /// Mark the operation failed by an exception. For provider exceptions the SQLSTATE or provider error code is
        /// recorded on the span as db.response.status_code.
        /// </summary>
        /// <param name="e">Exception.</param>
        internal void Fail(Exception e)
        {
            _Completed = true;
            _ErrorType = RestDbTelemetry.ErrorType(e);
            RestDbTelemetry.MarkFailed(_Activity, e);

            try
            {
                if (_Activity != null && e is DbException dbException)
                {
                    string code = !String.IsNullOrWhiteSpace(dbException.SqlState) ? dbException.SqlState : dbException.ErrorCode.ToString();
                    _Activity.SetTag("db.response.status_code", code);
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        /// <summary>
        /// Stop the span and record the operation metrics.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                if (!_Completed && _ErrorType == null) _ErrorType = "_OTHER";

                double seconds = Stopwatch.GetElapsedTime(_StartTimestamp).TotalSeconds;
                RestDbTelemetry.RecordDbOperation(_DbSystem, _DbNamespace, _Operation, _ErrorType, seconds, _Rows);

                if (_Activity != null)
                {
                    if (_Rows >= 0) _Activity.SetTag(RestDbTelemetryNames.AttributeDbRows, _Rows);
                    if (_Activity.Status == ActivityStatusCode.Unset)
                    {
                        if (_ErrorType == null) _Activity.SetStatus(ActivityStatusCode.Ok);
                        else _Activity.SetStatus(ActivityStatusCode.Error, _ErrorType);
                    }

                    _Activity.Dispose();
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        #endregion
    }
}
