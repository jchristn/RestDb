namespace RestDb
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Supported database types.
    /// </summary>
    public enum DbTypeEnum
    {
        /// <summary>
        /// SQLite.
        /// </summary>
        [EnumMember(Value = "Sqlite")]
        Sqlite,
        /// <summary>
        /// Microsoft SQL Server.
        /// </summary>
        [EnumMember(Value = "SqlServer")]
        SqlServer,
        /// <summary>
        /// MySQL.
        /// </summary>
        [EnumMember(Value = "Mysql")]
        Mysql,
        /// <summary>
        /// PostgreSQL.
        /// </summary>
        [EnumMember(Value = "Postgresql")]
        Postgresql
    }
}
