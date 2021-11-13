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
        #region Public-Members

        public string Name { get; set; } = null;
        public string PrimaryKey { get; set; } = null;
        public List<Column> Columns { get; set; } = new List<Column>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        public Table()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
