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
        static async Task PostTableInsert(HttpContext ctx)
        {
            string dbName = ctx.Request.RawUrlEntries[0];
            string tableName = ctx.Request.RawUrlEntries[1]; 

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
                _Logging.Warn("PostTableInsert no request body supplied");
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "No request body supplied"), true));
                return;
            }

            byte[] reqData = Common.StreamToBytes(ctx.Request.Data);
            Dictionary<string, object> dict = Common.DeserializeJson<Dictionary<string, object>>(reqData);
            DataTable result = db.Insert(tableName, dict);

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(Common.SerializeJson(Common.DataTableToDynamic(result), true));
            return;
        }
    }
}
