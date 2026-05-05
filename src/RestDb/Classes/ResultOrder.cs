namespace RestDb
{
    using System;

    /// <summary>
    /// Ordering definition.
    /// </summary>
    public class ResultOrder
    {
        /// <summary>
        /// Column name.
        /// </summary>
        public string Column { get; set; } = null;

        /// <summary>
        /// Sort direction.
        /// </summary>
        public OrderDirectionEnum Direction { get; set; } = OrderDirectionEnum.Ascending;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ResultOrder()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="column">Column.</param>
        /// <param name="direction">Direction.</param>
        public ResultOrder(string column, OrderDirectionEnum direction)
        {
            if (string.IsNullOrWhiteSpace(column)) throw new ArgumentNullException(nameof(column));
            Column = column;
            Direction = direction;
        }
    }
}
