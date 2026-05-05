namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RestDb.Classes;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    partial class RestDbServer
    {
        private static void ApplyCorsHeaders(HttpContext ctx)
        {
            if (ctx == null || ctx.Response == null) return;

            if (!ctx.Response.Headers.AllKeys.Contains("Access-Control-Allow-Origin"))
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            }

            if (!ctx.Response.Headers.AllKeys.Contains("Access-Control-Allow-Methods"))
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, PUT, POST, DELETE, OPTIONS, HEAD");
            }

            if (!ctx.Response.Headers.AllKeys.Contains("Access-Control-Allow-Headers"))
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", BuildCorsAllowHeaders(ctx));
            }

            if (!ctx.Response.Headers.AllKeys.Contains("Access-Control-Expose-Headers"))
            {
                ctx.Response.Headers.Add("Access-Control-Expose-Headers", Constants.HeaderExpression);
            }

            if (!ctx.Response.Headers.AllKeys.Contains("Access-Control-Max-Age"))
            {
                ctx.Response.Headers.Add("Access-Control-Max-Age", "86400");
            }
        }

        private static string BuildCorsAllowHeaders(HttpContext ctx)
        {
            List<string> values = new List<string>
            {
                "Accept",
                "Authorization",
                "Content-Type",
                "Origin",
                "X-Requested-With"
            };

            if (_Settings != null
                && _Settings.Server != null
                && !String.IsNullOrWhiteSpace(_Settings.Server.ApiKeyHeader))
            {
                values.Add(_Settings.Server.ApiKeyHeader);
            }

            string requestedHeaders = ctx.Request.RetrieveHeaderValue("Access-Control-Request-Headers");
            if (!String.IsNullOrWhiteSpace(requestedHeaders))
            {
                string[] requestedHeadersArray = requestedHeaders.Split(',');
                foreach (string requestedHeader in requestedHeadersArray)
                {
                    if (String.IsNullOrWhiteSpace(requestedHeader)) continue;
                    values.Add(requestedHeader.Trim());
                }
            }

            return String.Join(", ", values.Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
