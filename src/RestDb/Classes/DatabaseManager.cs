namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using SyslogLogging;
    using DatabaseWrapper;
    using DatabaseWrapper.Core;

    internal class DatabaseManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private Settings _Settings;
        private LoggingModule _Logging;
        private Dictionary<string, DatabaseClient> _Databases;
        private readonly object _DatabasesLock;

        #endregion

        #region Constructors-and-Factories

        internal DatabaseManager(Settings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Settings = settings;
            _Logging = logging;
            _Databases = new Dictionary<string, DatabaseClient>();
            _DatabasesLock = new object();

            InitializeDatabases();
        }

        #endregion

        #region Internal-Methods

        internal List<string> ListDatabasesByName()
        {
            List<string> ret = new List<string>();

            lock (_DatabasesLock)
            {
                foreach (KeyValuePair<string, DatabaseClient> curr in _Databases)
                {
                    ret.Add(curr.Key);
                }

                return ret;
            }
        }

        internal Database GetDatabaseByName(string dbName)
        {
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            return _Settings.GetDatabaseByName(dbName);
        }

        internal List<Table> GetTables(string dbName, bool describe)
        {
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            
            DatabaseClient db = GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Warn("GetTables unable to find client for database " + dbName);
                return null;
            }

            List<string> tableNames = db.ListTables();
            if (tableNames == null || tableNames.Count < 1)
            {
                _Logging.Warn("GetTables no tables returned from list tables for database " + dbName);
                return new List<Table>();
            }
            else
            {
                _Logging.Debug("GetTables returning " + tableNames.Count + " tables for database " + dbName);
            }

            List<Table> ret = new List<Table>();

            foreach (string curr in tableNames)
            {
                Table currTable = new Table();
                currTable.Name = curr;
                
                if (describe)
                {
                    currTable.Columns = new List<Column>();
                    List<DatabaseWrapper.Core.Column> columns = db.DescribeTable(curr);
                    if (columns == null || columns.Count < 1)
                    {
                        _Logging.Warn("GetTables no columns found for table " + curr + " in database " + dbName);
                        ret.Add(currTable);
                        continue;
                    }

                    foreach (DatabaseWrapper.Core.Column currColumn in columns)
                    {
                        Column tempColumn = new Column();
                        tempColumn.Name = currColumn.Name;
                        tempColumn.Nullable = currColumn.Nullable;
                        tempColumn.MaxLength = currColumn.MaxLength;
                        tempColumn.Type = currColumn.Type;
                        if (currColumn.PrimaryKey) currTable.PrimaryKey = tempColumn.Name;

                        currTable.Columns.Add(tempColumn);
                        // _Logging.Debug("GetTables adding column " + tempColumn.Name + " for table " + currTable.Name + " database " + dbName);
                    }
                }

                ret.Add(currTable);
            }

            return ret;
        }

        internal List<string> GetTableNames(string dbName)
        {            
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
             
            DatabaseClient db = GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Warn("GetTableNames unable to find client for database " + dbName);
                return null;
            }

            List<string> tableNames = db.ListTables();
            if (tableNames == null || tableNames.Count < 1)
            {
                _Logging.Debug("GetTableNames no tables returned from list tables for database " + dbName);
                return new List<string>();
            }

            return tableNames;
        }

        internal Table GetTableByName(string dbName, string tableName)
        {
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            DatabaseClient db = GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Warn("GetTableByName unable to find client for database " + dbName);
                return null;
            }

            Table ret = new Table();
            ret.Name = tableName;
            ret.Columns = new List<Column>();

            List<DatabaseWrapper.Core.Column> columns = db.DescribeTable(tableName);
            if (columns == null || columns.Count < 1)
            {
                _Logging.Warn("GetTableByName no columns found for table " + tableName + " in database " + dbName);
                return null;
            }
             
            foreach (Column currColumn in columns)
            {
                Column tempColumn = new Column();
                tempColumn.Name = currColumn.Name;
                tempColumn.Nullable = currColumn.Nullable;
                tempColumn.MaxLength = currColumn.MaxLength; 
                tempColumn.Type = currColumn.Type;
                if (currColumn.PrimaryKey)
                {
                    tempColumn.PrimaryKey = true;
                    ret.PrimaryKey = tempColumn.Name;
                }

                ret.Columns.Add(tempColumn);
            }

            return ret;
        }

        internal DatabaseClient GetDatabaseClient(string dbName)
        {
            lock (_DatabasesLock)
            {
                foreach (KeyValuePair<string, DatabaseClient> curr in _Databases)
                {
                    if (curr.Key.ToLower().Equals(dbName.ToLower())) return curr.Value;
                }

                return null;
            }
        }

        #endregion

        #region Private-Methods

        private void InitializeDatabases()
        {
            _Databases = new Dictionary<string, DatabaseClient>();

            foreach (Database curr in _Settings.Databases)
            {
                _Logging.Debug("InitializeDatabases initializing db " + curr.ToString());

                DatabaseClient db = null;

                switch (curr.Type)
                {
                    case DbTypeEnum.Sqlite:
                        db = new DatabaseClient(curr.Filename);
                        break;
                    case DbTypeEnum.SqlServer:
                    case DbTypeEnum.Mysql:
                    case DbTypeEnum.Postgresql:
                        db = new DatabaseClient(
                            curr.Type,
                            curr.Hostname,
                            Convert.ToInt32(curr.Port),
                            curr.Username,
                            curr.Password,
                            curr.Instance,
                            curr.Name);
                        break;
                    default:
                        throw new ArgumentException("Unknown database type: " + curr.Type.ToString()); 
                } 

                if (curr.Debug)
                {
                    db.Settings.Debug.Logger = Logger;
                    db.Settings.Debug.EnableForQueries = true;
                    db.Settings.Debug.EnableForResults = true;
                }

                _Databases.Add(curr.Name, db);
            }
        }

        private void Logger(string msg)
        {
            _Logging.Debug(msg);
        }

        #endregion 
    }
}
