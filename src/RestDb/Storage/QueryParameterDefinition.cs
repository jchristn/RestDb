namespace RestDb.Storage
{
    /// <summary>
    /// Query parameter definition.
    /// </summary>
    internal class QueryParameterDefinition
    {
        /// <summary>
        /// Parameter name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Parameter value.
        /// </summary>
        public object Value { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QueryParameterDefinition()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="value">Value.</param>
        public QueryParameterDefinition(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}
