using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;
using WatsonWebserver;

namespace RestDb
{
    partial class RestDbServer
    {
        static HttpResponse GetDatabase(HttpRequest req)
        {
            string dbName = req.RawUrlEntries[0];
            Database db = _Settings.GetDatabaseByName(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetDatabase unable to find database " + dbName);
                return new HttpResponse(req, false, 404, null, null, 
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            db = Database.Redact(db);
            
            bool describe = Common.IsTrue(req.RetrieveHeaderValue("_describe"));
            if (describe)
            {
                db.Tables = _Databases.GetTables(dbName, describe);
            }
            else
            {
                db.TableNames = _Databases.GetTableNames(dbName);
            }

            return new HttpResponse(req, true, 200, null, null, Common.SerializeJson(db, true), true);
        }
    }
}
