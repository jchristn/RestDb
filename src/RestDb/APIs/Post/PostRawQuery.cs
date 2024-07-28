namespace RestDb
{
    using System.Data;
    using System.Threading.Tasks;
    using RestDb.Classes;
    using DatabaseWrapper;

    partial class RestDbServer
    {
        static async Task PostRawQuery(RequestMetadata md)
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

            DataTable result = db.Query(md.Http.Request.DataAsString);

            if (result != null && result.Rows.Count > 0)
            {
                md.Http.Response.StatusCode = 200;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(Common.DataTableToListDynamic(result), true));
                return;
            }
            else
            {
                md.Http.Response.StatusCode = 200;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send();
                return;
            }
        }
    }
}
