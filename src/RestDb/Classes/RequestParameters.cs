using System;
using System.Collections.Generic;
using System.Text;
using DatabaseWrapper.Core;
using WatsonWebserver;

namespace RestDb.Classes
{
    /// <summary>
    /// Request parameters.
    /// </summary>
    public class RequestParameters
    {
        #region Public-Members

        /// <summary>
        /// API key header value.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// Describe.
        /// </summary>
        public bool Describe { get; set; } = false;

        /// <summary>
        /// Multiple.
        /// </summary>
        public bool Multiple { get; set; } = false;

        /// <summary>
        /// Index start.
        /// </summary>
        public int? IndexStart
        {
            get
            {
                return _IndexStart;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(IndexStart));
                _IndexStart = value;
            }
        }

        /// <summary>
        /// Max results.
        /// </summary>
        public int? MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxResults));
                _MaxResults = value;
            }
        }

        /// <summary>
        /// Order by.
        /// </summary>
        public List<string> OrderBy { get; set; } = null;

        /// <summary>
        /// Order direction.
        /// </summary>
        public OrderDirectionEnum OrderDirection { get; set; } = OrderDirectionEnum.Ascending;

        /// <summary>
        /// Return fields.
        /// </summary>
        public List<string> ReturnFields { get; set; } = null;

        /// <summary>
        /// Truncate table.
        /// </summary>
        public bool Truncate { get; set; } = false;

        /// <summary>
        /// Drop table.
        /// </summary>
        public bool Drop { get; set; } = false;

        /// <summary>
        /// Debug query.
        /// </summary>
        public bool Debug { get; set; } = false;

        #endregion

        #region Private-Members

        private int? _IndexStart = null;
        private int? _MaxResults = 100;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestParameters(HttpContext ctx)
        {
            if (ctx != null && ctx.Request != null)
            {
                if (ctx.Request.QuerystringExists(Constants.QueryDescribe))
                {
                    Describe = true;
                }
                else if (ctx.Request.QuerystringExists(Constants.QueryMultiple))
                {
                    Multiple = true;
                }
                else if (ctx.Request.QuerystringExists(Constants.QueryIndexStart))
                {
                    if (Int32.TryParse(ctx.Request.Query.Elements[Constants.QueryIndexStart], out int testInt))
                    {
                        IndexStart = testInt;
                    }
                }
                else if (ctx.Request.QuerystringExists(Constants.QueryMaxResults))
                {
                    if (Int32.TryParse(ctx.Request.Query.Elements[Constants.QueryMaxResults], out int testInt))
                    {
                        MaxResults = testInt;
                    }
                }
                else if (ctx.Request.QuerystringExists(Constants.QueryOrderBy))
                {
                    OrderBy = Common.CsvToStringList(ctx.Request.Query.Elements[Constants.QueryOrderBy]);
                }
                else if (ctx.Request.QuerystringExists(Constants.QueryOrderDirection))
                {
                    string orderDirection = ctx.Request.Query.Elements[Constants.QueryOrderDirection];
                    if (!String.IsNullOrEmpty(orderDirection))
                    {
                        if (orderDirection.ToLower().Equals("asc")) OrderDirection = OrderDirectionEnum.Ascending;
                        else if (orderDirection.ToLower().Equals("desc")) OrderDirection = OrderDirectionEnum.Descending;
                    }
                }
                else if (ctx.Request.QuerystringExists(Constants.QueryReturnFields))
                {
                    ReturnFields = Common.CsvToStringList(ctx.Request.Query.Elements[Constants.QueryReturnFields]);
                }
                if (ctx.Request.QuerystringExists(Constants.QueryTruncateTable))
                {
                    Truncate = true;
                }
                if (ctx.Request.QuerystringExists(Constants.QueryDropTable))
                {
                    Drop = true;
                }
                if (ctx.Request.QuerystringExists(Constants.QueryDebug))
                {
                    Debug = true;
                }
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
