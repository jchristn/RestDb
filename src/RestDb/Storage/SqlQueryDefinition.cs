namespace RestDb.Storage
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// SQL query definition.
    /// </summary>
    internal class SqlQueryDefinition
    {
        /// <summary>
        /// Command text.
        /// </summary>
        public string CommandText { get; set; } = null;

        /// <summary>
        /// Parameters.
        /// </summary>
        public List<QueryParameterDefinition> Parameters { get; } = new List<QueryParameterDefinition>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SqlQueryDefinition()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="commandText">Command text.</param>
        public SqlQueryDefinition(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText)) throw new ArgumentNullException(nameof(commandText));
            CommandText = commandText;
        }

        /// <summary>
        /// Add a parameter and return its placeholder name.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Parameter placeholder.</returns>
        public string AddParameter(object value)
        {
            string name = "@p" + Parameters.Count;
            Parameters.Add(new QueryParameterDefinition(name, value));
            return name;
        }
    }
}
