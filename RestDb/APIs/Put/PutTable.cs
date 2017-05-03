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
        static HttpResponse PutTable(HttpRequest req)
        {
            string dbName = req.RawUrlEntries[0];
            string tableName = req.RawUrlEntries[1];
            int? idVal = req.RetrieveIdValue();

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "PutTable unknown table " + tableName + " in database " + dbName);
                return new HttpResponse(req, false, 404, null, null, 
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "PutTable unable to retrieve database client for database " + dbName);
                return new HttpResponse(req, false, 404, null, null,
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            if (req.Data == null || req.Data.Length < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "PutTable no request body supplied");
                return new HttpResponse(req, false, 400, null, null,
                    Common.SerializeJson(new ErrorResponse("Bad request", "No request body supplied"), true), true);
            }

            if (idVal == null 
                && req.RawUrlEntries.Count == 2 
                && (req.QuerystringEntries == null || req.QuerystringEntries.Count < 1))
            {
                #region Search-Table

                Expression e = DeserializeExpression(req.Data, false);

                int? indexStart = null;
                int? maxResults = null;
                List<string> returnFields = null;

                if (req.QuerystringEntries != null && req.QuerystringEntries.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in req.QuerystringEntries)
                    {
                        if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                        e = Expression.PrependAndClause(
                            new Expression(currKvp.Key, Operators.Equals, currKvp.Value),
                            e);
                    }
                }

                if (req.QuerystringEntries.ContainsKey("_index_start")) indexStart = Convert.ToInt32(req.QuerystringEntries["_index_start"]);
                if (req.QuerystringEntries.ContainsKey("_max_results")) maxResults = Convert.ToInt32(req.QuerystringEntries["_max_results"]);
                if (req.QuerystringEntries.ContainsKey("_return_fields")) returnFields = Common.CsvToStringList(req.QuerystringEntries["_return_fields"]);

                DataTable result = db.Select(tableName, indexStart, maxResults, returnFields, e, null);

                if (result == null || result.Rows.Count < 1)
                {
                    return new HttpResponse(req, true, 200, null, null,
                        Common.SerializeJson(new List<dynamic>(), true), true);
                }
                else
                {
                    return new HttpResponse(req, true, 200, null, null,
                        Common.SerializeJson(Common.DataTableToListDynamic(result), true), true);
                } 

                #endregion
            }
            else
            {
                #region Update-Object

                if (String.IsNullOrEmpty(currTable.PrimaryKey))
                {
                    _Logging.Log(LoggingModule.Severity.Warn, "PutTable no primary key defined for table " + tableName + " in database " + dbName);
                    return new HttpResponse(req, false, 400, null, null,
                        Common.SerializeJson(new ErrorResponse("Bad request", "No primary key for table " + tableName), true), true);
                }

                Dictionary<string, object> dict = Common.DeserializeJson<Dictionary<string, object>>(req.Data);
                Expression e = new Expression(currTable.PrimaryKey, Operators.Equals, idVal);
                DataTable result = db.Update(tableName, dict, e);
                return new HttpResponse(req, true, 200, null, null, Common.SerializeJson(Common.DataTableToDynamic(result), true), true);

                #endregion
            }
        }
    }
}
