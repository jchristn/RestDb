namespace RestDb
{
    using System.Collections.Generic;
    using DatabaseWrapper.Core;

    /// <summary>
    /// Database table.
    /// </summary>
    public class Table
    {
        #region Public-Members

        /// <summary>
        /// Table name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Name of the primary key column.
        /// </summary>
        public string PrimaryKey { get; set; } = null;

        /// <summary>
        /// Table columns.
        /// </summary>
        public List<Column> Columns { get; set; } = new List<Column>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
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
