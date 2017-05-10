using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;
using SyslogLogging;

namespace RestDb
{
    public class AuthManager
    {
        #region Constructors-and-Factories

        public AuthManager(Settings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Settings = settings;
            _Logging = logging;
            _Keys = settings.ApiKeys;
        }

        #endregion

        #region Public-Members

        #endregion

        #region Private-Members

        private Settings _Settings;
        private LoggingModule _Logging;
        private List<ApiKey> _Keys;

        #endregion

        #region Public-Methods

        public bool Authenticate(HttpRequest req)
        {
            #region Check-for-Null-Values

            if (req == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "Authenticate null HTTP request supplied, returning false");
                return false;
            }

            #endregion

            #region Extract-API-Key

            string apiKey = req.RetrieveHeaderValue(_Settings.Server.ApiKeyHeader);
            if (String.IsNullOrEmpty(apiKey))
            {
                _Logging.Log(LoggingModule.Severity.Warn, "Authenticate unable to retrieve API key from headers");
                return false;
            }

            #endregion

            #region Compare

            if (_Keys != null && _Keys.Count > 0)
            {
                foreach (ApiKey curr in _Keys)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (curr.Key.ToLower().Equals(apiKey.ToLower()))
                    {
                        switch (req.Method.ToLower())
                        {
                            case "get":
                            case "head":
                                return curr.AllowGet;

                            case "put":
                                return curr.AllowPut;

                            case "post":
                                return curr.AllowPost;

                            case "delete":
                                return curr.AllowDelete;

                            default:
                                _Logging.Log(LoggingModule.Severity.Warn, "Authenticate unknown HTTP method " + req.Method);
                                return false;
                        }
                    }
                }

                _Logging.Log(LoggingModule.Severity.Warn, "Authenticate unknown API key " + apiKey);
                return false;
            }
            else
            {
                _Logging.Log(LoggingModule.Severity.Warn, "Authenticate no API keys defined in configuration");
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
