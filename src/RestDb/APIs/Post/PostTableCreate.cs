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
        static async Task PostTableCreate(RequestMetadata md)
        {
            string dbName = md.Http.Request.Url.Elements[0];
            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound, "The requested object was not found", null), true));
                return;
            }

            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                _Logging.Warn("PostTableCreate no request body supplied");
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody, "Invalid request", "No request body supplied"), true));
                return;
            }

            Table table = SerializationHelper.DeserializeJson<Table>(md.Http.Request.DataAsString);
            db.CreateTable(table.Name, table.Columns);

            _Logging.Info("PostTableCreate created table " + table.Name + " in database " + dbName);

            md.Http.Response.StatusCode = 201;
            md.Http.Response.ContentType = "application/json";
            await md.Http.Response.Send();
            return;
        }
    }
}
