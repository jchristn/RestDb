using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class ApiKey
    {
        #region Public-Members

        public string Key { get; set; } = "default";
        public bool AllowGet { get; set; } = true;
        public bool AllowPost { get; set; } = true;
        public bool AllowPut { get; set; } = true;
        public bool AllowDelete { get; set; } = true;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        public ApiKey()
        {

        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
