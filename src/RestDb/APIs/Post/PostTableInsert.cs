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
        static async Task PostTableInsert(RequestMetadata md)
        {
            string dbName = md.Http.Request.Url.Elements[0];
            string tableName = md.Http.Request.Url.Elements[1]; 

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound, "The requested object was not found", null), true));
                return;
            }

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
                _Logging.Warn("PostTableInsert no request body supplied");
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody, "Invalid request", "No request body supplied"), true));
                return;
            }

            DataTable result = null;

            if (!md.Params.Multiple)
            {
                Dictionary<string, object> dict = SerializationHelper.DeserializeJson<Dictionary<string, object>>(md.Http.Request.DataAsBytes);
                result = db.Insert(tableName, dict);
            }
            else
            {
                List<Dictionary<string, object>> dicts = SerializationHelper.DeserializeJson<List<Dictionary<string, object>>>(md.Http.Request.DataAsBytes);
                db.InsertMultiple(tableName, dicts);
            }

            md.Http.Response.StatusCode = 201;
            md.Http.Response.ContentType = "application/json";

            if (result != null)
            {
                await md.Http.Response.Send(SerializationHelper.SerializeJson(Common.DataTableToDynamic(result), true));
            }
            else
            {
                await md.Http.Response.Send();
            }
            return;
        }
    }
}
