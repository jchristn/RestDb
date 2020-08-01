using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseWrapper.Core;

namespace RestDb
{
    public class Table
    {
        #region Constructors-and-Factories

        public Table()
        {

        }

        #endregion

        #region Public-Members

        public string Name { get; set; }
        public string PrimaryKey { get; set; }
        public List<Column> Columns { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
