namespace RestDb.Storage
{
    using System;
    using RestDb.Storage.Providers.Mysql;
    using RestDb.Storage.Providers.Postgresql;
    using RestDb.Storage.Providers.Sqlite;
    using RestDb.Storage.Providers.SqlServer;

    /// <summary>
    /// Database driver factory.
    /// </summary>
    internal static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a driver for the supplied database settings.
        /// </summary>
        /// <param name="database">Database settings.</param>
        /// <param name="logger">Optional debug logger.</param>
        /// <returns>Driver.</returns>
        internal static DatabaseDriverBase Create(Database database, Action<string> logger = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            switch (database.Type)
            {
                case DbTypeEnum.Sqlite:
                    return new SqliteDatabaseDriver(database, logger);
                case DbTypeEnum.Postgresql:
                    return new PostgresqlDatabaseDriver(database, logger);
                case DbTypeEnum.SqlServer:
                    return new SqlServerDatabaseDriver(database, logger);
                case DbTypeEnum.Mysql:
                    return new MysqlDatabaseDriver(database, logger);
                default:
                    throw new NotSupportedException("Unsupported database type: " + database.Type);
            }
        }
    }
}
