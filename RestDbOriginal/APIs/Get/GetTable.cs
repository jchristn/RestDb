using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;

namespace RestDb
{
    partial class RestDbServer
    {
        static WatsonWebserver.HttpResponse GetTable(WatsonWebserver.HttpRequest req)
        {
            string dbName = req.RawUrlEntries[0];
            string tableName = req.RawUrlEntries[1];
            int? idVal = req.RetrieveIdValue();

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTable unknown table " + tableName + " in database " + dbName);
                return new WatsonWebserver.HttpResponse(req, false, 404, null, null, 
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTable unable to retrieve database client for database " + dbName);
                return new WatsonWebserver.HttpResponse(req, false, 404, null, null,
                    Common.SerializeJson(new ErrorResponse("Not found", null), true), true);
            }

            #region Check-for-Describe

            bool describe = Common.IsTrue(req.RetrieveHeaderValue("_describe"));
            if (describe)
            { 
                return new WatsonWebserver.HttpResponse(req, true, 200, null, null,
                    Common.SerializeJson(currTable, true), true);
            }

            #endregion
            
            #region Retrieve-Objects

            DataTable result = null;

            int? indexStart = null;
            int? maxResults = null;
            string orderBy = null;
            List<string> returnFields = null;
            Expression filter = null; 

            if (idVal != null)
            {
                if (String.IsNullOrEmpty(currTable.PrimaryKey))
                {
                    _Logging.Log(LoggingModule.Severity.Warn, "GetTable no primary key defined for table " + tableName + " in database " + dbName);
                    return new WatsonWebserver.HttpResponse(req, false, 400, null, null, 
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

            if (req.QuerystringEntries.ContainsKey("_index_start")) indexStart = Convert.ToInt32(req.QuerystringEntries["_index_start"]);
            if (req.QuerystringEntries.ContainsKey("_max_results")) maxResults = Convert.ToInt32(req.QuerystringEntries["_max_results"]);
            if (req.QuerystringEntries.ContainsKey("_order_by")) orderBy = HttpUtility.UrlDecode(req.QuerystringEntries["_order_by"]); 
            if (req.QuerystringEntries.ContainsKey("_return_fields")) returnFields = Common.CsvToStringList(req.QuerystringEntries["_return_fields"]);

            result = db.Select(tableName, indexStart, maxResults, returnFields, filter, orderBy);

            if (result == null || result.Rows.Count < 1)
            {
                return new WatsonWebserver.HttpResponse(req, true, 200, null, null,
                    Common.SerializeJson(new List<dynamic>(), true), true);
            }
            else
            {
                return new WatsonWebserver.HttpResponse(req, true, 200, null, null,
                    Common.SerializeJson(Common.DataTableToListDynamic(result), true), true);
            }

            #endregion 
        }
    }
}
