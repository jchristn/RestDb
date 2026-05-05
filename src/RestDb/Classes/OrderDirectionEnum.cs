namespace RestDb
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Sort direction.
    /// </summary>
    public enum OrderDirectionEnum
    {
        /// <summary>
        /// Ascending.
        /// </summary>
        [EnumMember(Value = "Ascending")]
        Ascending,
        /// <summary>
        /// Descending.
        /// </summary>
        [EnumMember(Value = "Descending")]
        Descending
    }
}
