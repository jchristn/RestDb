namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task GetDatabaseContext(RequestMetadata md)
        {
            string databaseName = md.Http.Request.Url.Elements[1];
            Database database = _Settings.GetDatabaseByName(databaseName);
            if (database == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true)).ConfigureAwait(false);
                return;
            }

            DatabaseContextPayload payload = await BuildDatabaseContextPayloadAsync(database.Name).ConfigureAwait(false);
            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(payload, true)).ConfigureAwait(false);
        }
    }
}
