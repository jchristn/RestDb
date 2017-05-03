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
        static HttpResponse DeleteTable(HttpRequest req)
        {
            string dbName = req.RawUrlEntries[0];
            string tableName = req.RawUrlEntries[1];
            int? idVal = req.RetrieveIdValue();

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "DeleteTable unknown table " + tableName + " in database " + dbName);
                return new HttpResponse(req, false, 404, null, null, 
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "DeleteTable unable to retrieve database client for database " + dbName);
                return new HttpResponse(req, false, 404, null, null,
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            if (idVal == null 
                && req.RawUrlEntries.Count == 2)
            {
                #region Retrieve-Table

                bool truncate = Common.IsTrue(req.RetrieveHeaderValue("_truncate"));
                if (!truncate)
                {
                    _Logging.Log(LoggingModule.Severity.Warn, "DeleteTable table deletion not allowed without setting _truncate in querystring for " + tableName + " in database " + dbName);
                    return new HttpResponse(req, false, 400, null, null,
                        Common.SerializeJson(new ErrorResponse("Bad request", "Cannot truncate table without setting _truncate in querystring to true"), true), true);
                }

                db.Truncate(tableName);
                _Logging.Log(LoggingModule.Severity.Warn, "DeleteTable truncated table " + tableName + " in database " + dbName);
                return new HttpResponse(req, true, 200, null, null, null, true);

                #endregion
            }
            else
            {
                #region Delete-Objects

                DataTable result = null;
                Expression filter = null; 

                if (idVal != null)
                {
                    if (String.IsNullOrEmpty(currTable.PrimaryKey))
                    {
                        _Logging.Log(LoggingModule.Severity.Warn, "DeleteTable no primary key defined for table " + tableName + " in database " + dbName);
                        return new HttpResponse(req, false, 400, null, null, 
                            Common.SerializeJson(new ErrorResponse("Bad request", "No primary key for table " + tableName), true), true);
                    }

                    filter = new Expression(currTable.PrimaryKey, Operators.Equals, idVal);
                }

                if (req.QuerystringEntries != null && req.QuerystringEntries.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in req.QuerystringEntries)
                    {
                        if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                        if (filter == null) filter = new Expression(currKvp.Key, Operators.Equals, currKvp.Value);
                        else filter = Expression.PrependAndClause(
                            new Expression(currKvp.Key, Operators.Equals, currKvp.Value),
                            filter);
                    }
                }

                result = db.Delete(tableName, filter);
                return new HttpResponse(req, true, 200, null, null, null, true);
                 
                #endregion
            }
        }
    }
}
