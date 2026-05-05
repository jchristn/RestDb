namespace RestDb.Storage.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using ExpressionTree;
    using RestDb.Storage.Interfaces;

    /// <summary>
    /// Shared record methods implementation.
    /// </summary>
    internal class RecordMethods : IRecordMethods
    {
        private readonly DatabaseDriverBase _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Driver.</param>
        public RecordMethods(DatabaseDriverBase driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<DataTable> SelectAsync(
            string tableName,
            List<Column> columns,
            int? indexStart,
            int? maxResults,
            bool includeRowNumber,
            List<string> returnFields,
            Expr filter,
            ResultOrder[] resultOrder,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));

            DataTable result = await _Driver.ExecuteQueryAsync(
                _Driver.QueryBuilder.BuildSelect(tableName, columns, indexStart, maxResults, returnFields, filter, resultOrder),
                token).ConfigureAwait(false);

            if (includeRowNumber)
            {
                AddRowNumbers(result, indexStart ?? 1);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<DataTable> InsertAsync(
            string tableName,
            List<Column> columns,
            Dictionary<string, object> values,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (values == null) throw new ArgumentNullException(nameof(values));

            InsertPlan plan = _Driver.QueryBuilder.BuildInsert(tableName, columns, values);
            DataTable result = await _Driver.ExecuteBatchAsync(plan.Batch, token).ConfigureAwait(false);

            if (plan.ReturnsInsertedRow)
            {
                return result;
            }

            return CreateInMemoryRow(values);
        }

        /// <inheritdoc />
        public async Task InsertMultipleAsync(
            string tableName,
            List<Column> columns,
            List<Dictionary<string, object>> valuesList,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (valuesList == null) throw new ArgumentNullException(nameof(valuesList));
            if (valuesList.Count < 1) return;

            await _Driver.ExecuteBatchAsync(_Driver.QueryBuilder.BuildInsertMultiple(tableName, columns, valuesList), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UpdateAsync(
            string tableName,
            List<Column> columns,
            Dictionary<string, object> values,
            Expr filter,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (values == null) throw new ArgumentNullException(nameof(values));

            await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildUpdate(tableName, columns, values, filter), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(
            string tableName,
            List<Column> columns,
            Expr filter,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            await _Driver.ExecuteQueryAsync(_Driver.QueryBuilder.BuildDelete(tableName, columns, filter), token).ConfigureAwait(false);
        }

        private void AddRowNumbers(DataTable table, int firstRowNumber)
        {
            if (table == null) return;
            if (!table.Columns.Contains("__row_num__"))
            {
                table.Columns.Add("__row_num__", typeof(int));
                table.Columns["__row_num__"].SetOrdinal(0);
            }

            int rowNumber = Math.Max(1, firstRowNumber);
            foreach (DataRow row in table.Rows)
            {
                row["__row_num__"] = rowNumber++;
            }
        }

        private DataTable CreateInMemoryRow(Dictionary<string, object> values)
        {
            DataTable table = new DataTable();
            if (values == null) return table;

            foreach (KeyValuePair<string, object> kvp in values)
            {
                table.Columns.Add(kvp.Key, typeof(object));
            }

            DataRow row = table.NewRow();
            foreach (KeyValuePair<string, object> kvp in values)
            {
                row[kvp.Key] = kvp.Value ?? DBNull.Value;
            }

            table.Rows.Add(row);
            return table;
        }
    }
}
