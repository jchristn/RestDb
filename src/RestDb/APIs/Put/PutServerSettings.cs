namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task PutServerSettings(RequestMetadata md)
        {
            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody), true)).ConfigureAwait(false);
                return;
            }

            Settings settings = SerializationHelper.DeserializeJson<Settings>(md.Http.Request.DataAsString);
            RuntimeConfigurationResult result = UpdateSettings(settings);
            ApplyOperationHeaders(md.Http, result);

            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(new
            {
                Result = result,
                Settings = GetSettingsSnapshot()
            }, true)).ConfigureAwait(false);
        }
    }
}
