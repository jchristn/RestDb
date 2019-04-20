using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;

namespace RestDb
{
    partial class RestDbServer
    {
        static HttpResponse PostTable(HttpRequest req)
        {
            string dbName = req.RawUrlEntries[0];
            string tableName = req.RawUrlEntries[1]; 

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "PostTable unknown table " + tableName + " in database " + dbName);
                return new HttpResponse(req, false, 404, null, null, 
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "PostTable unable to retrieve database client for database " + dbName);
                return new HttpResponse(req, false, 404, null, null,
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            if (req.Data == null || req.Data.Length < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "PostTable no request body supplied");
                return new HttpResponse(req, false, 400, null, null,
                    Common.SerializeJson(new ErrorResponse("Bad request", "No request body supplied"), true), true);
            }

            Dictionary<string, object> dict = Common.DeserializeJson<Dictionary<string, object>>(req.Data);
            DataTable result = db.Insert(tableName, dict);
            return new HttpResponse(req, true, 200, null, null, Common.SerializeJson(Common.DataTableToDynamic(result), true), true);  
        }
    }
}
