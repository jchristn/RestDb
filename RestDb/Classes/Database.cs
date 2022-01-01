using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseWrapper.Core;

namespace RestDb
{
    /// <summary>
    /// Database settings.
    /// </summary>
    public class Database
    {
        #region Public-Members

        /// <summary>
        /// Database name.
        /// </summary>
        public string Name { get; set; } = "Database";

        /// <summary>
        /// Database type.
        /// </summary>
        public DbTypes Type { get; set; } = DbTypes.Sqlite;

        /// <summary>
        /// Database filename.
        /// </summary>
        public string Filename { get; set; } = null;

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Port.
        /// </summary>
        public int? Port { get; set; } = null;

        /// <summary>
        /// For SQL Server and SQL Server Express, the instance name.
        /// </summary>
        public string Instance { get; set; } = null;

        /// <summary>
        /// The username.
        /// </summary>
        public string Username { get; set; } = null;

        /// <summary>
        /// Password.
        /// </summary>
        public string Password { get; set; } = null;

        /// <summary>
        /// Flag to enable or disable debugging.
        /// </summary>
        public bool Debug { get; set; } = false;

        /// <summary>
        /// List of tables.
        /// </summary>
        public List<Table> Tables { get; set; } = new List<Table>();

        /// <summary>
        /// List of table names.
        /// </summary>
        public List<string> TableNames { get; set; } = new List<string>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Database()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Human-readable string.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret = "";

            switch (Type)
            {
                case DbTypes.Sqlite:
                    ret += Name + " [" + Type.ToString() + "] ";
                    break;
                default:
                    ret += Name + " [" + Type.ToString() + "] " + Hostname + ":" + Port + " " + Instance + " User: " + Username;
                    break;
            }

            return ret;
        }

        #endregion

        #region Private-Methods

        #endregion

        #region Public-Static-Methods

        /// <summary>
        /// Redact.
        /// </summary>
        /// <param name="db">Database.</param>
        /// <returns>Redacted database.</returns>
        public static Database Redact(Database db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            Database ret = new Database();
            ret.Name = db.Name;
            ret.Type = db.Type;
            ret.Hostname = db.Hostname;
            ret.Port = db.Port;
            ret.Instance = db.Instance;
            return ret;
        }

        #endregion

        #region Private-Static-Methods

        #endregion
    }
}
