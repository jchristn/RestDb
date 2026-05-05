namespace RestDb
{
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task PutContextFile(RequestMetadata md)
        {
            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody), true)).ConfigureAwait(false);
                return;
            }

            ContextDocument contextDocument = SerializationHelper.DeserializeJson<ContextDocument>(md.Http.Request.DataAsString);
            RuntimeConfigurationResult result = UpdateContextDocument(contextDocument);
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
