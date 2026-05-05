namespace RestDb.Storage.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Schema methods.
    /// </summary>
    internal interface ISchemaMethods
    {
        Task<List<string>> ListTablesAsync(CancellationToken token = default);
        Task<List<Column>> DescribeTableAsync(string tableName, CancellationToken token = default);
        Task CreateTableAsync(string tableName, List<Column> columns, CancellationToken token = default);
        Task ClearTableAsync(string tableName, CancellationToken token = default);
        Task DropTableAsync(string tableName, CancellationToken token = default);
    }
}
