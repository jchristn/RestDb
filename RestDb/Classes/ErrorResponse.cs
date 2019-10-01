using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class ErrorResponse
    {
        #region Constructors-and-Factories

        public ErrorResponse()
        {

        }

        public ErrorResponse(string msg, string detail)
        {
            Success = false;
            Message = msg;
            Detail = detail;
        }

        #endregion

        #region Public-Members

        public bool Success { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
