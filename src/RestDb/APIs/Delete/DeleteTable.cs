using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestDb.Classes;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;
using DatabaseWrapper.Core;
using ExpressionTree;

namespace RestDb 
{
    partial class RestDbServer
    {
        static async Task DeleteTable(RequestMetadata md)
        {
            string dbName = md.Http.Request.Url.Elements[0];
            string tableName = md.Http.Request.Url.Elements[1];
            int idVal = 0;
            if (md.Http.Request.Url.Elements.Length == 3) Int32.TryParse(md.Http.Request.Url.Elements[2], out idVal);
            
            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound, "The requested object was not found", null), true));
                return;
            }

            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                md.Http.Response.StatusCode = 404;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotFound, "The requested object was not found", null), true));
                return;
            }

            if (md.Params.Truncate)
            {
                #region Truncate

                db.Truncate(tableName);
                _Logging.Warn("DeleteTable truncated table " + tableName + " in database " + dbName);
                md.Http.Response.StatusCode = 204;
                await md.Http.Response.Send();
                return;

                #endregion
            }
            else if (md.Params.Drop)
            {
                #region Drop

                db.DropTable(tableName);
                _Logging.Warn("DeleteTable dropped table " + tableName + " in database " + dbName);
                md.Http.Response.StatusCode = 204;
                await md.Http.Response.Send();
                return;

                #endregion
            }
            else if (md.Http.Request.Url.Elements.Length >= 2)
            {
                #region Delete-Objects
                 
                Expr filter = null; 

                if (idVal > 0)
                {
                    if (String.IsNullOrEmpty(currTable.PrimaryKey))
                    {
                        _Logging.Warn("DeleteTable no primary key defined for table " + tableName + " in database " + dbName);
                        md.Http.Response.StatusCode = 400;
                        md.Http.Response.ContentType = "application/json";
                        await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Invalid request", "No primary key for table " + tableName), true));
                        return;
                    }

                    filter = new Expr(currTable.PrimaryKey, OperatorEnum.Equals, idVal);
                }

                if (md.Http.Request.Query.Elements != null && md.Http.Request.Query.Elements.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in md.Http.Request.Query.Elements)
                    {
                        if (Constants.QueryKeys.Contains(currKvp.Key)) continue;
                        if (filter == null) filter = new Expr(currKvp.Key, OperatorEnum.Equals, currKvp.Value);
                        else filter.PrependAnd(currKvp.Key, OperatorEnum.Equals, currKvp.Value);
                    }
                }

                db.Delete(tableName, filter);
                md.Http.Response.StatusCode = 204;
                await md.Http.Response.Send();
                return;

                #endregion
            }
        }
    }
}
