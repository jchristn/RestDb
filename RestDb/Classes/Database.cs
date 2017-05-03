using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class Database
    {
        #region Constructors-and-Factories

        public Database()
        {

        }

        #endregion

        #region Public-Members

        public string Name { get; set; }
        public string Type { get; set; }
        public string Hostname { get; set; }
        public int Port { get; set; }
        public string Instance { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Debug { get; set; }

        public List<Table> Tables { get; set; }
        public List<string> TableNames { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        public override string ToString()
        {
            string ret = "";
            ret += Name + " [" + Type.ToString() + "] " + Hostname + ":" + Port + " " + Instance + " User: " + Username;
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
