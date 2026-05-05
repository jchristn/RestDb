namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task PutDatabaseContext(RequestMetadata md)
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

            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody), true)).ConfigureAwait(false);
                return;
            }

            DatabaseContextPayload payload = SerializationHelper.DeserializeJson<DatabaseContextPayload>(md.Http.Request.DataAsString);
            RuntimeConfigurationResult result = UpdateDatabaseContext(database.Name, payload);
            ApplyOperationHeaders(md.Http, result);

            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(new
            {
                Result = result,
                Context = await BuildDatabaseContextPayloadAsync(database.Name).ConfigureAwait(false)
            }, true)).ConfigureAwait(false);
        }
    }
}
