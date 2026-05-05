namespace RestDb.Storage.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.Storage.Interfaces;

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
            return _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildRawSql(query), token);
        }
    }
}
