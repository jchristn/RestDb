namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using ExpressionTree;
using RestDb;
using RestDb.Storage.Interfaces;
using RestDb.Storage.Providers.Mysql;
using RestDb.Storage.Providers.Postgresql;
using RestDb.Storage.Providers.Sqlite;
using RestDb.Storage.Providers.SqlServer;

internal static class TestData
{
    public static IReadOnlyList<string> ProviderNames { get; } =
        new List<string> { "Sqlite", "Postgresql", "SqlServer", "Mysql" };

    public static IRestDbQueryBuilder CreateBuilder(string providerName)
    {
        return providerName switch
        {
            "Sqlite" => new SqliteQueryBuilder(),
            "Postgresql" => new PostgresqlQueryBuilder(),
            "SqlServer" => new SqlServerQueryBuilder(),
            "Mysql" => new MysqlQueryBuilder(),
            _ => throw new InvalidOperationException("Unknown provider " + providerName)
        };
    }

    public static List<Column> SampleColumns()
    {
        return new List<Column>
        {
            new Column { Name = "person_id", Type = "int", Nullable = false, PrimaryKey = true },
            new Column { Name = "first_name", Type = "nvarchar", MaxLength = 32, Nullable = false },
            new Column { Name = "last_name", Type = "nvarchar", MaxLength = 32, Nullable = true },
            new Column { Name = "age", Type = "int", Nullable = false },
            new Column { Name = "created", Type = "datetime", Nullable = true }
        };
    }

    public static Dictionary<string, object> SampleInsertValues()
    {
        return new Dictionary<string, object>
        {
            ["first_name"] = "joel",
            ["last_name"] = "christner",
            ["age"] = 40,
            ["created"] = "2024-01-01 00:00:00"
        };
    }

    public static Dictionary<string, object> SampleStringTypedInsertValues()
    {
        return new Dictionary<string, object>
        {
            ["first_name"] = "joel",
            ["last_name"] = "christner",
            ["age"] = "40",
            ["created"] = "2024-01-01 00:00:00"
        };
    }

    public static List<Dictionary<string, object>> SampleInsertMultipleValues()
    {
        return new List<Dictionary<string, object>>
        {
            SampleInsertValues(),
            new Dictionary<string, object>
            {
                ["first_name"] = "jane",
                ["last_name"] = "doe",
                ["age"] = 35,
                ["created"] = "2024-01-02 00:00:00"
            }
        };
    }

    public static Database CreateDatabase(TestRuntimeConfiguration runtime, string? sqliteFilenameOverride = null)
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));

        return new Database
        {
            Name = runtime.DatabaseName,
            Type = runtime.DatabaseType,
            Filename = sqliteFilenameOverride ?? runtime.Filename,
            Hostname = runtime.Hostname,
            Port = runtime.Port,
            Instance = runtime.Instance,
            Username = runtime.Username,
            Password = runtime.Password,
            Debug = runtime.Debug
        };
    }

    public static Expr BuildPagedSelectFilter()
    {
        Expr filter = new Expr("first_name", OperatorEnum.Equals, "joel");
        return Expr.PrependAndClause(new Expr("age", OperatorEnum.GreaterThanOrEqualTo, 18), filter);
    }

    public static Expr BuildGetByIdWithAdditionalQueryFilter()
    {
        Expr filter = new Expr("person_id", OperatorEnum.Equals, 7);
        return Expr.PrependAndClause(new Expr("first_name", OperatorEnum.Equals, "joel"), filter);
    }

    public static Expr BuildSearchFilter()
    {
        Expr filter = new Expr("last_name", OperatorEnum.StartsWith, "Chr");
        filter = Expr.PrependAndClause(new Expr("created", OperatorEnum.IsNotNull, null), filter);
        return Expr.PrependOrClause(new Expr("age", OperatorEnum.In, new List<object> { 18, 19 }), filter);
    }

    public static Expr BuildSearchWithAdditionalQueryFilters()
    {
        Expr filter = new Expr("age", OperatorEnum.GreaterThanOrEqualTo, 18);
        filter = Expr.PrependAndClause(new Expr("first_name", OperatorEnum.Equals, "joel"), filter);
        return Expr.PrependAndClause(new Expr("last_name", OperatorEnum.Equals, "christner"), filter);
    }

    public static Expr BuildDeleteWithAdditionalQueryFilters()
    {
        Expr filter = new Expr("person_id", OperatorEnum.Equals, 1);
        return Expr.PrependAndClause(new Expr("first_name", OperatorEnum.Equals, "joel"), filter);
    }

    public static Expr BuildDeleteWithQuerystringFiltersOnly()
    {
        Expr filter = new Expr("first_name", OperatorEnum.Equals, "joel");
        return Expr.PrependAndClause(new Expr("age", OperatorEnum.Equals, 18), filter);
    }
}
