using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestDb.Classes;
using SyslogLogging;
using WatsonWebserver;

namespace RestDb
{
    partial class RestDbServer
    {
        static async Task GetDatabase(HttpContext ctx)
        {
            string dbName = ctx.Request.Url.Elements[0];
            Database db = _Settings.GetDatabaseByName(dbName);
            if (db == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }

            db = Database.Redact(db);

            string describe = ctx.Request.RetrieveHeaderValue("_describe");
            if (!String.IsNullOrEmpty(describe) && describe.Equals("true"))
            {
                db.Tables = _Databases.GetTables(dbName, true);
            }
            else
            {
                db.TableNames = _Databases.GetTableNames(dbName);
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(SerializationHelper.SerializeJson(db, true));
            return;
        }
    }
}
