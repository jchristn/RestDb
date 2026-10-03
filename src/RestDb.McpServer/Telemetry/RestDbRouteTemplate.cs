namespace RestDb.McpServer.Telemetry
{
    using System;

    /// <summary>
    /// Maps a RestDb API path to one of a fixed set of route templates, mirroring the RestDb server's routing, so the
    /// value is safe to use as a metric label. Database names, table names, ids, and query strings never appear in the result.
    /// Thread safety: stateless; safe for concurrent use.
    /// </summary>
    internal static class RestDbRouteTemplate
    {
        /// <summary>
        /// Map a path (optionally with a query string) to its route template.
        /// </summary>
        /// <param name="pathAndQuery">Path and query, for example /sample/person/1?_describe=true.</param>
        /// <returns>Route template, for example /{database}/{table}/{id}.</returns>
        internal static string FromPath(string? pathAndQuery)
        {
            if (String.IsNullOrWhiteSpace(pathAndQuery)) return "/";

            string path = pathAndQuery;
            int query = path.IndexOf('?');
            if (query >= 0) path = path.Substring(0, query);

            string[] elements = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (elements.Length == 0) return "/";

            string first = elements[0];

            if (first.Equals("_context", StringComparison.OrdinalIgnoreCase))
            {
                if (elements.Length == 1) return "/_context";
                if (elements.Length == 2 && elements[1].Equals("reload", StringComparison.OrdinalIgnoreCase)) return "/_context/reload";
                if (elements.Length == 2) return "/_context/{database}";
                if (elements.Length == 3) return "/_context/{database}/{table}";
                return "/_context/(other)";
            }

            if (first.Equals("_settings", StringComparison.OrdinalIgnoreCase))
            {
                if (elements.Length == 1) return "/_settings";
                if (elements.Length == 2 && elements[1].Equals("reload", StringComparison.OrdinalIgnoreCase)) return "/_settings/reload";
                return "/_settings/(other)";
            }

            if (elements.Length == 1)
            {
                if (first.Equals("_databases", StringComparison.OrdinalIgnoreCase)) return "/_databases";
                if (first.Equals("_databaseclients", StringComparison.OrdinalIgnoreCase)) return "/_databaseclients";
                return "/{database}";
            }

            if (elements.Length == 2) return "/{database}/{table}";
            if (elements.Length == 3) return "/{database}/{table}/{id}";
            return "/(other)";
        }
    }
}
