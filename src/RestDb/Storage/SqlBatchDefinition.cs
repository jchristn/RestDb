namespace RestDb.Storage
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL batch definition.
    /// </summary>
    internal class SqlBatchDefinition
    {
        /// <summary>
        /// Queries.
        /// </summary>
        public List<SqlQueryDefinition> Queries { get; } = new List<SqlQueryDefinition>();

        /// <summary>
        /// Whether the batch should run in a transaction.
        /// </summary>
        public bool UseTransaction { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SqlBatchDefinition()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="queries">Queries.</param>
        /// <param name="useTransaction">Use transaction.</param>
        public SqlBatchDefinition(IEnumerable<SqlQueryDefinition> queries, bool useTransaction = true)
        {
            if (queries != null) Queries.AddRange(queries);
            UseTransaction = useTransaction;
        }
    }
}
