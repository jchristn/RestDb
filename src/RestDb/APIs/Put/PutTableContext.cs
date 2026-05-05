namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task PutTableContext(RequestMetadata md)
        {
            string databaseName = md.Http.Request.Url.Elements[1];
            string tableName = md.Http.Request.Url.Elements[2];
            Table table = await _Databases.GetTableByNameAsync(databaseName, tableName).ConfigureAwait(false);
            if (table == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true)).ConfigureAwait(false);
                return;
            }

            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody), true)).ConfigureAwait(false);
                return;
            }

            TableContextPayload payload = SerializationHelper.DeserializeJson<TableContextPayload>(md.Http.Request.DataAsString);
            RuntimeConfigurationResult result = UpdateTableContext(databaseName, table.Name, payload.Context);
            ApplyOperationHeaders(md.Http, result);

            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(new
            {
                Result = result,
                Context = await BuildTableContextPayloadAsync(databaseName, table.Name).ConfigureAwait(false)
            }, true)).ConfigureAwait(false);
        }
    }
}
