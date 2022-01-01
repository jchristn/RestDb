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
        static async Task GetDatabases(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(SerializationHelper.SerializeJson(_Settings.GetDatabaseNames(), true));
            return;
        }
    }
}
