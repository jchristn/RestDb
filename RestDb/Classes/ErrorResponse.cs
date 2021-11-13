using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class ErrorResponse
    {
        #region Public-Members

        public bool Success { get; set; } = true;
        public string Message { get; set; } = null;
        public string Detail { get; set; } = null;
        public Exception Exception { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        public ErrorResponse()
        {

        }

        public ErrorResponse(string msg, string detail, Exception e = null)
        {
            Success = false;
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
