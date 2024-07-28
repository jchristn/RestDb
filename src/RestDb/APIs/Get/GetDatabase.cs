namespace RestDb
{
    using System;
    using System.Threading.Tasks;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task GetDatabase(RequestMetadata md)
        {
            string dbName = md.Http.Request.Url.Elements[0];
            Database db = _Settings.GetDatabaseByName(dbName);
            if (db == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true));
                return;
            }

            db = Database.Redact(db);

            string describe = md.Http.Request.RetrieveHeaderValue("_describe");
            if (!String.IsNullOrEmpty(describe) && describe.Equals("true"))
            {
                db.Tables = _Databases.GetTables(dbName, true);
            }
            else
            {
                db.TableNames = _Databases.GetTableNames(dbName);
            }

            md.Http.Response.StatusCode = 200;
            md.Http.Response.ContentType = Constants.JsonContentType;
            await md.Http.Response.Send(SerializationHelper.SerializeJson(db, true));
            return;
        }
    }
}
