namespace RestDb.Storage.Interfaces
{
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Raw SQL methods.
    /// </summary>
    internal interface IRawSqlMethods
    {
        Task<DataTable> QueryAsync(string query, CancellationToken token = default);
    }
}
