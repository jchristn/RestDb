using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestDb.Classes;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;
using DatabaseWrapper.Core;

namespace RestDb
{
    partial class RestDbServer
    {
        static async Task PostTableCreate(HttpContext ctx)
        {
            string dbName = ctx.Request.Url.Elements[0];
            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }

            if (ctx.Request.Data == null || ctx.Request.ContentLength < 1)
            {
                _Logging.Warn("PostTableCreate no request body supplied");
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Bad request", "No request body supplied"), true));
                return;
            }

            Table table = SerializationHelper.DeserializeJson<Table>(Common.StreamToBytes(ctx.Request.Data));
            db.CreateTable(table.Name, table.Columns);
             
            ctx.Response.StatusCode = 201;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send();
            return;
        }
    }
}
