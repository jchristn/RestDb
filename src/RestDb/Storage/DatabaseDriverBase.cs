namespace RestDb.Storage
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.Storage.Implementations;
    using RestDb.Storage.Interfaces;

    /// <summary>
    /// Base database driver.
    /// </summary>
    internal abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        private bool _Disposed = false;
        private readonly Action<string> _Logger;

        /// <summary>
        /// Database settings.
        /// </summary>
        internal Database Settings { get; }

        /// <summary>
        /// Provider query builder.
        /// </summary>
        internal IRestDbQueryBuilder QueryBuilder { get; }

        /// <summary>
        /// Schema methods.
        /// </summary>
        internal ISchemaMethods Schema { get; }

        /// <summary>
        /// Record methods.
        /// </summary>
        internal IRecordMethods Records { get; }

        /// <summary>
        /// Raw SQL methods.
        /// </summary>
        internal IRawSqlMethods RawSql { get; }

        /// <summary>
        /// Provider name.
        /// </summary>
        internal string ProviderName => QueryBuilder.ProviderName;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="queryBuilder">Query builder.</param>
        /// <param name="logger">Optional debug logger.</param>
        protected DatabaseDriverBase(Database settings, IRestDbQueryBuilder queryBuilder, Action<string> logger = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            QueryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
            _Logger = logger;
            Schema = new SchemaMethods(this);
            Records = new RecordMethods(this);
            RawSql = new RawSqlMethods(this);
        }

        /// <summary>
        /// Initialize the driver.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Execute a query.
        /// </summary>
        /// <param name="query">Query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Data table.</returns>
        internal abstract Task<DataTable> ExecuteQueryAsync(SqlQueryDefinition query, CancellationToken token = default);

        /// <summary>
        /// Execute a batch.
        /// </summary>
        /// <param name="batch">Batch.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Data table from the last query.</returns>
        internal abstract Task<DataTable> ExecuteBatchAsync(SqlBatchDefinition batch, CancellationToken token = default);

        /// <summary>
        /// Write a debug message when enabled for the configured database.
        /// </summary>
        /// <param name="message">Message.</param>
        internal void DebugLog(string message)
        {
            if (!Settings.Debug || string.IsNullOrWhiteSpace(message)) return;
            if (_Logger != null) _Logger(message);
            else Console.WriteLine(message);
        }

        /// <summary>
        /// Ensure the driver has not been disposed.
        /// </summary>
        protected void EnsureNotDisposed()
        {
            if (_Disposed) throw new ObjectDisposedException(GetType().Name);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _Disposed = true;
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
