using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb.Classes
{
    /// <summary>
    /// Error response.
    /// </summary>
    public class ErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// Flag indicating whether or not the operation was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error code.
        /// </summary>
        public ErrorCodeEnum Code { get; set; } = ErrorCodeEnum.Unknown;

        /// <summary>
        /// Message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Detail.
        /// </summary>
        public string Detail { get; set; } = null;

        /// <summary>
        /// Exception data.
        /// </summary>
        public Exception Exception { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ErrorResponse()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="msg">Message.</param>
        /// <param name="detail">Detail.</param>
        /// <param name="e">Exception.</param>
        public ErrorResponse(ErrorCodeEnum code, string msg, string detail, Exception e = null)
        {
            Success = false;
            Code = code;
            Message = msg;
            Detail = detail;
            Exception = e;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
