﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RestDb.Classes;
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

            if (!File.Exists("./system.json")) new Setup();

            _Settings = Settings.FromFile("./system.json");

            #endregion

            #region Initialize-Globals

            Welcome();

            _Logging = new LoggingModule(
                _Settings.Logging.ServerIp,
                _Settings.Logging.ServerPort,
                _Settings.Logging.ConsoleLogging);

            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumLevel;

            _Databases = new DatabaseManager(_Settings, _Logging);

            _Auth = new AuthManager(_Settings, _Logging);

            _WebserverSettings = new WebserverSettings() {
                Hostname = _Settings.Server.ListenerHostname,
                Port = _Settings.Server.ListenerPort,
                Ssl = new WebserverSettings.SslSettings() {
                    Enable = _Settings.Server.Ssl,
                }
            };

            _Server = new Webserver(
                _WebserverSettings,
                DefaultRoute);

            _Server.Start();

            string header = "http";
            if (_Settings.Server.Ssl) header += "s";
            header += "://" + _Settings.Server.ListenerHostname + ":" + _Settings.Server.ListenerPort;
            Console.WriteLine("Listening for requests on " + header);

            #endregion

            Terminator.WaitOne();
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
                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentType = "text/html; charset=utf8";
                            await ctx.Response.Send(RootHtml());
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/favicon.ico")
                            || ctx.Request.Url.RawWithoutQuery.Equals("/robots.txt"))
                        {
                            ctx.Response.StatusCode = 200;
                            await ctx.Response.Send();
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
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Unknown method", null), true));
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
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.NotAuthenticated, "You are not authorized to perform this operation", null), true));
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
                            await GetDatabaseClients(md);
                            return;
                        }

                        if (ctx.Request.Url.RawWithoutQuery.Equals("/_databases"))
                        {
                            await GetDatabases(md);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 1)
                        {
                            await GetDatabase(md);
                            return;
                        }

                        if (ctx.Request.Url.Elements.Length == 2 || ctx.Request.Url.Elements.Length == 3)
                        {
                            await GetTableSelect(md);
                            return;
                        }

                        break;

                    #endregion

                    case HttpMethod.PUT:
                        #region PUT

                        if (ctx.Request.Url.Elements.Length == 2 || ctx.Request.Url.Elements.Length == 3)
                        {
                            await PutTable(md);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.POST:
                        #region POST

                        if (ctx.Request.Url.Elements.Length == 1)
                        {
                            if (ctx.Request.Query.Elements.AllKeys.Contains("raw"))
                            {
                                await PostRawQuery(md);
                                return;
                            }
                            else
                            {
                                await PostTableCreate(md);
                                return;
                            }
                        }

                        if (ctx.Request.Url.Elements.Length == 2)
                        {
                            await PostTableInsert(md);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.DELETE:
                        #region DELETE

                        if (ctx.Request.Url.Elements.Length == 2 || ctx.Request.Url.Elements.Length == 3)
                        {
                            await DeleteTable(md);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.OPTIONS:
                        #region OPTIONS
                        ctx.Response.StatusCode = 200;
                        ctx.Response.Headers.Add("Allow", "GET, PUT, POST, DELETE, OPTIONS");
                        await ctx.Response.Send();
                        return;

                    #endregion

                    default:
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Unknown method", null), true));
                        return;
                }

                #endregion

                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InvalidRequest, "Unknown endpoint", null), true)); 
            }
            catch (Exception e)
            {
                _Logging.Exception(e);
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(SerializationHelper.SerializeJson(new ErrorResponse(ErrorCodeEnum.InternalError, "Internal server error", e.Message, e), true));
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
