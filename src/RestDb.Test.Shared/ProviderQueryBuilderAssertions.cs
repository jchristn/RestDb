namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using RestDb;
using RestDb.Storage;
using RestDb.Storage.Interfaces;

internal static class ProviderQueryBuilderAssertions
{
    public static void GetDatabasePathUsesListTablesQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildListTables();

        TestAssert.False(string.IsNullOrWhiteSpace(query.CommandText));

        switch (providerName)
        {
            case "Sqlite":
                TestAssert.Contains("sqlite_master", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Postgresql":
                TestAssert.Contains("current_schema()", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
                TestAssert.Contains("INFORMATION_SCHEMA.TABLES", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Mysql":
                TestAssert.Contains("DATABASE()", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void DescribePathsUseDescribeTableQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildDescribeTable("person");

        TestAssert.False(string.IsNullOrWhiteSpace(query.CommandText));

        switch (providerName)
        {
            case "Sqlite":
                TestAssert.Contains("pragma_table_info('person')", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Postgresql":
                TestAssert.Contains("information_schema.columns c", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Equal(1, query.Parameters.Count);
                break;
            case "SqlServer":
                TestAssert.Contains("INFORMATION_SCHEMA.COLUMNS c", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Equal(1, query.Parameters.Count);
                break;
            case "Mysql":
                TestAssert.Contains("information_schema.columns c", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Equal(1, query.Parameters.Count);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void GetDatabaseDescribePathUsesListThenDescribeQueries(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition listTables = builder.BuildListTables();
        SqlQueryDefinition describeTable = builder.BuildDescribeTable("person");

        TestAssert.False(string.IsNullOrWhiteSpace(listTables.CommandText));
        TestAssert.False(string.IsNullOrWhiteSpace(describeTable.CommandText));

        switch (providerName)
        {
            case "Sqlite":
                TestAssert.Contains("sqlite_master", listTables.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("pragma_table_info('person')", describeTable.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Postgresql":
                TestAssert.Contains("current_schema()", listTables.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("information_schema.columns c", describeTable.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
                TestAssert.Contains("INFORMATION_SCHEMA.TABLES", listTables.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("INFORMATION_SCHEMA.COLUMNS c", describeTable.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Mysql":
                TestAssert.Contains("DATABASE()", listTables.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("information_schema.columns c", describeTable.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void PostTableCreatePathBuildsProviderSpecificCreateTableDdl(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildCreateTable("person", TestData.SampleColumns());

        TestAssert.Contains("person", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("first_name", query.CommandText, StringComparison.OrdinalIgnoreCase);

        switch (providerName)
        {
            case "Sqlite":
                TestAssert.Contains("INTEGER PRIMARY KEY AUTOINCREMENT", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("VARCHAR(32)", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Postgresql":
                TestAssert.Contains("GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("VARCHAR(32)", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
                TestAssert.Contains("IDENTITY(1,1) NOT NULL PRIMARY KEY", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("NVARCHAR(32)", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Mysql":
                TestAssert.Contains("AUTO_INCREMENT PRIMARY KEY", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("VARCHAR(32)", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void GetTableSelectPathBuildsPaginatedFilteredSelect(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            1,
            25,
            new List<string> { "person_id", "first_name" },
            TestData.BuildPagedSelectFilter(),
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Descending), new ResultOrder("first_name", OrderDirectionEnum.Ascending) });

        TestAssert.Contains("SELECT", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("FROM", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("ORDER BY", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.True(query.Parameters.Count >= 3);

        switch (providerName)
        {
            case "Sqlite":
            case "Postgresql":
            case "Mysql":
                TestAssert.Contains("LIMIT", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
                TestAssert.Contains("OFFSET", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("FETCH NEXT", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void GetTableSelectPathBuildsDefaultUnfilteredSelect(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            null,
            100,
            null!,
            null!,
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Ascending) });

        TestAssert.Contains("SELECT * FROM", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.DoesNotContain("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("ORDER BY", query.CommandText, StringComparison.OrdinalIgnoreCase);

        switch (providerName)
        {
            case "Sqlite":
            case "Postgresql":
            case "Mysql":
                TestAssert.Contains("LIMIT", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
                TestAssert.Contains("OFFSET", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("FETCH NEXT", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void GetTableByIdPathBuildsPrimaryKeySelect(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            null,
            100,
            null!,
            new ExpressionTree.Expr("person_id", ExpressionTree.OperatorEnum.Equals, 7),
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Ascending) });

        TestAssert.Contains("person_id", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("ORDER BY", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.True(query.Parameters.Count >= 2);
    }

    public static void GetTableByIdPathBuildsCombinedPrimaryKeyAndQuerystringFilters(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            null,
            100,
            null!,
            TestData.BuildGetByIdWithAdditionalQueryFilter(),
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Ascending) });

        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("person_id", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("first_name", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.True(query.Parameters.Count >= 3);
    }

    public static void PutSearchPathBuildsExpressionSelect(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            2,
            10,
            null!,
            TestData.BuildSearchFilter(),
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Ascending) });

        TestAssert.Contains("ORDER BY", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("person", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.True(query.Parameters.Count >= 4);
    }

    public static void PutSearchPathBuildsCombinedExpressionAndQuerystringFilters(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            null,
            100,
            null!,
            TestData.BuildSearchWithAdditionalQueryFilters(),
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Ascending) });

        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("AND", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("first_name", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("last_name", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("age", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.True(query.Parameters.Count >= 3);
    }

    public static void PostTableInsertPathBuildsInsertAndReadback(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        InsertPlan plan = builder.BuildInsert("person", TestData.SampleColumns(), TestData.SampleInsertValues());

        TestAssert.NotNull(plan);
        TestAssert.NotNull(plan.Batch);
        TestAssert.True(plan.Batch.Queries.Count >= 1);
        TestAssert.Contains("INSERT INTO", plan.Batch.Queries[0].CommandText, StringComparison.OrdinalIgnoreCase);

        switch (providerName)
        {
            case "Sqlite":
                TestAssert.True(plan.ReturnsInsertedRow);
                TestAssert.Equal(2, plan.Batch.Queries.Count);
                TestAssert.Contains("last_insert_rowid()", plan.Batch.Queries[1].CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Postgresql":
                TestAssert.True(plan.ReturnsInsertedRow);
                TestAssert.Equal(1, plan.Batch.Queries.Count);
                TestAssert.Contains("RETURNING *", plan.Batch.Queries[0].CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
                TestAssert.True(plan.ReturnsInsertedRow);
                TestAssert.Equal(1, plan.Batch.Queries.Count);
                TestAssert.Contains("OUTPUT INSERTED.*", plan.Batch.Queries[0].CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "Mysql":
                TestAssert.True(plan.ReturnsInsertedRow);
                TestAssert.Equal(2, plan.Batch.Queries.Count);
                TestAssert.Contains("LAST_INSERT_ID()", plan.Batch.Queries[1].CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void PostTableInsertPathCoercesTypedStringValues(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        InsertPlan plan = builder.BuildInsert("person", TestData.SampleColumns(), TestData.SampleStringTypedInsertValues());
        SqlQueryDefinition insertQuery = plan.Batch.Queries[0];

        TestAssert.Equal(typeof(int), insertQuery.Parameters[2].Value.GetType());
        TestAssert.Equal(40, (int)insertQuery.Parameters[2].Value);
        TestAssert.Equal(typeof(DateTime), insertQuery.Parameters[3].Value.GetType());
    }

    public static void PostTableInsertMultiplePathBuildsTransactionalMultiInsert(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlBatchDefinition batch = builder.BuildInsertMultiple(
            "person",
            TestData.SampleColumns(),
            TestData.SampleInsertMultipleValues());

        TestAssert.True(batch.UseTransaction);
        TestAssert.Equal(2, batch.Queries.Count);
        foreach (SqlQueryDefinition query in batch.Queries)
        {
            TestAssert.Contains("INSERT INTO", query.CommandText, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void PutUpdateByIdPathBuildsUpdate(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildUpdate(
            "person",
            TestData.SampleColumns(),
            new Dictionary<string, object> { ["age"] = 18 },
            new ExpressionTree.Expr("person_id", ExpressionTree.OperatorEnum.Equals, 1));

        TestAssert.Contains("UPDATE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("SET", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Equal(2, query.Parameters.Count);
    }

    public static void TypedFiltersAreCoercedUsingColumnSchema(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildSelect(
            "person",
            TestData.SampleColumns(),
            null,
            100,
            null!,
            new ExpressionTree.Expr("age", ExpressionTree.OperatorEnum.Equals, "18"),
            new[] { new ResultOrder("person_id", OrderDirectionEnum.Ascending) });

        TestAssert.Equal(typeof(int), query.Parameters[0].Value.GetType());
        TestAssert.Equal(18, (int)query.Parameters[0].Value);
    }

    public static void DeletePathsBuildDeleteQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildDelete(
            "person",
            TestData.SampleColumns(),
            new ExpressionTree.Expr("person_id", ExpressionTree.OperatorEnum.Equals, 1));

        TestAssert.Contains("DELETE FROM", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Equal(1, query.Parameters.Count);
    }

    public static void DeletePathBuildsCombinedPrimaryKeyAndQuerystringFilteredDelete(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildDelete(
            "person",
            TestData.SampleColumns(),
            TestData.BuildDeleteWithAdditionalQueryFilters());

        TestAssert.Contains("DELETE FROM", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("person_id", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("first_name", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Equal(2, query.Parameters.Count);
    }

    public static void DeletePathBuildsUnfilteredDeleteQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildDelete(
            "person",
            TestData.SampleColumns(),
            null!);

        TestAssert.Contains("DELETE FROM", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.DoesNotContain("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Equal(0, query.Parameters.Count);
    }

    public static void DeletePathBuildsQuerystringFilteredDeleteQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildDelete(
            "person",
            TestData.SampleColumns(),
            TestData.BuildDeleteWithQuerystringFiltersOnly());

        TestAssert.Contains("DELETE FROM", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("WHERE", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("first_name", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Contains("age", query.CommandText, StringComparison.OrdinalIgnoreCase);
        TestAssert.Equal(2, query.Parameters.Count);
    }

    public static void ClearPathBuildsProviderSpecificClearQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildClearTable("person");

        switch (providerName)
        {
            case "Sqlite":
                TestAssert.True(query.CommandText.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase));
                break;
            case "Postgresql":
                TestAssert.Contains("TRUNCATE TABLE", query.CommandText, StringComparison.OrdinalIgnoreCase);
                TestAssert.Contains("RESTART IDENTITY", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            case "SqlServer":
            case "Mysql":
                TestAssert.Contains("TRUNCATE TABLE", query.CommandText, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                throw new InvalidOperationException("Unknown provider " + providerName);
        }
    }

    public static void DropPathBuildsDropQuery(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        SqlQueryDefinition query = builder.BuildDropTable("person");
        TestAssert.Contains("DROP TABLE", query.CommandText, StringComparison.OrdinalIgnoreCase);
    }

    public static void RawQueryPathPassthroughIsPreserved(string providerName)
    {
        IRestDbQueryBuilder builder = TestData.CreateBuilder(providerName);
        const string raw = "SELECT * FROM person;";
        SqlQueryDefinition query = builder.BuildRawSql(raw);
        TestAssert.Equal(raw, query.CommandText);
    }
}
