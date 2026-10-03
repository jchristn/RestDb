namespace RestDb
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using RestDb.Classes;
    using RestDb.Telemetry;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    partial class RestDbServer
    {
        private static async Task RunOperationAsync(RequestMetadata md, string operation, Func<RequestMetadata, Task> handler)
        {
            await RunOperationAsync(md.Http, operation, () => handler(md)).ConfigureAwait(false);
        }

        private static async Task RunOperationAsync(HttpContext ctx, string operation, Func<Task> handler)
        {
            using (ApiOperationScope scope = RestDbTelemetry.BeginOperation(operation, ctx.Request.Method.ToString(), RouteTemplate(ctx)))
            {
                TagRequestedResources(ctx);

                try
                {
                    await handler().ConfigureAwait(false);
                    scope.Complete(ctx.Response.StatusCode);
                }
                catch (Exception e)
                {
                    scope.Fail(e);
                    throw;
                }
            }
        }

        private static string TableSelectOperation(RequestMetadata md)
        {
            return md.Params.Describe ? RestDbTelemetryNames.OperationTableDescribe : RestDbTelemetryNames.OperationTableSelect;
        }

        private static string PutTableOperation(HttpContext ctx)
        {
            return ctx.Request.Url.Elements.Length == 2 ? RestDbTelemetryNames.OperationTableSearch : RestDbTelemetryNames.OperationTableUpdate;
        }

        private static string DeleteTableOperation(RequestMetadata md)
        {
            if (md.Params.Truncate) return RestDbTelemetryNames.OperationTableTruncate;
            if (md.Params.Drop) return RestDbTelemetryNames.OperationTableDrop;
            return RestDbTelemetryNames.OperationTableDelete;
        }

        /// <summary>
        /// Map a request to one of a fixed set of route templates, so the value is safe to use as a label.
        /// Database names, table names, and ids never appear in the result.
        /// </summary>
        internal static string RouteTemplate(HttpContext ctx)
        {
            if (ctx?.Request?.Url == null) return null;
            return RouteTemplate(ctx.Request.Url.Elements);
        }

        /// <summary>
        /// Map URL path segments to one of a fixed set of route templates.
        /// </summary>
        internal static string RouteTemplate(string[] elements)
        {
            if (elements == null || elements.Length == 0) return "/";

            string first = elements[0] ?? String.Empty;

            if (first.Equals("_context", StringComparison.OrdinalIgnoreCase))
            {
                if (elements.Length == 1) return "/_context";
                if (elements.Length == 2 && String.Equals(elements[1], "reload", StringComparison.OrdinalIgnoreCase)) return "/_context/reload";
                if (elements.Length == 2) return "/_context/{database}";
                if (elements.Length == 3) return "/_context/{database}/{table}";
                return "/_context/(other)";
            }

            if (first.Equals("_settings", StringComparison.OrdinalIgnoreCase))
            {
                if (elements.Length == 1) return "/_settings";
                if (elements.Length == 2 && String.Equals(elements[1], "reload", StringComparison.OrdinalIgnoreCase)) return "/_settings/reload";
                return "/_settings/(other)";
            }

            if (elements.Length == 1)
            {
                if (first.Equals("_databases", StringComparison.OrdinalIgnoreCase)) return "/_databases";
                if (first.Equals("_databaseclients", StringComparison.OrdinalIgnoreCase)) return "/_databaseclients";
                if (first.Equals("favicon.ico", StringComparison.OrdinalIgnoreCase)) return "/favicon.ico";
                if (first.Equals("robots.txt", StringComparison.OrdinalIgnoreCase)) return "/robots.txt";
                return "/{database}";
            }

            if (elements.Length == 2) return "/{database}/{table}";
            if (elements.Length == 3) return "/{database}/{table}/{id}";
            return "/(other)";
        }

        private static void TagRequestedResources(HttpContext ctx)
        {
            try
            {
                Activity current = Activity.Current;
                if (current == null || current.Source != RestDbTelemetry.Source) return;

                string[] elements = ctx.Request.Url.Elements;
                if (elements == null || elements.Length == 0) return;

                int offset = String.Equals(elements[0], "_context", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                if (offset == 0 && elements[0].StartsWith("_", StringComparison.Ordinal)) return;

                if (elements.Length > offset) current.SetTag(RestDbTelemetryNames.AttributeRequestedDatabase, elements[offset]);
                if (elements.Length > offset + 1) current.SetTag(RestDbTelemetryNames.AttributeRequestedTable, elements[offset + 1]);
            }
            catch (Exception)
            {
                // best-effort
            }
        }
    }
}
