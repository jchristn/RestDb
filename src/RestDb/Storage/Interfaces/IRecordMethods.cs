namespace RestDb.Storage.Interfaces
{
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using ExpressionTree;

    /// <summary>
    /// Record methods.
    /// </summary>
    internal interface IRecordMethods
    {
        Task<DataTable> SelectAsync(
            string tableName,
            List<Column> columns,
            int? indexStart,
            int? maxResults,
            bool includeRowNumber,
            List<string> returnFields,
            Expr filter,
            ResultOrder[] resultOrder,
            CancellationToken token = default);

        Task<DataTable> InsertAsync(
            string tableName,
            List<Column> columns,
            Dictionary<string, object> values,
            CancellationToken token = default);

        Task InsertMultipleAsync(
            string tableName,
            List<Column> columns,
            List<Dictionary<string, object>> valuesList,
            CancellationToken token = default);

        Task UpdateAsync(
            string tableName,
            List<Column> columns,
            Dictionary<string, object> values,
            Expr filter,
            CancellationToken token = default);

        Task DeleteAsync(
            string tableName,
            List<Column> columns,
            Expr filter,
            CancellationToken token = default);
    }
}
