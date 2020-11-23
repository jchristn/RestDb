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
        static async Task DeleteTable(HttpContext ctx)
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

            if (ctx.Request.Url.Elements.Length == 2 && ctx.Request.Query.Elements.ContainsKey("_truncate"))
            {
                #region Truncate

                db.Truncate(tableName);
                _Logging.Warn("DeleteTable truncated table " + tableName + " in database " + dbName);
                ctx.Response.StatusCode = 204;
                await ctx.Response.Send();
                return;

                #endregion
            }
            else if (ctx.Request.Url.Elements.Length == 2 && ctx.Request.Query.Elements.ContainsKey("_drop"))
            {
                #region Drop

                db.DropTable(tableName);
                _Logging.Warn("DeleteTable dropped table " + tableName + " in database " + dbName);
                ctx.Response.StatusCode = 204;
                await ctx.Response.Send();
                return;

                #endregion
            }
            else if (ctx.Request.Url.Elements.Length >= 2)
            {
                #region Delete-Objects
                 
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

                if (ctx.Request.Query.Elements != null && ctx.Request.Query.Elements.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in ctx.Request.Query.Elements)
                    {
                        if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                        if (filter == null) filter = new Expression(currKvp.Key, Operators.Equals, currKvp.Value);
                        else filter.PrependAnd(currKvp.Key, Operators.Equals, currKvp.Value);
                    }
                }

                db.Delete(tableName, filter);
                ctx.Response.StatusCode = 204;
                await ctx.Response.Send();
                return;

                #endregion
            }
        }
    }
}
