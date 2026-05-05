namespace RestDb
{
    using System;

    /// <summary>
    /// Table column definition.
    /// </summary>
    public class Column
    {
        /// <summary>
        /// Column name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Column type.
        /// </summary>
        public string Type { get; set; } = null;

        /// <summary>
        /// Whether the column allows null values.
        /// </summary>
        public bool Nullable { get; set; } = true;

        /// <summary>
        /// Maximum character length when relevant.
        /// </summary>
        public int? MaxLength { get; set; } = null;

        /// <summary>
        /// Whether the column is a primary key.
        /// </summary>
        public bool PrimaryKey { get; set; } = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Column()
        {

        }

        /// <summary>
        /// Create a shallow copy.
        /// </summary>
        /// <returns>Column.</returns>
        public Column Copy()
        {
            return new Column
            {
                Name = Name,
                Type = Type,
                Nullable = Nullable,
                MaxLength = MaxLength,
                PrimaryKey = PrimaryKey
            };
        }
    }
}
