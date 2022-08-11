using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestDb.Classes;

namespace RestDb
{
    /// <summary>
    /// Settings.
    /// </summary>
    public class Settings
    {
        #region Public-Members

        /// <summary>
        /// Server settings.
        /// </summary>
        public ServerSettings Server { get; set; } = new ServerSettings();

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Databases.
        /// </summary>
        public List<Database> Databases { get; set; } = new List<Database>();

        /// <summary>
        /// API keys.
        /// </summary>
        public List<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Settings()
        {

        }

        /// <summary>
        /// Instantiate from file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns>Settings.</returns>
        public static Settings FromFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            return SerializationHelper.DeserializeJson<Settings>(File.ReadAllBytes(filename));
        }

        /// <summary>
        /// Write to file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        public void ToFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            File.WriteAllBytes(filename, Encoding.UTF8.GetBytes(SerializationHelper.SerializeJson(this, true)));
            return;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve database names.
        /// </summary>
        /// <returns>List of string.</returns>
        public List<string> GetDatabaseNames()
        {
            List<string> ret = new List<string>();
            foreach (Database curr in Databases)
            {
                ret.Add(curr.Name);
            }
            return ret;
        }

        /// <summary>
        /// Get database by name.
        /// </summary>
        /// <param name="name">Database name.</param>
        /// <returns>Database.</returns>
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

    /// <summary>
    /// Server settings.
    /// </summary>
    public class ServerSettings
    {
        /// <summary>
        /// Listener hostname.
        /// </summary>
        public string ListenerHostname { get; set; } = "localhost";

        /// <summary>
        /// Listener port.
        /// </summary>
        public int ListenerPort { get; set; } = 8000;

        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Enable or disable debugging.
        /// </summary>
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Flag to specify whether or not API key authentication is required.
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Header in which API keys should be supplied.
        /// </summary>
        public string ApiKeyHeader { get; set; } = "x-api-key";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServerSettings()
        {

        }
    }

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Server IP.
        /// </summary>
        public string ServerIp { get; set; } = "127.0.0.1";

        /// <summary>
        /// Server port.
        /// </summary>
        public int ServerPort { get; set; } = 514;

        /// <summary>
        /// Minimum level.
        /// </summary>
        public int MinimumLevel { get; set; } = 1;

        /// <summary>
        /// Flag to indicate whether or not HTTP requests should be logged.
        /// </summary>
        public bool LogHttpRequests { get; set; } = false;

        /// <summary>
        /// Flag to indicate whether or not HTTP responses should be logged.
        /// </summary>
        public bool LogHttpResponses { get; set; } = false;

        /// <summary>
        /// Flag to indicate whether or not log messages should be sent to the console.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {

        }
    }
}
