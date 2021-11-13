﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseWrapper.Core;

namespace RestDb
{
    public class Database
    {
        #region Public-Members

        public string Name { get; set; } = "Database";
        public DbTypes Type { get; set; } = DbTypes.Sqlite;
        public string Filename { get; set; } = "database.db";
        public string Hostname { get; set; } = null;
        public int? Port { get; set; } = null;
        public string Instance { get; set; } = null;
        public string Username { get; set; } = null;
        public string Password { get; set; } = null;
        public bool Debug { get; set; } = false;

        public List<Table> Tables { get; set; } = new List<Table>();
        public List<string> TableNames { get; set; } = new List<string>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        public Database()
        {

        }

        #endregion

        #region Public-Methods

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
