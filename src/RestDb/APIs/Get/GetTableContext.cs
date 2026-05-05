namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task GetTableContext(RequestMetadata md)
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

            TableContextPayload payload = await BuildTableContextPayloadAsync(databaseName, table.Name).ConfigureAwait(false);
            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(payload, true)).ConfigureAwait(false);
        }
    }
}
