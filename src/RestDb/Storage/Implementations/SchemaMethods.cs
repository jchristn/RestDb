namespace RestDb.Storage.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.Storage.Interfaces;
    using RestDb.Telemetry;

    /// <summary>
    /// Shared schema methods implementation.
    /// </summary>
    internal class SchemaMethods : ISchemaMethods
    {
        private readonly DatabaseDriverBase _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        public SchemaMethods(DatabaseDriverBase driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<List<string>> ListTablesAsync(CancellationToken token = default)
        {
            SqlQueryDefinition query = _Driver.QueryBuilder.BuildListTables();
            query.OperationName = RestDbTelemetryNames.DbOperationListTables;
            return _Driver.QueryBuilder.ReadTableNames(await _Driver.ExecuteQueryAsync(query, token).ConfigureAwait(false));
        }

        /// <inheritdoc />
        public async Task<List<Column>> DescribeTableAsync(string tableName, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));

            SqlQueryDefinition query = _Driver.QueryBuilder.BuildDescribeTable(tableName);
            query.OperationName = RestDbTelemetryNames.DbOperationDescribeTable;
            return _Driver.QueryBuilder.ReadColumns(await _Driver.ExecuteQueryAsync(query, token).ConfigureAwait(false));
        }

        /// <inheritdoc />
        public async Task CreateTableAsync(string tableName, List<Column> columns, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            SqlQueryDefinition query = _Driver.QueryBuilder.BuildCreateTable(tableName, columns);
            query.OperationName = RestDbTelemetryNames.DbOperationCreateTable;
            await RestDbTelemetry.RunStageAsync(RestDbTelemetryNames.StageQuery, () => _Driver.ExecuteQueryAsync(query, token)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ClearTableAsync(string tableName, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            SqlQueryDefinition query = _Driver.QueryBuilder.BuildClearTable(tableName);
            query.OperationName = RestDbTelemetryNames.DbOperationClearTable;
            await RestDbTelemetry.RunStageAsync(RestDbTelemetryNames.StageQuery, () => _Driver.ExecuteQueryAsync(query, token)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DropTableAsync(string tableName, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            SqlQueryDefinition query = _Driver.QueryBuilder.BuildDropTable(tableName);
            query.OperationName = RestDbTelemetryNames.DbOperationDropTable;
            await RestDbTelemetry.RunStageAsync(RestDbTelemetryNames.StageQuery, () => _Driver.ExecuteQueryAsync(query, token)).ConfigureAwait(false);
        }
    }
}
