namespace RestDb.Test.Shared;

using System;
using RestDb;

public sealed class TestRuntimeConfiguration
{
    public DbTypeEnum DatabaseType { get; set; } = DbTypeEnum.Sqlite;

    public string DatabaseName { get; set; } = "restdb_touchstone";

    public string? Filename { get; set; }

    public string? Hostname { get; set; }

    public int? Port { get; set; }

    public string? Instance { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Schema { get; set; }

    public bool Debug { get; set; }

    public TestRuntimeConfiguration Copy()
    {
        return new TestRuntimeConfiguration
        {
            DatabaseType = DatabaseType,
            DatabaseName = DatabaseName,
            Filename = Filename,
            Hostname = Hostname,
            Port = Port,
            Instance = Instance,
            Username = Username,
            Password = Password,
            Schema = Schema,
            Debug = Debug
        };
    }

    public void Validate()
    {
        if (DatabaseType == DbTypeEnum.Sqlite)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Hostname))
        {
            throw new InvalidOperationException("Hostname is required for non-SQLite automated test execution.");
        }

        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            throw new InvalidOperationException("Database name is required for non-SQLite automated test execution.");
        }
    }
}
