using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RestDb.Classes;
using RestDb.Telemetry;
using SyslogLogging;
using WatsonWebserver;
using WatsonWebserver.Core;

namespace RestDb
{
    partial class RestDbServer
    {
        static string _Version;
        static readonly EventWaitHandle Terminator = new EventWaitHandle(false, EventResetMode.ManualReset);
        static Settings _Settings;
        static WebserverSettings _WebserverSettings;
        static LoggingModule _Logging;
        static Webserver _Server;
        static DatabaseManager _Databases;
        static AuthManager _Auth;
        static TelemetryHost _Telemetry;
        static int _ShutdownStarted = 0;

        static void Main(string[] args)
        {
            _Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

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

            InitializeRuntimeState();

            #endregion

            #region Initialize-Globals

            Welcome();

            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Shutdown();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Shutdown();
            };

            if (_Telemetry != null && !String.IsNullOrEmpty(_Telemetry.StartupError))
            {
                _Logging.Warn("Telemetry export disabled, host failed to start: " + _Telemetry.StartupError);
            }
            else if (_Telemetry != null && _Telemetry.ScrapeUrl != null)
            {
                _Logging.Info("Telemetry Prometheus endpoint: " + _Telemetry.ScrapeUrl);
            }

            _WebserverSettings = new WebserverSettings();
            _WebserverSettings.Hostname = _Settings.Server.ListenerHostname;
            _WebserverSettings.Port = _Settings.Server.ListenerPort;
            _WebserverSettings.Ssl.Enable = _Settings.Server.Ssl;

            // Watson emits the HTTP layer (request metrics and one server span per request) on the "Watson" meter and
            // activity source, which the telemetry host subscribes to. Confirm the defaults explicitly.
            _WebserverSettings.Telemetry.Enable = true;
            _WebserverSettings.Telemetry.EnableMetrics = true;
            _WebserverSettings.Telemetry.EnableTraces = true;
            _WebserverSettings.Telemetry.PropagateContext = true;

            _Server = new Webserver(
                _WebserverSettings,
                DefaultRoute);
            _Server.Routes.PreRouting = PreRouting;
            _Server.Routes.PostRouting = PostRouting;
            _Server.Routes.Preflight = PreflightRoute;

            _Server.Start();

            string header = "http";
            if (_Settings.Server.Ssl) header += "s";
            header += "://" + _Settings.Server.ListenerHostname + ":" + _Settings.Server.ListenerPort;
            Console.WriteLine("Listening for requests on " + header);

            #endregion

            Terminator.WaitOne();
        }

        private static void Shutdown()
        {
            if (Interlocked.Exchange(ref _ShutdownStarted, 1) == 1) return;

            try
            {
                _Server?.Stop();
            }
            catch (Exception)
            {
            }

            _Telemetry?.Dispose();
            Terminator.Set();
        }

        private static void Welcome()
        {
            ConsoleColor prior = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(Constants.Logo);
            Console.WriteLine("RestDb | RESTful API for databases | v" + _Version);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("");

            if (_Settings.Server.ListenerHostname.Equals("localhost") || _Settings.Server.ListenerHostname.Equals("127.0.0.1"))
            {
                //                          1         2         3         4         5         6         7         8
                //                 12345678901234567890123456789012345678901234567890123456789012345678901234567890
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNING: RestDb started on '" + _Settings.Server.ListenerHostname + "'");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("RestDb can only service requests from the local machine.  If you wish to serve");
                Console.WriteLine("external requests, edit the System.json file and specify a DNS-resolvable");
                Console.WriteLine("hostname in the Server.ListenerHostname field.");
                Console.WriteLine("");
            }

            List<string> adminListeners = new List<string> { "*", "+", "0.0.0.0" };

            if (adminListeners.Contains(_Settings.Server.ListenerHostname))
            {
                //                          1         2         3         4         5         6         7         8
                //                 12345678901234567890123456789012345678901234567890123456789012345678901234567890
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("NOTICE: RestDb listening on a wildcard hostname: '" + _Settings.Server.ListenerHostname + "'");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("RestDb must be run with administrative privileges, otherwise it will not be");
                Console.WriteLine("able to respond to incoming requests.");
                Console.WriteLine("");
            }

            Console.ForegroundColor = prior;
        }

        static async Task DefaultRoute(HttpContextBase ctxBase)
        {
            HttpContext ctx = (HttpContext)ctxBase;
            DateTime startTime = DateTime.Now;
            string header = ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " "; 
            _Logging.Debug(header + ctx.Request.Method + " " + ctx.Request.Url.RawWithoutQuery);

            #region APIs

            try
            {
                #region Unauthenticated-Methods

                switch (ctx.Request.Method)
                {
                    case HttpMethod.GET:
                        #region GET

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/"))
                        {
                            await RunOperationAsync(ctx, RestDbTelemetryNames.OperationRoot, async () =>
                            {
                                ctx.Response.StatusCode = 200;
                                ctx.Response.ContentType = "text/html; charset=utf-8";
                                await ctx.Response.Send(RootHtml());
                            });
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/favicon.ico")
                            || ctx.Request.Url.RawWithoutQuery.Equals("/robots.txt"))
                        {
                            await RunOperationAsync(ctx, RestDbTelemetryNames.OperationStatic, async () =>
                            {
                                ctx.Response.StatusCode = 200;
                                await ctx.Response.Send();
                            });
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.PUT:
                        #region PUT
                         
                        break;

                    #endregion

                    case HttpMethod.POST:
                        #region POST
                         
                        break;

                    #endregion

                    case HttpMethod.DELETE:
                        #region DELETE
                         
                        break;

                    #endregion

                    case HttpMethod.OPTIONS:
                        #region OPTIONS
                        break;

                    #endregion

                    default:
                        await RunOperationAsync(ctx, RestDbTelemetryNames.OperationUnknown, async () =>
                        {
                            ctx.Response.StatusCode = 400;
                            ctx.Response.ContentType = Constants.JsonContentType;
                            await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Unknown method."), true));
                        });
                        return;
                }

                #endregion

                #region Build-Metadata

                RequestMetadata md = new RequestMetadata(ctx);

                #endregion

                #region Authenticate

                if (_Settings.Server.RequireAuthentication)
                {
                    if (!_Auth.Authenticate(ctx, out string apiKey, out ApiKey key))
                    {
                        _Logging.Warn(header + "authentication failed");
                        RestDbTelemetry.TagServerSpan(ctx.Request.Method.ToString(), RouteTemplate(ctx), null);
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.LoginFailed), true));
                        return;
                    }

                    md.ApiKey = key;
                    md.Params.ApiKey = apiKey;
                }

                #endregion

                #region Authenticated-Methods

                switch (ctx.Request.Method)
                {
                    case HttpMethod.GET:
                        #region GET
                         
                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_databaseclients"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationDatabaseClients, GetDatabaseClients);
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_settings"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationSettingsRead, GetServerSettings);
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_context"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationContextRead, GetContextFile);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 2
                            && ctx.Request.Url.Elements[0].Equals("_context", StringComparison.OrdinalIgnoreCase))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationDatabaseContextRead, GetDatabaseContext);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 3
                            && ctx.Request.Url.Elements[0].Equals("_context", StringComparison.OrdinalIgnoreCase))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationTableContextRead, GetTableContext);
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_databases"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationDatabasesList, GetDatabases);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 1)
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationDatabaseRead, GetDatabase);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 2 || ctx.Request.Url.Elements.Length == 3)
                        {
                            await RunOperationAsync(md, TableSelectOperation(md), GetTableSelect);
                            return;
                        }

                        break;

                    #endregion

                    case HttpMethod.PUT:
                        #region PUT

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_settings"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationSettingsUpdate, PutServerSettings);
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_context"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationContextUpdate, PutContextFile);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 2
                            && ctx.Request.Url.Elements[0].Equals("_context", StringComparison.OrdinalIgnoreCase))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationDatabaseContextUpdate, PutDatabaseContext);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 3
                            && ctx.Request.Url.Elements[0].Equals("_context", StringComparison.OrdinalIgnoreCase))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationTableContextUpdate, PutTableContext);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 2 || ctx.Request.Url.Elements.Length == 3)
                        {
                            await RunOperationAsync(md, PutTableOperation(ctx), PutTable);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.POST:
                        #region POST

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_settings/reload"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationSettingsReload, PostServerSettingsReload);
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_context/reload"))
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationContextReload, PostContextReload);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 1)
                        {
                            if (ctx.Request.Query.Elements.AllKeys.Contains("raw"))
                            {
                                await RunOperationAsync(md, RestDbTelemetryNames.OperationRawQuery, PostRawQuery);
                                return;
                            }
                            else
                            {
                                await RunOperationAsync(md, RestDbTelemetryNames.OperationTableCreate, PostTableCreate);
                                return;
                            }
                        }

                        if (ctx.Request.Url.Elements.Length == 2)
                        {
                            await RunOperationAsync(md, RestDbTelemetryNames.OperationTableInsert, PostTableInsert);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.DELETE:
                        #region DELETE

                        if (ctx.Request.Url.Elements.Length == 2 || ctx.Request.Url.Elements.Length == 3)
                        {
                            await RunOperationAsync(md, DeleteTableOperation(md), DeleteTable);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.OPTIONS:
                        #region OPTIONS
                        #endregion
                        break;

                    default:
                        await RunOperationAsync(ctx, RestDbTelemetryNames.OperationUnknown, async () =>
                        {
                            ctx.Response.StatusCode = 400;
                            ctx.Response.ContentType = Constants.JsonContentType;
                            await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Unknown method."), true));
                        });
                        return;
                }

                #endregion

                await RunOperationAsync(ctx, RestDbTelemetryNames.OperationUnknown, async () =>
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = Constants.JsonContentType;
                    await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Unknown endpoint."), true));
                });
            }
            catch (Exception e)
            {
                _Logging.Exception(e);
                if (Activity.Current != null) _Logging.Warn(header + "request failed, trace_id " + Activity.Current.TraceId.ToHexString());
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = Constants.JsonContentType;
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InternalError, e.Message), true));
            }
            finally
            {
                _Logging.Debug(header + ctx.Request.Method + " " + ctx.Request.Url.RawWithoutQuery + " " + Common.TotalMsFrom(startTime) + "ms: " + ctx.Response.StatusCode);
            }

            #endregion
        }

        private static string RootHtml()
        {
            string ret =
                "<html>" +
                "  <head>" +
                "    <title>RestDb</title>" +
                "  </head>" +
                "  <body>" +
                "    <pre>";

            ret += Constants.Logo + Environment.NewLine;
            ret += "RestDb is running." + Environment.NewLine;
            ret += "Documentation and source code: <a href='https://github.com/jchristn/restdb' target='_blank'>https://github.com/jchristn/restdb</a>" + Environment.NewLine;
            ret +=
                "    </pre>" +
                "  </body>" +
                "</html>";
            return ret;
        }
    }
}
