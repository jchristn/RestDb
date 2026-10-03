namespace RestDb.Storage.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.Storage.Interfaces;
    using RestDb.Telemetry;

    /// <summary>
    /// Shared raw SQL implementation.
    /// </summary>
    internal class RawSqlMethods : IRawSqlMethods
    {
        private readonly DatabaseDriverBase _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        public RawSqlMethods(DatabaseDriverBase driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public Task<DataTable> QueryAsync(string query, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));
            SqlQueryDefinition definition = _Driver.QueryBuilder.BuildRawSql(query);
            definition.OperationName = RestDbTelemetryNames.DbOperationRaw;
            return RestDbTelemetry.RunStageAsync(RestDbTelemetryNames.StageQuery, () => _Driver.ExecuteQueryAsync(definition, token));
        }
    }
}
