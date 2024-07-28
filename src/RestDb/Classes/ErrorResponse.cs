namespace RestDb.Classes
{
    using System;

    /// <summary>
    /// Error response.
    /// </summary>
    public class ErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// Flag indicating whether or not the operation was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error code.
        /// </summary>
        public ErrorCodeEnum Code { get; set; } = ErrorCodeEnum.Unknown;

        /// <summary>
        /// Message.
        /// </summary>
        public string Message
        {
            get
            {
                switch (Code)
                {
                    case ErrorCodeEnum.Conflict:
                        return "The requested resource already exists.";
                    case ErrorCodeEnum.DeserializationError:
                        return "The request body could not be deserialized.";
                    case ErrorCodeEnum.InternalError:
                        return "An internal error was encountered.";
                    case ErrorCodeEnum.InvalidRequest:
                        return "Your request was invalid.";
                    case ErrorCodeEnum.LoginFailed:
                        return "Login failed.";
                    case ErrorCodeEnum.MissingField:
                        return "A required field was missing.";
                    case ErrorCodeEnum.MissingQuery:
                        return "No query was supplied.";
                    case ErrorCodeEnum.MissingRequestBody:
                        return "No request body was supplied.";
                    case ErrorCodeEnum.NotFound:
                        return "The requested resource was not found.";
                    case ErrorCodeEnum.RequestTooLarge:
                        return "The supplied request body was too large.";
                    case ErrorCodeEnum.Unknown:
                    default:
                        return "An unknown error was encountered.";
                }
            }
        }

        /// <summary>
        /// Detail.
        /// </summary>
        public string Detail { get; set; } = null;

        /// <summary>
        /// Exception data.
        /// </summary>
        public Exception Exception { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ErrorResponse()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="detail">Detail.</param>
        public ErrorResponse(ErrorCodeEnum code, string detail = null)
        {
            Success = false;
            Code = code;
            Detail = detail;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion 
    }
}
