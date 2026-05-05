namespace RestDb.Storage.Interfaces
{
    using System.Collections.Generic;
    using System.Data;
    using ExpressionTree;

    /// <summary>
    /// Provider query builder.
    /// </summary>
    internal interface IRestDbQueryBuilder
    {
        string ProviderName { get; }
        SqlQueryDefinition BuildListTables();
        List<string> ReadTableNames(DataTable result);
        SqlQueryDefinition BuildDescribeTable(string tableName);
        List<Column> ReadColumns(DataTable result);
        SqlQueryDefinition BuildCreateTable(string tableName, List<Column> columns);
        SqlQueryDefinition BuildClearTable(string tableName);
        SqlQueryDefinition BuildDropTable(string tableName);
        SqlQueryDefinition BuildSelect(
            string tableName,
            List<Column> columns,
            int? indexStart,
            int? maxResults,
            List<string> returnFields,
            Expr filter,
            ResultOrder[] resultOrder);
        InsertPlan BuildInsert(string tableName, List<Column> columns, Dictionary<string, object> values);
        SqlBatchDefinition BuildInsertMultiple(string tableName, List<Column> columns, List<Dictionary<string, object>> valuesList);
        SqlQueryDefinition BuildUpdate(string tableName, List<Column> columns, Dictionary<string, object> values, Expr filter);
        SqlQueryDefinition BuildDelete(string tableName, List<Column> columns, Expr filter);
        SqlQueryDefinition BuildRawSql(string query);
    }
}
