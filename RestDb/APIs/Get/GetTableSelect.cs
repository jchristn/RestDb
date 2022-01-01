using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using RestDb.Classes;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;
using DatabaseWrapper.Core;
using ExpressionTree;

namespace RestDb
{
    partial class RestDbServer
    {
        static async Task GetTableSelect(HttpContext ctx)
        {
            string dbName = ctx.Request.Url.Elements[0];
            string tableName = ctx.Request.Url.Elements[1];
            int idVal = 0;
            if (ctx.Request.Url.Elements.Length == 3) Int32.TryParse(ctx.Request.Url.Elements[2], out idVal);

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }
             
            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }

            #region Check-for-Describe

            if (ctx.Request.Query.Elements.ContainsKey("_describe"))
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(currTable, true));
                return;
            }

            #endregion
            
            #region Retrieve-Objects

            DataTable result = null;

            int? indexStart = null;
            int? maxResults = null; 
            List<string> returnFields = null;
            Expr filter = null;
            string order = null;
            string orderBy = null;
            List<ResultOrder> resultOrderList = new List<ResultOrder>();
            ResultOrder[] resultOrder = new ResultOrder[1];
            resultOrder[0] = new ResultOrder(currTable.PrimaryKey, OrderDirection.Ascending);

            if (idVal > 0)
            {
                if (String.IsNullOrEmpty(currTable.PrimaryKey))
                {
                    _Logging.Warn("GetTable no primary key defined for table " + tableName + " in database " + dbName);
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Bad request", "No primary key for table " + tableName), true));
                    return;
                }

                filter = new Expr(currTable.PrimaryKey, OperatorEnum.Equals, idVal);
            }

            if (ctx.Request.Query.Elements != null && ctx.Request.Query.Elements.Count > 0)
            {
                foreach (KeyValuePair<string, string> currKvp in ctx.Request.Query.Elements)
                {
                    if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                    if (filter == null) filter = new Expr(currKvp.Key, OperatorEnum.Equals, currKvp.Value);
                    else filter = Expr.PrependAndClause(
                        new Expr(currKvp.Key, OperatorEnum.Equals, currKvp.Value),
                        filter);
                }
            }

            if (ctx.Request.Query.Elements.ContainsKey("_index")) indexStart = Convert.ToInt32(ctx.Request.Query.Elements["_index"]);
            if (ctx.Request.Query.Elements.ContainsKey("_max")) maxResults = Convert.ToInt32(ctx.Request.Query.Elements["_max"]); 
            if (ctx.Request.Query.Elements.ContainsKey("_return_fields")) returnFields = Common.CsvToStringList(ctx.Request.Query.Elements["_return_fields"]);
            if (ctx.Request.Query.Elements.ContainsKey("_order")) order = ctx.Request.Query.Elements["_order"];
            if (ctx.Request.Query.Elements.ContainsKey("_order_by")) orderBy = ctx.Request.Query.Elements["_order_by"];

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
                    await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Bad request", "Invalid order parameter '" + order + "'"), true));
                    return;
                }

                if (resultOrderList.Count > 0) resultOrder = resultOrderList.ToArray();
            }

            result = db.Select(tableName, indexStart, maxResults, returnFields, filter, resultOrder);

            if (result == null || result.Rows.Count < 1)
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new List<dynamic>(), true));
                return;
            }
            else
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(Common.DataTableToListDynamic(result), true));
                return;
            }

            #endregion 
        }
    }
}
