namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task PostContextReload(RequestMetadata md)
        {
            RuntimeConfigurationResult result = ReloadContextFromDisk();
            ApplyOperationHeaders(md.Http, result);

            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(new
            {
                Result = result,
                Context = GetContextDocumentSnapshot()
            }, true)).ConfigureAwait(false);
        }
    }
}
