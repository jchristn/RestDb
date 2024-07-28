namespace RestDb
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using RestDb.Classes;
    using DatabaseWrapper;
    using DatabaseWrapper.Core;

    partial class RestDbServer
    {
        static async Task PostTableCreate(RequestMetadata md)
        {
            string dbName = md.Http.Request.Url.Elements[0];
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
                _Logging.Warn("PostTableCreate no request body supplied");
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody), true));
                return;
            }

            Table table = SerializationHelper.DeserializeJson<Table>(md.Http.Request.DataAsString);

            if (!String.IsNullOrEmpty(table.PrimaryKey))
            {
                if (table.Columns.Any(c => c.Name.Equals(table.PrimaryKey)))
                {
                    Column primaryKey = table.Columns.First(c => c.Name.Equals(table.PrimaryKey));
                    table.Columns.Remove(primaryKey);
                    primaryKey.PrimaryKey = true;
                    table.Columns.Add(primaryKey);
                }
                else
                {
                    _Logging.Warn("PostTableCreate requested primary key property does not exist in column list");
                    md.Http.Response.StatusCode = 400;
                    md.Http.Response.ContentType = Constants.JsonContentType;
                    await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingField, "Specified primary key column does not exist in column list."), true));
                    return;
                }
            }

            if (table.Columns.Count(c => c.PrimaryKey) > 1)
            {
                _Logging.Warn("PostTableCreate multiple primary key properties found in request");
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.Conflict, "Multiple primary key columns found in request."), true));
                return;
            }

            db.CreateTable(table.Name, table.Columns);

            _Logging.Info("PostTableCreate created table " + table.Name + " in database " + dbName);

            md.Http.Response.StatusCode = 201;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send();
            return;
        }
    }
}
