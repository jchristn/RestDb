using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    public class Column
    {
        #region Constructors-and-Factories

        public Column()
        {

        }

        #endregion

        #region Public-Members

        public string Name { get; set; }
        public string Type { get; set; } 
        public int? MaxLength { get; set; }
        public bool Nullable { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion

        #region Public-Static-Methods

        #endregion

        #region Private-Static-Methods

        #endregion
    }
}
