namespace RestDb.Storage.Providers.Postgresql
{
    using System.Data.Common;
    using Npgsql;

    /// <summary>
    /// PostgreSQL database driver.
    /// </summary>
    internal class PostgresqlDatabaseDriver : AdoDatabaseDriverBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="logger">Optional debug logger.</param>
        public PostgresqlDatabaseDriver(Database settings, System.Action<string> logger = null) : base(settings, new PostgresqlQueryBuilder(), logger)
        {
        }

        /// <inheritdoc />
        protected override DbProviderFactory ProviderFactory => NpgsqlFactory.Instance;

        /// <inheritdoc />
        protected override string BuildConnectionString()
        {
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
            {
                Host = Settings.Hostname,
                Port = Settings.Port ?? 5432,
                Username = Settings.Username,
                Password = Settings.Password,
                Database = Settings.Name,
                Pooling = true
            };
            return builder.ConnectionString;
        }
    }
}
