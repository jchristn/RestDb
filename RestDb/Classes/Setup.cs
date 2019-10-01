using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    internal class Setup
    {
        #region Constructors-and-Factories

        internal Setup()
        {
            RunSetup();
        }

        #endregion

        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void RunSetup()
        {
            #region General

            if (Console.WindowWidth < 79) Console.WindowWidth = 79;

            Settings ret = new Settings();
            ret.Version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;

            ret.Logging = LoggingSettings.Default();

            ret.ApiKeys = new List<ApiKey>();
            ret.ApiKeys.Add(ApiKey.Default());

            #endregion

            #region Server-Settings

            //                 0        1         2         3         4         5         6         7
            //                 1234567890123456789012345678901234567890123456789012345678901234567890123456789
            Console.WriteLine("RestDb Setup");
            Console.WriteLine("------------");
            Console.WriteLine("We'll collect some values and put together your initial configuration.");
            Console.WriteLine("");
            Console.WriteLine("On which hostname should this node listen?  The hostname supplied here MUST");
            Console.WriteLine("match the host header received on incoming RESTful HTTP requests.  It is");
            Console.WriteLine("recommended that you use the DNS hostname of the machine.");
            Console.WriteLine("");
            Console.WriteLine("Important: if you use localhost or 127.0.0.1, RestDb will only be able to");
            Console.WriteLine("accept requests from within the local system.  If you use *, +, or 0.0.0.0 to");
            Console.WriteLine("represent any address, you will have to run RestDb with admin privileges.");
            Console.WriteLine("");

            ret.Server = new ServerSettings();
            ret.Server.ListenerHostname = Common.InputString("Hostname?", "localhost", false);
            ret.Server.ListenerPort = Common.InputInteger("Port number?", 8000, true, false);
            ret.Server.Ssl = Common.InputBoolean("Require SSL?", false);
            ret.Server.Debug = false;
            ret.Server.ApiKeyHeader = "x-api-key";

            Console.WriteLine("");

            #endregion

            #region Database-Settings

            List<Database> databases = new List<Database>();

            while (true)
            {
                Console.WriteLine("");
                Console.WriteLine("Databases");
                Console.WriteLine("---------");
                if (databases.Count > 0)
                {
                    Console.WriteLine("The following databases are configured:");
                    foreach (Database db in databases)
                    {
                        Console.WriteLine(db.ToString());
                    }

                    Console.WriteLine("");
                }

                //                 0        1         2         3         4         5         6         7
                //                 1234567890123456789012345678901234567890123456789012345678901234567890123456789
                Console.WriteLine("Press ENTER on the database name (blank line) to stop adding databases.");
                Console.WriteLine("");

                Database curr = new Database();
                curr.Name = Common.InputString("Database name?", null, true);

                if (String.IsNullOrEmpty(curr.Name))
                {
                    if (databases.Count < 1)
                    {
                        Console.WriteLine("Error: At least one database must be configured.");
                        Console.WriteLine("");
                        continue;
                    }

                    break;
                }

                string currType = Common.InputString("Database type [mssql|mysql|pgsql]?", "mssql", false);
                if (!currType.Equals("mssql") && !currType.Equals("mysql") && !currType.Equals("pgsql"))
                {
                    Console.WriteLine("Error: Use mssql, mysql, or pgsql for the database type.");
                    Console.WriteLine("");
                    continue;
                }

                curr.Type = currType;
                curr.Hostname = Common.InputString("Server hostname?", "localhost", false);

                if (curr.Type.Equals("mssql")) curr.Port = Common.InputInteger("Server port?", 1433, true, false);
                else if (curr.Type.Equals("mysql")) curr.Port = Common.InputInteger("Server port?", 3306, true, false);
                else curr.Port = Common.InputInteger("Server port?", 5432, true, false);

                curr.Instance = null;
                if (curr.Type.Equals("mssql")) curr.Instance = Common.InputString("Instance name?", null, true);

                curr.Username = Common.InputString("Username?", null, false);
                curr.Password = Common.InputString("Password?", null, false);
                curr.Debug = false;
                databases.Add(curr);
            }

            ret.Databases = databases;
            Console.WriteLine("");

            #endregion

            #region Finish

            //                 0        1         2         3         4         5         6         7
            //                 1234567890123456789012345678901234567890123456789012345678901234567890123456789
            Console.WriteLine("All set!  We're writing your configuration to System.Json.  It is important");
            Console.WriteLine("to note that, by default, authentication via API key is **DISABLED**.  You");
            Console.WriteLine("should modify your System.Json file to add API keys to the 'ApiKeys' section.");
            Console.WriteLine("Then, set 'RequireAuthentication' to true in the 'Server' section.");
            Console.WriteLine("Once API keys are added and authentication is set to required, requests will");
            Console.WriteLine("need to be made including the x-api-key header.");
            Console.WriteLine("");

            ret.ToFile("System.Json");

            #endregion
        }

        #endregion 
    }
}
