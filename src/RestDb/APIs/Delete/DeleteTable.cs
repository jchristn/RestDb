namespace RestDb
{
    using System;
    using System.Threading.Tasks;
    using ExpressionTree;
    using RestDb.Classes;

    partial class RestDbServer
    {
        static async Task DeleteTable(RequestMetadata md)
        {
            string dbName = md.Http.Request.Url.Elements[0];
            string tableName = md.Http.Request.Url.Elements[1];
            int idVal = 0;
            if (md.Http.Request.Url.Elements.Length == 3) Int32.TryParse(md.Http.Request.Url.Elements[2], out idVal);

            Table currTable = await _Databases.GetTableByNameAsync(dbName, tableName);
            if (currTable == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true));
                return;
            }

            var db = _Databases.GetDatabaseDriver(dbName);
            if (db == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = Constants.JsonContentType;
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound), true));
                return;
            }

            if (md.Params.Truncate)
            {
                await db.Schema.ClearTableAsync(currTable.Name);
                _Logging.Warn("DeleteTable truncated table " + tableName + " in database " + dbName);
                md.Http.Response.StatusCode = 204;
                await md.Http.Response.Send();
                return;
            }

            if (md.Params.Drop)
            {
                await db.Schema.DropTableAsync(currTable.Name);
                _Logging.Warn("DeleteTable dropped table " + tableName + " in database " + dbName);
                md.Http.Response.StatusCode = 204;
                await md.Http.Response.Send();
                return;
            }

            if (md.Http.Request.Url.Elements.Length >= 2)
            {
                Expr filter = null;

                if (idVal > 0)
                {
                    if (string.IsNullOrEmpty(currTable.PrimaryKey))
                    {
                        _Logging.Warn("DeleteTable no primary key defined for table " + tableName + " in database " + dbName);
                        md.Http.Response.StatusCode = 400;
                        md.Http.Response.ContentType = Constants.JsonContentType;
                        await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "No primary key for table " + tableName + "."), true));
                        return;
                    }

                    filter = new Expr(currTable.PrimaryKey, OperatorEnum.Equals, idVal);
                }

                if (md.Http.Request.Query.Elements != null && md.Http.Request.Query.Elements.Count > 0)
                {
                    for (int i = 0; i < md.Http.Request.Query.Elements.Count; i++)
                    {
                        string currKey = md.Http.Request.Query.Elements.GetKey(i);
                        string currVal = md.Http.Request.Query.Elements.Get(i);
                        if (string.IsNullOrEmpty(currKey)) continue;
                        if (Constants.QueryKeys.Contains(currKey)) continue;

                        if (filter == null) filter = new Expr(currKey, OperatorEnum.Equals, currVal);
                        else filter = Expr.PrependAndClause(new Expr(currKey, OperatorEnum.Equals, currVal), filter);
                    }
                }

                await db.Records.DeleteAsync(currTable.Name, currTable.Columns, filter);
                md.Http.Response.StatusCode = 204;
                await md.Http.Response.Send();
            }
        }
    }
}
