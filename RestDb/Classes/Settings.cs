using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class Settings
    {
        #region Constructors-and-Factories

        public Settings()
        {

        }

        public static Settings FromFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            return Common.DeserializeJson<Settings>(Common.ReadBinaryFile(filename));
        }

        public void ToFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            Common.WriteFile(filename, Encoding.UTF8.GetBytes(Common.SerializeJson(this, true)));
            return;
        }

        #endregion

        #region Public-Members

        public string Version { get; set; } 
        public ServerSettings Server { get; set; }
        public LoggingSettings Logging { get; set; }
        public List<Database> Databases { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        public List<string> DatabaseNames()
        {
            List<string> ret = new List<string>();
            foreach (Database curr in Databases)
            {
                ret.Add(curr.Name);
            }
            return ret;
        }

        public Database GetDatabaseByName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            
            foreach (Database curr in Databases)
            {
                if (curr.Name.ToLower().Equals(name.ToLower())) return curr;
            }

            return null;
        }

        #endregion

        #region Private-Methods

        #endregion

        #region Public-Static-Methods

        #endregion

        #region Private-Static-Methods

        #endregion
    }

    public class ServerSettings
    {
        public string ListenerHostname;
        public int ListenerPort;
        public bool Ssl;
        public bool Debug;

        public static ServerSettings Default()
        {
            ServerSettings ret = new ServerSettings();
            ret.ListenerHostname = "localhost";
            ret.ListenerPort = 8000;
            ret.Ssl = false;
            ret.Debug = false;
            return ret;
        }
    }

    public class LoggingSettings
    {
        public string ServerIp;
        public int ServerPort;
        public int MinimumLevel;
        public bool LogHttpRequests;
        public bool LogHttpResponses;
        public bool ConsoleLogging;

        public static LoggingSettings Default()
        {
            LoggingSettings ret = new LoggingSettings();
            ret.ServerIp = "127.0.0.1";
            ret.ServerPort = 514;
            ret.MinimumLevel = 1;
            ret.LogHttpRequests = false;
            ret.LogHttpResponses = false;
            ret.ConsoleLogging = true;
            return ret;
        }
    }
}
