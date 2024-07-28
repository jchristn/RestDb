namespace RestDb.Classes
{
    using System;
    using WatsonWebserver;

    /// <summary>
    /// Request metadata.
    /// </summary>
    public class RequestMetadata
    {
        #region Public-Members

        /// <summary>
        /// HTTP context.
        /// </summary>
        public HttpContext Http
        {
            get
            {
                return _Http;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Http));
                _Http = value;
            }
        }

        /// <summary>
        /// API key.
        /// </summary>
        public ApiKey ApiKey
        {
            get
            {
                return _ApiKey;
            }
            set
            {
                _ApiKey = value;
            }
        }

        /// <summary>
        /// Request parameters.
        /// </summary>
        public RequestParameters Params
        {
            get
            {
                return _Params;
            }
        }

        #endregion

        #region Private-Members

        private HttpContext _Http = null;
        private ApiKey _ApiKey = null;
        private RequestParameters _Params = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestMetadata(HttpContext ctx)
        {
            _Http = ctx;
            _Params = new RequestParameters(ctx);
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
