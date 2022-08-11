using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace RestDb.Classes
{
    /// <summary>
    /// Order direction.
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
