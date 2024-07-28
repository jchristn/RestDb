namespace RestDb
{
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;
    using RestDb.Classes;
    using DatabaseWrapper;

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
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true));
                return;
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true));
                return;
            }

            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                _Logging.Warn("PostTableInsert no request body supplied");
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody), true));
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
            md.Http.Response.ContentType = Constants.JsonContentType;

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
