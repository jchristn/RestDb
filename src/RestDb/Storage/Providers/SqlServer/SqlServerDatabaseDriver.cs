namespace RestDb.Storage.Providers.SqlServer
{
    using System.Data.Common;
    using Microsoft.Data.SqlClient;

    /// <summary>
    /// SQL Server database driver.
    /// </summary>
    internal class SqlServerDatabaseDriver : AdoDatabaseDriverBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="logger">Optional debug logger.</param>
        public SqlServerDatabaseDriver(Database settings, System.Action<string> logger = null) : base(settings, new SqlServerQueryBuilder(), logger)
        {
        }

        /// <inheritdoc />
        protected override DbProviderFactory ProviderFactory => SqlClientFactory.Instance;

        /// <inheritdoc />
        protected override string BuildConnectionString()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = BuildDataSource(),
                UserID = Settings.Username,
                Password = Settings.Password,
                InitialCatalog = Settings.Name,
                Encrypt = false,
                TrustServerCertificate = true
            };
            return builder.ConnectionString;
        }

        private string BuildDataSource()
        {
            string dataSource = Settings.Hostname;

            if (!string.IsNullOrWhiteSpace(Settings.Instance))
            {
                dataSource += "\\" + Settings.Instance;
            }

            if (Settings.Port.HasValue)
            {
                dataSource += "," + Settings.Port.Value;
            }

            return dataSource;
        }
    }
}
