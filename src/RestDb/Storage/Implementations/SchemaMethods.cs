namespace RestDb.Storage.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.Storage.Interfaces;

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
            return _Driver.QueryBuilder.ReadTableNames(
                await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildListTables(), token).ConfigureAwait(false));
        }

        /// <inheritdoc />
        public async Task<List<Column>> DescribeTableAsync(string tableName, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));

            return _Driver.QueryBuilder.ReadColumns(
                await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildDescribeTable(tableName), token).ConfigureAwait(false));
        }

        /// <inheritdoc />
        public async Task CreateTableAsync(string tableName, List<Column> columns, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildCreateTable(tableName, columns), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ClearTableAsync(string tableName, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildClearTable(tableName), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DropTableAsync(string tableName, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildDropTable(tableName), token).ConfigureAwait(false);
        }
    }
}
