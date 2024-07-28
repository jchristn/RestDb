namespace RestDb.Classes
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Error code enumeration.
    /// </summary>
    public enum ErrorCodeEnum
    {
        /// <summary>
        /// Already exists.
        /// </summary>
        [EnumMember(Value = "AlreadyExists")]
        AlreadyExists,
        /// <summary>
        /// Conflict.
        /// </summary>
        [EnumMember(Value = "Conflict")]
        Conflict,
        /// <summary>
        /// Deserialization error.
        /// </summary>
        [EnumMember(Value = "DeserializationError")]
        DeserializationError,
        /// <summary>
        /// Internal error.
        /// </summary>
        [EnumMember(Value = "InternalError")]
        InternalError,
        /// <summary>
        /// Invalid request.
        /// </summary>
        [EnumMember(Value = "InvalidRequest")]
        InvalidRequest,
        /// <summary>
        /// Login failed.
        /// </summary>
        [EnumMember(Value = "LoginFailed")]
        LoginFailed,
        /// <summary>
        /// Missing field.
        /// </summary>
        [EnumMember(Value = "MissingField")]
        MissingField,
        /// <summary>
        /// Missing query.
        /// </summary>
        [EnumMember(Value = "MissingQuery")]
        MissingQuery,
        /// <summary>
        /// Missing request body.
        /// </summary>
        [EnumMember(Value = "MissingRequestBody")]
        MissingRequestBody,
        /// <summary>
        /// Not found.
        /// </summary>
        [EnumMember(Value = "NotFound")]
        NotFound,
        /// <summary>
        /// Request too large.
        /// </summary>
        [EnumMember(Value = "RequestTooLarge")]
        RequestTooLarge,
        /// <summary>
        /// Too many objects.
        /// </summary>
        [EnumMember(Value = "TooManyObjects")]
        TooManyObjects,
        /// <summary>
        /// Unknown
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown,
    }
}
