using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class ApiKey
    {
        #region Constructors-and-Factories

        public ApiKey()
        {

        }

        public static ApiKey Default()
        {
            ApiKey ret = new ApiKey();
            ret.Key = "default";
            ret.AllowGet = true;
            ret.AllowPost = true;
            ret.AllowPut = true;
            ret.AllowDelete = true;
            return ret;
        }

        #endregion

        #region Public-Members

        public string Key { get; set; }
        public bool AllowGet { get; set; }
        public bool AllowPost { get; set; }
        public bool AllowPut { get; set; }
        public bool AllowDelete { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
