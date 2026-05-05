namespace RestDb.Storage
{
    /// <summary>
    /// Insert execution plan.
    /// </summary>
    internal class InsertPlan
    {
        /// <summary>
        /// Query batch.
        /// </summary>
        public SqlBatchDefinition Batch { get; set; } = new SqlBatchDefinition();

        /// <summary>
        /// Whether the inserted row can be read back from the batch result.
        /// </summary>
        public bool ReturnsInsertedRow { get; set; } = false;
    }
}
