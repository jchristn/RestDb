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
        static async Task PutTable(RequestMetadata md)
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

            if (md.Http.Request.Data == null || md.Http.Request.ContentLength < 1)
            {
                _Logging.Warn("PutTable no request body supplied");
                md.Http.Response.StatusCode = 400;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.MissingRequestBody, "Invalid request", "No request body supplied"), true));
                return;
            }

            if (idVal == 0 
                && md.Http.Request.Url.Elements.Length == 2)
            {
                #region Search-Table

                Expr filter = SerializationHelper.DeserializeJsonExpression(md.Http.Request.DataAsBytes);

                ResultOrder[] resultOrder = new ResultOrder[1];
                resultOrder[0] = new ResultOrder(currTable.PrimaryKey, OrderDirection.Ascending);

                if (md.Http.Request.Query.Elements != null && md.Http.Request.Query.Elements.Count > 0)
                {
                    foreach (KeyValuePair<string, string> currKvp in md.Http.Request.Query.Elements)
                    {
                        if (Constants.QueryKeys.Contains(currKvp.Key)) continue;
                        filter = Expr.PrependAndClause(
                            new Expr(currKvp.Key, OperatorEnum.Equals, currKvp.Value),
                            filter);
                    }
                }

                if (md.Params.OrderBy != null && md.Params.OrderBy.Count > 0)
                {
                    List<ResultOrder> resultOrderList = new List<ResultOrder>();

                    foreach (string curr in md.Params.OrderBy)
                    {
                        if (md.Params.OrderDirection == OrderDirectionEnum.Descending)
                        {
                            ResultOrder ro = new ResultOrder(curr, OrderDirection.Descending);
                        }
                        else if (md.Params.OrderDirection == OrderDirectionEnum.Ascending)
                        {
                            ResultOrder ro = new ResultOrder(curr, OrderDirection.Ascending);
                        }
                    }

                    if (resultOrderList.Count > 0)
                    {
                        resultOrder = new ResultOrder[resultOrderList.Count];
                        for (int i = 0; i < resultOrderList.Count; i++)
                        {
                            resultOrder[i] = resultOrderList[i];
                        }
                    }
                }

                DataTable result = db.Select(
                    tableName,
                    md.Params.IndexStart, 
                    md.Params.MaxResults, 
                    md.Params.ReturnFields, 
                    filter, 
                    resultOrder);

                if (md.Params.Debug && filter != null)
                {
                    md.Http.Response.Headers.Add(Constants.HeaderExpression, filter.ToString());
                }

                if (result == null || result.Rows.Count < 1)
                {
                    md.Http.Response.StatusCode = 200;
                    md.Http.Response.ContentType = "application/json";
                    await md.Http.Response.Send(SerializationHelper.SerializeJson(new List<dynamic>(), true));
                    return;
                }
                else
                {
                    md.Http.Response.StatusCode = 200;
                    md.Http.Response.ContentType = "application/json";
                    await md.Http.Response.Send(SerializationHelper.SerializeJson(Common.DataTableToListDynamic(result), true));
                    return;
                } 

                #endregion
            }
            else
            {
                #region Update-Object

                if (String.IsNullOrEmpty(currTable.PrimaryKey))
                {
                    _Logging.Warn("PutTable no primary key defined for table " + tableName + " in database " + dbName);
                    md.Http.Response.StatusCode = 400;
                    md.Http.Response.ContentType = "application/json";
                    await md.Http.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Invalid request", "No primary key for table " + tableName), true));
                    return;
                }

                byte[] reqData = md.Http.Request.DataAsBytes;
                Dictionary<string, object> dict = SerializationHelper.DeserializeJson<Dictionary<string, object>>(reqData);
                Expr e = new Expr(currTable.PrimaryKey, OperatorEnum.Equals, idVal);
                DataTable result = db.Update(tableName, dict, e);

                md.Http.Response.StatusCode = 200;
                md.Http.Response.ContentType = "application/json";
                await md.Http.Response.Send(SerializationHelper.SerializeJson(Common.DataTableToDynamic(result), true));
                return; 

                #endregion
            }
        }
    }
}
