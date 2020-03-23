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
                (Severity)_Settings.Logging.MinimumLevel,
                false,
                true,
                true,
                false,
                true,
                false);

            _Databases = new DatabaseManager(_Settings, _Logging);

            _Auth = new AuthManager(_Settings, _Logging);

            Console.WriteLine(Logo());

            _Server = new Server(
                _Settings.Server.ListenerHostname,
                _Settings.Server.ListenerPort,
                _Settings.Server.Ssl,
                DefaultRoute);

            string header = "http";
            if (_Settings.Server.Ssl) header += "s";
            header += "://" + _Settings.Server.ListenerHostname + ":" + _Settings.Server.ListenerPort;
            Console.WriteLine("Listening for requests on " + header);

            #endregion

            Terminator.WaitOne();
        }

        static async Task DefaultRoute(HttpContext ctx)
        { 
            DateTime startTime = DateTime.Now;
            string header = ctx.Request.SourceIp + ":" + ctx.Request.SourcePort + " "; 
            _Logging.Debug(header + ctx.Request.Method + " " + ctx.Request.RawUrlWithoutQuery);
             
            #region APIs

            try
            {
                #region Unauthenticated-Methods

                switch (ctx.Request.Method)
                {
                    case HttpMethod.GET:
                        #region get

                        if (ctx.Request.RawUrlWithoutQuery.Equals("/"))
                        {
                            ctx.Response.StatusCode = 200;
                            ctx.Response.ContentType = "text/html; charset=utf8";
                            await ctx.Response.Send(RootHtml());
                            return;
                        }

                        if (ctx.Request.RawUrlWithoutQuery.Equals("/favicon.ico")
                            || ctx.Request.RawUrlWithoutQuery.Equals("/robots.txt"))
                        {
                            ctx.Response.StatusCode = 200;
                            await ctx.Response.Send();
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.PUT:
                        #region put
                         
                        break;

                    #endregion

                    case HttpMethod.POST:
                        #region post
                         
                        break;

                    #endregion

                    case HttpMethod.DELETE:
                        #region delete
                         
                        break;

                    #endregion

                    default:
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Unknown method", null), true));
                        return;
                }

                #endregion

                #region Authenticate

                if (_Settings.Server.RequireAuthentication)
                {
                    if (!_Auth.Authenticate(ctx))
                    {
                        _Logging.Warn(header + "authentication failed");
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Unauthorized", null), true));
                        return;
                    }
                }

                #endregion

                #region Authenticated-Methods

                switch (ctx.Request.Method)
                {
                    case HttpMethod.GET:
                        #region get
                         
                        if (ctx.Request.RawUrlWithoutQuery.Equals("/_databaseclients"))
                        {
                            await GetDatabaseClients(ctx);
                            return;
                        }

                        if (ctx.Request.RawUrlWithoutQuery.Equals("/_databases"))
                        {
                            await GetDatabases(ctx);
                            return;
                        }

                        if (ctx.Request.RawUrlEntries.Count == 1)
                        {
                            await GetDatabase(ctx);
                            return;
                        }

                        if (ctx.Request.RawUrlEntries.Count == 2 || ctx.Request.RawUrlEntries.Count == 3)
                        {
                            await GetTableSelect(ctx);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.PUT:
                        #region put

                        if (ctx.Request.RawUrlEntries.Count == 2 || ctx.Request.RawUrlEntries.Count == 3)
                        {
                            await PutTable(ctx);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.POST:
                        #region post

                        if (ctx.Request.RawUrlEntries.Count == 1)
                        {
                            await PostTableCreate(ctx);
                            return;
                        }

                        if (ctx.Request.RawUrlEntries.Count == 2)
                        {
                            await PostTableInsert(ctx);
                            return;
                        }
                        break;

                    #endregion

                    case HttpMethod.DELETE:
                        #region delete

                        if (ctx.Request.RawUrlEntries.Count == 2 || ctx.Request.RawUrlEntries.Count == 3)
                        {
                            await DeleteTable(ctx);
                            return;
                        }
                        break;

                    #endregion

                    default:
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Unknown method", null), true));
                        return;
                }

                #endregion

                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Unknown API", null), true)); 
            }
            catch (Exception e)
            {
                _Logging.Exception("RestDb", "Router", e);
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Internal server error", e.Message), true));
            }
            finally
            {
                _Logging.Debug(header + ctx.Request.Method + " " + ctx.Request.RawUrlWithoutQuery + " " + Common.TotalMsFrom(startTime) + "ms: " + ctx.Response.StatusCode);
            }

            #endregion
        }

        private static string Logo()
        {
            return
                Environment.NewLine +
                Environment.NewLine +
                @"                 _      _ _      " + Environment.NewLine +
                @"   _ __ ___  ___| |_ __| | |__   " + Environment.NewLine +
                @"  | '__/ _ \/ __| __/ _  |  _ \  " + Environment.NewLine +
                @"  | | |  __/\__ \ || (_| | |_) | " + Environment.NewLine +
                @"  |_|  \___||___/\__\__,_|_.__/  " + Environment.NewLine +
                Environment.NewLine;
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

            ret += Logo() + Environment.NewLine;
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
