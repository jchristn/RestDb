using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;
using DatabaseWrapper.Core;

namespace RestDb
{
    partial class RestDbServer
    {
        static async Task PutTable(HttpContext ctx)
        {
            string dbName = ctx.Request.RawUrlEntries[0];
            string tableName = ctx.Request.RawUrlEntries[1];
            int idVal = 0;
            if (ctx.Request.RawUrlEntries.Count == 3) Int32.TryParse(ctx.Request.RawUrlEntries[2], out idVal);

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }

            if (ctx.Request.Data == null || ctx.Request.ContentLength < 1)
            {
                _Logging.Warn("PutTable no request body supplied");
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "No request body supplied"), true));
                return;
            }

            if (idVal > 0 
                && ctx.Request.RawUrlEntries.Count == 2)
            {
                #region Search-Table

                byte[] reqData = Common.StreamToBytes(ctx.Request.Data);
                Expression e = DeserializeExpression(reqData, Common.IsTrue(ctx.Request.RetrieveHeaderValue("_debug")));

                int? indexStart = null;
                int? maxResults = null;
                List<string> returnFields = null;
                string order = null;
                string orderBy = null;
                List<ResultOrder> resultOrderList = new List<ResultOrder>();
                ResultOrder[] resultOrder = null;

                if (ctx.Request.QuerystringEntries != null && ctx.Request.QuerystringEntries.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in ctx.Request.QuerystringEntries)
                    {
                        if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                        e = Expression.PrependAndClause(
                            new Expression(currKvp.Key, Operators.Equals, currKvp.Value),
                            e);
                    }
                }

                if (ctx.Request.QuerystringEntries.ContainsKey("_index_start")) indexStart = Convert.ToInt32(ctx.Request.QuerystringEntries["_index_start"]);
                if (ctx.Request.QuerystringEntries.ContainsKey("_max_results")) maxResults = Convert.ToInt32(ctx.Request.QuerystringEntries["_max_results"]);
                if (ctx.Request.QuerystringEntries.ContainsKey("_return_fields")) returnFields = Common.CsvToStringList(ctx.Request.QuerystringEntries["_return_fields"]);
                if (ctx.Request.QuerystringEntries.ContainsKey("_order")) order = ctx.Request.QuerystringEntries["_order"];
                if (ctx.Request.QuerystringEntries.ContainsKey("_order_by")) orderBy = ctx.Request.QuerystringEntries["_order_by"];

                if (!String.IsNullOrEmpty(orderBy) && !String.IsNullOrEmpty(order))
                {
                    List<string> orderByElements = new List<string>();
                    if (orderBy.Contains(","))
                    {
                        orderByElements = Common.CsvToStringList(orderBy);
                    }
                    else
                    {
                        orderByElements.Add(orderBy);
                    }

                    if (order.Equals("asc"))
                    {
                        foreach (string curr in orderByElements)
                        {
                            ResultOrder ro = new ResultOrder(db.SanitizeString(curr), OrderDirection.Ascending);
                            resultOrderList.Add(ro);
                        }
                    }
                    else if (order.Equals("desc"))
                    {
                        foreach (string curr in orderByElements)
                        {
                            ResultOrder ro = new ResultOrder(db.SanitizeString(curr), OrderDirection.Descending); 
                            resultOrderList.Add(ro);
                        }
                    }
                    else
                    {
                        _Logging.Warn("PutTable invalid order '" + order + "'");
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "Invalid order parameter '" + order + "'"), true));
                        return;
                    }

                    if (resultOrderList.Count > 0) resultOrder = resultOrderList.ToArray();
                }

                DataTable result = db.Select(tableName, indexStart, maxResults, returnFields, e, resultOrder);

                if (result == null || result.Rows.Count < 1)
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send(Common.SerializeJson(new List<dynamic>(), true));
                    return;
                }
                else
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send(Common.SerializeJson(Common.DataTableToListDynamic(result), true));
                    return;
                } 

                #endregion
            }
            else
            {
                #region Update-Object

                if (String.IsNullOrEmpty(currTable.PrimaryKey))
                {
                    _Logging.Warn("PutTable no primary key defined for table " + tableName + " in database " + dbName);
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "No primary key for table " + tableName), true));
                    return;
                }

                byte[] reqData = Common.StreamToBytes(ctx.Request.Data);
                Dictionary<string, object> dict = Common.DeserializeJson<Dictionary<string, object>>(reqData);
                Expression e = new Expression(currTable.PrimaryKey, Operators.Equals, idVal);
                DataTable result = db.Update(tableName, dict, e);

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(Common.DataTableToDynamic(result), true));
                return; 

                #endregion
            }
        }
    }
}
