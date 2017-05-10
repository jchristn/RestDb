using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyslogLogging;
using WatsonWebserver;

namespace RestDb
{
    partial class RestDbServer
    {
        static readonly EventWaitHandle Terminator = new EventWaitHandle(false, EventResetMode.ManualReset, "UserIntervention");
        static Settings _Settings;
        static LoggingModule _Logging;
        static Server _Server;
        static DatabaseManager _Databases;
        static AuthManager _Auth;

        static void Main(string[] args)
        {
            #region Process-Arguments

            if (args != null && args.Length > 0)
            {
                foreach (string curr in args)
                {
                    if (curr.Equals("setup")) new Setup();
                }
            }

            #endregion

            #region Load-Configuration

            if (!Common.FileExists("System.Json")) new Setup();

            _Settings = Settings.FromFile("System.Json");

            #endregion

            #region Initialize-Globals

            _Logging = new LoggingModule(
                _Settings.Logging.ServerIp,
                _Settings.Logging.ServerPort,
                _Settings.Logging.ConsoleLogging,
                (LoggingModule.Severity)_Settings.Logging.MinimumLevel,
                false,
                true,
                true,
                false,
                true,
                false);

            _Databases = new DatabaseManager(_Settings, _Logging);

            _Auth = new AuthManager(_Settings, _Logging);

            Console.Write("RestDb :: ");
            _Server = new Server(
                _Settings.Server.ListenerHostname,
                _Settings.Server.ListenerPort,
                _Settings.Server.Ssl,
                Router,
                _Settings.Server.Debug);

            #endregion

            Terminator.WaitOne();
        }

        static HttpResponse Router(HttpRequest req)
        {
            #region Setup

            DateTime startTime = DateTime.Now;
            string ipPort = SourceIpPort(req);
            _Logging.Log(LoggingModule.Severity.Debug, "Router " + ipPort + " " + req.Method + " " + req.RawUrlWithoutQuery);

            HttpResponse resp = new HttpResponse(req, false, 500, null, null, 
                Common.SerializeJson(new ErrorResponse("Internal server error", null), true), true);

            #endregion

            #region APIs

            try
            {
                #region Unauthenticated-Methods

                switch (req.Method.ToLower())
                {
                    case "get":
                        #region get

                        if (req.RawUrlWithoutQuery.Equals("/"))
                        {
                            resp = GetHelloWorld(req);
                            return resp;
                        }

                        if (req.RawUrlWithoutQuery.Equals("/favicon.ico")
                            || req.RawUrlWithoutQuery.Equals("/robots.txt"))
                        {
                            resp = new HttpResponse(req, true, 200, null, null, null, true);
                            return resp;
                        }
                        break;

                    #endregion

                    case "put":
                        #region put
                         
                        break;

                    #endregion

                    case "post":
                        #region post
                         
                        break;

                    #endregion

                    case "delete":
                        #region delete
                         
                        break;

                    #endregion

                    default:
                        _Logging.Log(LoggingModule.Severity.Warn, "Router " + ipPort + " unknown method: " + req.Method);
                        resp = new HttpResponse(req, false, 400, null, null,
                            Common.SerializeJson(new ErrorResponse("Unknown method", null), true), true);
                        return resp;
                }

                #endregion

                #region Authenticate

                if (_Settings.Server.RequireAuthentication)
                {
                    if (!_Auth.Authenticate(req))
                    {
                        _Logging.Log(LoggingModule.Severity.Warn, "Router " + ipPort + " authentication failed");
                        resp = new HttpResponse(req, false, 401, null, null,
                            Common.SerializeJson(new ErrorResponse("Unauthorized", null), true), true);
                        return resp;
                    }
                }

                #endregion

                #region Authenticated-Methods

                switch (req.Method.ToLower())
                {
                    case "get":
                        #region get
                         
                        if (req.RawUrlWithoutQuery.Equals("/_databaseclients"))
                        {
                            resp = GetDatabaseClients(req);
                            return resp;
                        }

                        if (req.RawUrlWithoutQuery.Equals("/_databases"))
                        {
                            resp = GetDatabases(req);
                            return resp;
                        }

                        if (req.RawUrlEntries.Count == 1)
                        {
                            resp = GetDatabase(req); 
                            return resp;
                        }

                        if (req.RawUrlEntries.Count == 2 || req.RawUrlEntries.Count == 3)
                        {
                            resp = GetTable(req);
                            return resp;
                        }
                        break;

                    #endregion

                    case "put":
                        #region put

                        if (req.RawUrlEntries.Count == 2 || req.RawUrlEntries.Count == 3)
                        {
                            resp = PutTable(req);
                            return resp;
                        }
                        break;

                    #endregion

                    case "post":
                        #region post

                        if (req.RawUrlEntries.Count == 2)
                        {
                            resp = PostTable(req);
                            return resp;
                        }
                        break;

                    #endregion

                    case "delete":
                        #region delete

                        if (req.RawUrlEntries.Count == 2 || req.RawUrlEntries.Count == 3)
                        {
                            resp = DeleteTable(req);
                            return resp;
                        }
                        break;

                    #endregion

                    default:
                        _Logging.Log(LoggingModule.Severity.Warn, "Router " + ipPort + " unknown method: " + req.Method);
                        resp = new HttpResponse(req, false, 400, null, null, 
                            Common.SerializeJson(new ErrorResponse("Unknown method", null), true), true);
                        return resp;
                }

                #endregion

                resp = new HttpResponse(req, false, 404, null, null, 
                    Common.SerializeJson(new ErrorResponse("Unknown API", null), true), true);
                return resp;
            }
            catch (Exception e)
            {
                _Logging.LogException("RestDbServer", "Router", e);
                resp = new HttpResponse(req, false, 500, null, null, 
                    Common.SerializeJson(new ErrorResponse("Internal server error", e.Message), true), true);
                return resp;
            }
            finally
            {
                _Logging.Log(LoggingModule.Severity.Info, "Router " + ipPort + " " + req.Method + " " + req.RawUrlWithoutQuery + " " + Common.TotalMsFrom(startTime) + "ms " + resp.StatusCode);
            }

            #endregion
        }

        static string SourceIpPort(HttpRequest req)
        {
            return req.SourceIp + ":" + req.SourcePort;
        }
    }
}
