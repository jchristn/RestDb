namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using DatabaseWrapper.Core;
    using GetSomeInput;

    internal class Setup
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        internal Setup()
        {
            RunSetup();
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void RunSetup()
        {
            #region Welcome-and-General
             
            Console.WriteLine(
                Environment.NewLine +
                Environment.NewLine +
                @"                 _      _ _      " + Environment.NewLine +
                @"   _ __ ___  ___| |_ __| | |__   " + Environment.NewLine +
                @"  | '__/ _ \/ __| __/ _  |  _ \  " + Environment.NewLine +
                @"  | | |  __/\__ \ || (_| | |_) | " + Environment.NewLine +
                @"  |_|  \___||___/\__\__,_|_.__/  " + Environment.NewLine +
                Environment.NewLine +
                Environment.NewLine);

            Settings ret = new Settings();
            ret.Logging = new LoggingSettings();
            ret.ApiKeys = new List<ApiKey>();
            ret.ApiKeys.Add(new ApiKey());

            #endregion

            #region Server-Settings

            //                 0        1         2         3         4         5         6         7
            //                 1234567890123456789012345678901234567890123456789012345678901234567890123456789
            Console.WriteLine("Thank you for using RestDb!");
            Console.WriteLine("");
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
            ret.Server.ListenerHostname = Inputty.GetString("Hostname?", "localhost", false);
            ret.Server.ListenerPort = Inputty.GetInteger("Port number?", 8000, true, false);
            ret.Server.Ssl = Inputty.GetBoolean("Require SSL?", false);
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
                curr.Name = Inputty.GetString("Database name?", null, true);

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
                 
                string currType = Inputty.GetString("Database type [SqlServer|Mysql|Postgresql|Sqlite]?", "Sqlite", false);
                if (!currType.Equals("Sqlite") && !currType.Equals("SqlServer") && !currType.Equals("Mysql") && !currType.Equals("Postgresql"))
                {
                    Console.WriteLine("Error: Use SqlServer, Mysql, Postgresql, or Sqlite for the database type.");
                    Console.WriteLine("");
                    continue;
                }

                curr.Type = (DbTypeEnum)(Enum.Parse(typeof(DbTypeEnum), currType));

                switch (curr.Type)
                {
                    case DbTypeEnum.Sqlite:
                        curr.Filename = Inputty.GetString("Filename?", "./database.db", false);
                        break;
                    case DbTypeEnum.SqlServer:
                        curr.Hostname = Inputty.GetString("Server hostname?", "localhost", false); 
                        curr.Port = Inputty.GetInteger("Server port?", 1433, true, false);
                        curr.Instance = Inputty.GetString("Instance name?", null, true);
                        curr.Username = Inputty.GetString("Username?", null, false);
                        curr.Password = Inputty.GetString("Password?", null, false);
                        break;
                    case DbTypeEnum.Mysql:
                        curr.Hostname = Inputty.GetString("Server hostname?", "localhost", false);
                        curr.Port = Inputty.GetInteger("Server port?", 3306, true, false);
                        curr.Instance = null;
                        curr.Username = Inputty.GetString("Username?", null, false);
                        curr.Password = Inputty.GetString("Password?", null, false);
                        break;
                    case DbTypeEnum.Postgresql:
                        curr.Hostname = Inputty.GetString("Server hostname?", "localhost", false);
                        curr.Port = Inputty.GetInteger("Server port?", 5432, true, false);
                        curr.Instance = null;
                        curr.Username = Inputty.GetString("Username?", null, false);
                        curr.Password = Inputty.GetString("Password?", null, false);
                        break;
                    default:
                        throw new ArgumentException("Unknown database type: " + curr.Type.ToString());
                }

                curr.Debug = false;
                databases.Add(curr);
            }

            ret.Databases = databases;
            Console.WriteLine("");

            #endregion

            #region Finish

            //                 0        1         2         3         4         5         6         7
            //                 1234567890123456789012345678901234567890123456789012345678901234567890123456789
            Console.WriteLine("All set!  We're writing your configuration to restdb.json.  It is important");
            Console.WriteLine("to note that, by default, authentication via API key is **DISABLED**.  You");
            Console.WriteLine("should modify your restdb.json file to add API keys to the 'ApiKeys' section.");
            Console.WriteLine("Then, set 'RequireAuthentication' to true in the 'Server' section.");
            Console.WriteLine("Once API keys are added and authentication is set to required, requests will");
            Console.WriteLine("need to be made including the x-api-key header.");
            Console.WriteLine("");

            ret.ToFile("restdb.json");

            #endregion
        }

        #endregion 
    }
}
