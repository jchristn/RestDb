namespace RestDb.Storage.Providers.Sqlite
{
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// SQLite database driver.
    /// </summary>
    internal class SqliteDatabaseDriver : AdoDatabaseDriverBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="logger">Optional debug logger.</param>
        public SqliteDatabaseDriver(Database settings, System.Action<string> logger = null) : base(settings, new SqliteQueryBuilder(), logger)
        {
        }

        /// <inheritdoc />
        protected override DbProviderFactory ProviderFactory => SqliteFactory.Instance;

        /// <inheritdoc />
        protected override string BuildConnectionString()
        {
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
            {
                DataSource = Settings.Filename,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            return builder.ConnectionString;
        }

        /// <inheritdoc />
        protected override async Task OnConnectionOpenedAsync(DbConnection connection, CancellationToken token = default)
        {
            using (DbCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }
    }
}
