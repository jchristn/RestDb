using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class Settings
    {
        #region Public-Members

        public string Version { get; set; } = "1.0.0";
        public ServerSettings Server { get; set; } = new ServerSettings();
        public LoggingSettings Logging { get; set; } = new LoggingSettings();
        public List<Database> Databases { get; set; } = new List<Database>();
        public List<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();

        #endregion

        #region Private-Members

        #endregion

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
    }

    public class ServerSettings
    {
        public string ListenerHostname { get; set; } = "localhost";
        public int ListenerPort { get; set; } = 8000;
        public bool Ssl { get; set; } = false;
        public bool Debug { get; set; } = false;
        public bool RequireAuthentication { get; set; } = false;
        public string ApiKeyHeader { get; set; } = "x-api-key";

        public ServerSettings()
        {

        }
    }

    public class LoggingSettings
    {
        public string ServerIp { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 514;
        public int MinimumLevel { get; set; } = 1;
        public bool LogHttpRequests { get; set; } = false;
        public bool LogHttpResponses { get; set; } = false;
        public bool ConsoleLogging { get; set; } = true;

        public LoggingSettings()
        {

        }
    }
}
