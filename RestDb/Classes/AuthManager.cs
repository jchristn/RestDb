using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;
using SyslogLogging;

namespace RestDb
{
    internal class AuthManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private Settings _Settings;
        private LoggingModule _Logging;
        private readonly object _KeysLock = new object();
        private List<ApiKey> _Keys = new List<ApiKey>();

        #endregion

        #region Constructors-and-Factories

        internal AuthManager(Settings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Settings = settings;
            _Logging = logging;
            _Keys = settings.ApiKeys;
        }

        #endregion

        #region Public-Methods

        internal bool Authenticate(HttpContext ctx)
        { 
            #region Extract-API-Key

            string apiKey = ctx.Request.RetrieveHeaderValue(_Settings.Server.ApiKeyHeader);
            if (String.IsNullOrEmpty(apiKey))
            {
                _Logging.Warn("Authenticate unable to retrieve API key from headers");
                return false;
            }

            #endregion

            #region Compare

            if (_Keys != null && _Keys.Count > 0)
            {
                lock (_KeysLock)
                {
                    if (_Keys.Exists(k => k.Key.Equals(apiKey)))
                    {
                        ApiKey curr = _Keys.Where(k => k.Key.Equals(apiKey)).First();
                        switch (ctx.Request.Method)
                        {
                            case HttpMethod.GET:
                            case HttpMethod.HEAD:
                                return curr.AllowGet;

                            case HttpMethod.PUT:
                                return curr.AllowPut;

                            case HttpMethod.POST:
                                return curr.AllowPost;

                            case HttpMethod.DELETE:
                                return curr.AllowDelete;

                            default:
                                _Logging.Warn("Authenticate unknown HTTP method " + ctx.Request.Method);
                                return false;
                        }
                    }
                }

                _Logging.Warn("Authenticate unknown API key " + apiKey);
                return false;
            }
            else
            {
                _Logging.Warn("Authenticate no API keys defined in configuration");
                return false;
            }

            #endregion
        }

        #endregion

        #region Private-Methods

        #endregion

        #region Public-Static-Methods

        #endregion

        #region Private-Static-Methods

        #endregion
    }
}
