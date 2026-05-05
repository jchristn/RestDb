namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using RestDb.Storage;
    using SyslogLogging;

    internal class DatabaseManager : IDisposable
    {
        #region Private-Members

        private Settings _Settings;
        private LoggingModule _Logging;
        private Dictionary<string, DatabaseDriverBase> _Databases;
        private readonly object _DatabasesLock;

        #endregion

        #region Constructors-and-Factories

        internal DatabaseManager(Settings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Settings = settings;
            _Logging = logging;
            _Databases = new Dictionary<string, DatabaseDriverBase>();
            _DatabasesLock = new object();

            InitializeDatabases();
        }

        #endregion

        #region Internal-Methods

        public void Dispose()
        {
            lock (_DatabasesLock)
            {
                if (_Databases == null || _Databases.Count < 1) return;

                foreach (KeyValuePair<string, DatabaseDriverBase> curr in _Databases)
                {
                    curr.Value?.Dispose();
                }

                _Databases.Clear();
            }
        }

        internal List<string> ListDatabasesByName()
        {
            List<string> ret = new List<string>();

            lock (_DatabasesLock)
            {
                foreach (KeyValuePair<string, DatabaseDriverBase> curr in _Databases)
                {
                    ret.Add(curr.Key);
                }

                return ret;
            }
        }

        internal Database GetDatabaseByName(string dbName)
        {
            if (string.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            return _Settings.GetDatabaseByName(dbName);
        }

        internal async Task<List<Table>> GetTablesAsync(string dbName, bool describe)
        {
            if (string.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));

            DatabaseDriverBase db = GetDatabaseDriver(dbName);
            if (db == null)
            {
                _Logging.Warn("GetTables unable to find client for database " + dbName);
                return null;
            }

            List<string> tableNames = await db.Schema.ListTablesAsync().ConfigureAwait(false);
            if (tableNames == null || tableNames.Count < 1)
            {
                _Logging.Warn("GetTables no tables returned from list tables for database " + dbName);
                return new List<Table>();
            }

            _Logging.Debug("GetTables returning " + tableNames.Count + " tables for database " + dbName);

            List<Table> ret = new List<Table>();
            foreach (string curr in tableNames)
            {
                Table currTable = new Table
                {
                    Name = curr
                };

                if (describe)
                {
                    List<Column> columns = await db.Schema.DescribeTableAsync(curr).ConfigureAwait(false);
                    if (columns == null || columns.Count < 1)
                    {
                        _Logging.Warn("GetTables no columns found for table " + curr + " in database " + dbName);
                        ret.Add(currTable);
                        continue;
                    }

                    currTable.Columns = new List<Column>();
                    foreach (Column currColumn in columns)
                    {
                        Column tempColumn = currColumn.Copy();
                        if (currColumn.PrimaryKey) currTable.PrimaryKey = tempColumn.Name;
                        currTable.Columns.Add(tempColumn);
                    }
                }

                ret.Add(currTable);
            }

            return ret;
        }

        internal async Task<List<string>> GetTableNamesAsync(string dbName)
        {
            if (string.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));

            DatabaseDriverBase db = GetDatabaseDriver(dbName);
            if (db == null)
            {
                _Logging.Warn("GetTableNames unable to find client for database " + dbName);
                return null;
            }

            List<string> tableNames = await db.Schema.ListTablesAsync().ConfigureAwait(false);
            if (tableNames == null || tableNames.Count < 1)
            {
                _Logging.Debug("GetTableNames no tables returned from list tables for database " + dbName);
                return new List<string>();
            }

            return tableNames;
        }

        internal async Task<Table> GetTableByNameAsync(string dbName, string tableName)
        {
            if (string.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            DatabaseDriverBase db = GetDatabaseDriver(dbName);
            if (db == null)
            {
                _Logging.Warn("GetTableByName unable to find client for database " + dbName);
                return null;
            }

            string actualTableName = await ResolveTableNameAsync(db, tableName).ConfigureAwait(false);
            if (string.IsNullOrEmpty(actualTableName))
            {
                _Logging.Warn("GetTableByName unable to resolve table " + tableName + " in database " + dbName);
                return null;
            }

            List<Column> columns = await db.Schema.DescribeTableAsync(actualTableName).ConfigureAwait(false);
            if (columns == null || columns.Count < 1)
            {
                _Logging.Warn("GetTableByName no columns found for table " + actualTableName + " in database " + dbName);
                return null;
            }

            Table ret = new Table
            {
                Name = actualTableName,
                Columns = new List<Column>()
            };

            foreach (Column currColumn in columns)
            {
                Column tempColumn = currColumn.Copy();
                if (currColumn.PrimaryKey)
                {
                    tempColumn.PrimaryKey = true;
                    ret.PrimaryKey = tempColumn.Name;
                }

                ret.Columns.Add(tempColumn);
            }

            return ret;
        }

        internal DatabaseDriverBase GetDatabaseDriver(string dbName)
        {
            lock (_DatabasesLock)
            {
                foreach (KeyValuePair<string, DatabaseDriverBase> curr in _Databases)
                {
                    if (curr.Key.Equals(dbName, StringComparison.OrdinalIgnoreCase)) return curr.Value;
                }

                return null;
            }
        }

        #endregion

        #region Private-Methods

        private void InitializeDatabases()
        {
            _Databases = new Dictionary<string, DatabaseDriverBase>();

            foreach (Database curr in _Settings.Databases)
            {
                _Logging.Debug("InitializeDatabases initializing db " + curr);
                DatabaseDriverBase db = DatabaseDriverFactory.Create(curr, Logger);
                db.InitializeAsync().GetAwaiter().GetResult();
                _Databases.Add(curr.Name, db);
            }
        }

        private async Task<string> ResolveTableNameAsync(DatabaseDriverBase db, string requestedTableName)
        {
            List<string> tableNames = await db.Schema.ListTablesAsync().ConfigureAwait(false);
            if (tableNames == null || tableNames.Count < 1) return requestedTableName;

            string exact = tableNames.FirstOrDefault(t => t.Equals(requestedTableName, StringComparison.OrdinalIgnoreCase));
            return exact ?? requestedTableName;
        }

        private void Logger(string msg)
        {
            _Logging.Debug(msg);
        }

        #endregion
    }
}
