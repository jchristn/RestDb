namespace RestDb.Storage
{
    using System;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.Storage.Interfaces;
    using RestDb.Telemetry;

    /// <summary>
    /// Common ADO.NET-backed driver.
    /// </summary>
    internal abstract class AdoDatabaseDriverBase : DatabaseDriverBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="queryBuilder">Query builder.</param>
        /// <param name="logger">Optional debug logger.</param>
        protected AdoDatabaseDriverBase(Database settings, IRestDbQueryBuilder queryBuilder, Action<string> logger = null) : base(settings, queryBuilder, logger)
        {
        }

        /// <summary>
        /// Provider factory.
        /// </summary>
        protected abstract DbProviderFactory ProviderFactory { get; }

        /// <summary>
        /// Build connection string.
        /// </summary>
        /// <returns>Connection string.</returns>
        protected abstract string BuildConnectionString();

        /// <summary>
        /// Hook invoked after a connection is opened.
        /// </summary>
        /// <param name="connection">Connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        protected virtual Task OnConnectionOpenedAsync(DbConnection connection, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        internal override async Task InitializeAsync(CancellationToken token = default)
        {
            EnsureNotDisposed();

            using (DbClientScope scope = RestDbTelemetry.BeginDbOperation(Settings, RestDbTelemetryNames.DbOperationInitialize, 0))
            {
                try
                {
                    using (DbConnection connection = await OpenConnectionAsync(scope, token).ConfigureAwait(false))
                    {
                        await connection.CloseAsync().ConfigureAwait(false);
                    }

                    scope.Complete(-1);
                }
                catch (Exception e)
                {
                    scope.Fail(e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        internal override async Task<DataTable> ExecuteQueryAsync(SqlQueryDefinition query, CancellationToken token = default)
        {
            EnsureNotDisposed();
            if (query == null) throw new ArgumentNullException(nameof(query));

            using (DbClientScope scope = RestDbTelemetry.BeginDbOperation(Settings, query.OperationName, 1))
            {
                try
                {
                    using (DbConnection connection = await OpenConnectionAsync(scope, token).ConfigureAwait(false))
                    {
                        DataTable result = await ExecuteInternalAsync(connection, null, query, token).ConfigureAwait(false);
                        await connection.CloseAsync().ConfigureAwait(false);
                        scope.Complete(result?.Rows.Count ?? 0);
                        return result;
                    }
                }
                catch (Exception e)
                {
                    scope.Fail(e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        internal override async Task<DataTable> ExecuteBatchAsync(SqlBatchDefinition batch, CancellationToken token = default)
        {
            EnsureNotDisposed();
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (batch.Queries == null || batch.Queries.Count < 1) return new DataTable();

            using (DbClientScope scope = RestDbTelemetry.BeginDbOperation(Settings, batch.OperationName, batch.Queries.Count))
            {
                try
                {
                    using (DbConnection connection = await OpenConnectionAsync(scope, token).ConfigureAwait(false))
                    {
                        DbTransaction transaction = null;
                        DataTable lastResult = new DataTable();

                        try
                        {
                            if (batch.UseTransaction)
                            {
                                transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                            }

                            foreach (SqlQueryDefinition query in batch.Queries)
                            {
                                lastResult = await ExecuteInternalAsync(connection, transaction, query, token).ConfigureAwait(false);
                            }

                            if (transaction != null)
                            {
                                await transaction.CommitAsync(token).ConfigureAwait(false);
                                scope.Transaction(true);
                            }

                            await connection.CloseAsync().ConfigureAwait(false);
                            scope.Complete(lastResult?.Rows.Count ?? 0);
                            return lastResult;
                        }
                        catch
                        {
                            if (transaction != null)
                            {
                                await transaction.RollbackAsync(token).ConfigureAwait(false);
                                scope.Transaction(false);
                            }

                            throw;
                        }
                        finally
                        {
                            if (transaction != null) transaction.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    scope.Fail(e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Create and open a connection, running the provider's post-open hook, and record the open time (including any
        /// connection pool wait) on the supplied scope.
        /// </summary>
        /// <param name="scope">Telemetry scope for the calling operation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Open connection. The caller disposes it.</returns>
        protected async Task<DbConnection> OpenConnectionAsync(DbClientScope scope, CancellationToken token)
        {
            long openStart = Stopwatch.GetTimestamp();
            DbConnection connection = CreateConnection();

            try
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                await OnConnectionOpenedAsync(connection, token).ConfigureAwait(false);
                scope?.ConnectionOpened(openStart, true);
                return connection;
            }
            catch
            {
                scope?.ConnectionOpened(openStart, false);
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Create a connection.
        /// </summary>
        /// <returns>Connection.</returns>
        protected DbConnection CreateConnection()
        {
            DbConnection conn = ProviderFactory.CreateConnection();
            conn.ConnectionString = BuildConnectionString();
            return conn;
        }

        private async Task<DataTable> ExecuteInternalAsync(DbConnection connection, DbTransaction transaction, SqlQueryDefinition query, CancellationToken token)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = query.CommandText;
                command.Transaction = transaction;
                command.CommandTimeout = 0;

                if (query.Parameters != null)
                {
                    foreach (QueryParameterDefinition parameter in query.Parameters)
                    {
                        DbParameter dbParameter = command.CreateParameter();
                        dbParameter.ParameterName = parameter.Name;
                        dbParameter.Value = parameter.Value ?? DBNull.Value;
                        command.Parameters.Add(dbParameter);
                    }
                }

                DebugLog("[" + QueryBuilder.ProviderName + "] " + ToDebugString(query));

                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    DataTable result = new DataTable();
                    if (reader.FieldCount > 0)
                    {
                        result.Load(reader);
                    }
                    return result;
                }
            }
        }

        private string ToDebugString(SqlQueryDefinition query)
        {
            if (query == null) return string.Empty;
            StringBuilder sb = new StringBuilder();
            sb.Append(query.CommandText);

            if (query.Parameters != null && query.Parameters.Count > 0)
            {
                sb.Append(" | params: ");
                for (int i = 0; i < query.Parameters.Count; i++)
                {
                    QueryParameterDefinition curr = query.Parameters[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append(curr.Name);
                    sb.Append("=");
                    sb.Append(curr.Value ?? "NULL");
                }
            }

            return sb.ToString();
        }
    }
}
