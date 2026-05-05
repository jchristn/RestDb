namespace RestDb.Storage.Providers.Mysql
{
    using System.Data.Common;
    using MySqlConnector;

    /// <summary>
    /// MySQL database driver.
    /// </summary>
    internal class MysqlDatabaseDriver : AdoDatabaseDriverBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="logger">Optional debug logger.</param>
        public MysqlDatabaseDriver(Database settings, System.Action<string> logger = null) : base(settings, new MysqlQueryBuilder(), logger)
        {
        }

        /// <inheritdoc />
        protected override DbProviderFactory ProviderFactory => MySqlConnectorFactory.Instance;

        /// <inheritdoc />
        protected override string BuildConnectionString()
        {
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = Settings.Hostname,
                Port = (uint)(Settings.Port ?? 3306),
                UserID = Settings.Username,
                Password = Settings.Password,
                Database = Settings.Name,
                AllowUserVariables = true,
                Pooling = true
            };
            return builder.ConnectionString;
        }
    }
}
