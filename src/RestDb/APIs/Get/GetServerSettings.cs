namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task GetServerSettings(RequestMetadata md)
        {
            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(GetSettingsSnapshot(), true)).ConfigureAwait(false);
        }
    }
}
