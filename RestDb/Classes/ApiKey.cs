using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    /// <summary>
    /// API key.
    /// </summary>
    public class ApiKey
    {
        #region Public-Members

        /// <summary>
        /// Key.
        /// </summary>
        public string Key { get; set; } = "default";

        /// <summary>
        /// Allow GET operations.
        /// </summary>
        public bool AllowGet { get; set; } = true;

        /// <summary>
        /// Allow POST operations.
        /// </summary>
        public bool AllowPost { get; set; } = true;

        /// <summary>
        /// Allow PUT operations.
        /// </summary>
        public bool AllowPut { get; set; } = true;

        /// <summary>
        /// Allow DELETE operations.
        /// </summary>
        public bool AllowDelete { get; set; } = true;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
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
