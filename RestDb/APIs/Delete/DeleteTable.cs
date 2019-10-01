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
        static async Task DeleteTable(HttpContext ctx)
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

            if (idVal == 0 
                && ctx.Request.RawUrlEntries.Count == 2)
            {
                #region Retrieve-Table

                if (!ctx.Request.QuerystringEntries.ContainsKey("_truncate"))
                {
                    _Logging.Warn("DeleteTable table deletion not allowed without setting _truncate in querystring for " + tableName + " in database " + dbName);
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "Cannot truncate table without setting _truncate in querystring to true"), true));
                    return;
                }

                db.Truncate(tableName);
                _Logging.Warn("DeleteTable truncated table " + tableName + " in database " + dbName);
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;

                #endregion
            }
            else
            {
                #region Delete-Objects

                DataTable result = null;
                Expression filter = null; 

                if (idVal > 0)
                {
                    if (String.IsNullOrEmpty(currTable.PrimaryKey))
                    {
                        _Logging.Warn("DeleteTable no primary key defined for table " + tableName + " in database " + dbName);
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "No primary key for table " + tableName), true));
                        return;
                    }

                    filter = new Expression(currTable.PrimaryKey, Operators.Equals, idVal);
                }

                if (ctx.Request.QuerystringEntries != null && ctx.Request.QuerystringEntries.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in ctx.Request.QuerystringEntries)
                    {
                        if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                        if (filter == null) filter = new Expression(currKvp.Key, Operators.Equals, currKvp.Value);
                        else filter = Expression.PrependAndClause(
                            new Expression(currKvp.Key, Operators.Equals, currKvp.Value),
                            filter);
                    }
                }

                result = db.Delete(tableName, filter);
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send();
                return;

                #endregion
            }
        }
    }
}
