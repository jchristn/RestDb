namespace RestDb.McpServer.Registrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using RestDb.McpServer.Classes;

    internal static class RestMcpToolCatalog
    {
        public static List<RestMcpToolDefinition> Build(RestMcpRestProxy proxy)
        {
            if (proxy == null) throw new ArgumentNullException(nameof(proxy));

            return new List<RestMcpToolDefinition>
            {
                new RestMcpToolDefinition(
                    "restdb_check_system_health",
                    "Checks whether the RestDb HTTP service is reachable and returns the root response payload.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Get, "/", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_retrieve_database_client_capabilities",
                    "Returns the configured database client types supported by this RestDb server.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Get, "/_databaseclients", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_retrieve_database_list",
                    "Returns the list of configured database names.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Get, "/_databases", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_retrieve_server_settings",
                    "Returns the current live restdb.json configuration as seen by the running server.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Get, "/_settings", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_update_server_settings",
                    "Replaces the live server settings with a new restdb.json document and persists it to disk. Listener host, port, and SSL changes may require a process restart.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            settings = new { type = "object", description = "Full restdb.json settings document." }
                        },
                        required = new[] { "settings" }
                    },
                    async (args, token) => await proxy.SendAsync(HttpMethod.Put, "/_settings", RequireRawJsonProperty(args, "settings"), token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_reload_server_settings",
                    "Reloads restdb.json from disk into the running server.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Post, "/_settings/reload", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_retrieve_context_document",
                    "Returns the full shared context.json document containing database and table context.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Get, "/_context", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_update_context_document",
                    "Replaces the full context.json document and persists it to disk.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            context = new { type = "object", description = "Full context.json document." }
                        },
                        required = new[] { "context" }
                    },
                    async (args, token) => await proxy.SendAsync(HttpMethod.Put, "/_context", RequireRawJsonProperty(args, "context"), token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_reload_context_document",
                    "Reloads context.json from disk into the running server.",
                    EmptyObjectSchema(),
                    (args, token) => proxy.SendAsync(HttpMethod.Post, "/_context/reload", null, token).ContinueWith<object>(t => t.Result, token)),

                new RestMcpToolDefinition(
                    "restdb_retrieve_database_context",
                    "Returns the context record for a database, including table-level context entries.",
                    DatabaseTargetSchema(),
                    async (args, token) => await proxy.SendAsync(HttpMethod.Get, "/_context/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")), null, token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_update_database_context",
                    "Updates the context record for a database, including optional per-table descriptions.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            context = new { type = "string", description = "Database-level context description." },
                            tables = new { type = "object", description = "Optional table name to context map.", additionalProperties = new { type = "string" } }
                        },
                        required = new[] { "databaseName" }
                    },
                    async (args, token) =>
                    {
                        string databaseName = RequireString(args, "databaseName");
                        string body = JsonSerializer.Serialize(new
                        {
                            database = databaseName,
                            context = GetOptionalString(args, "context"),
                            tables = GetOptionalJsonObject(args, "tables")
                        });

                        return await proxy.SendAsync(HttpMethod.Put, "/_context/" + RestMcpRestProxy.Escape(databaseName), body, token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_retrieve_table_context",
                    "Returns the context description for a specific table.",
                    DatabaseTableTargetSchema(),
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Get,
                        "/_context/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName")),
                        null,
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_update_table_context",
                    "Updates the context description for a specific table.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            tableName = new { type = "string", description = "Table name." },
                            context = new { type = "string", description = "Table-level context description." }
                        },
                        required = new[] { "databaseName", "tableName", "context" }
                    },
                    async (args, token) =>
                    {
                        string databaseName = RequireString(args, "databaseName");
                        string tableName = RequireString(args, "tableName");
                        string body = JsonSerializer.Serialize(new
                        {
                            database = databaseName,
                            table = tableName,
                            context = RequireString(args, "context")
                        });

                        return await proxy.SendAsync(
                            HttpMethod.Put,
                            "/_context/" + RestMcpRestProxy.Escape(databaseName) + "/" + RestMcpRestProxy.Escape(tableName),
                            body,
                            token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_inspect_database",
                    "Returns metadata for a configured database, including its provider and table list, with optional context enrichment.",
                    DatabaseTargetSchema(includeContext: true),
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Get,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + BuildContextQuery(args),
                        null,
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_inspect_database_with_schema",
                    "Returns metadata for a configured database and describes every table in that database, with optional context enrichment.",
                    DatabaseTargetSchema(includeContext: true),
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Get,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "?_describe=true" + BuildContextQuery(args, hasExistingQuery: true),
                        null,
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_inspect_table_schema",
                    "Returns the schema description for a specific table, with optional context enrichment.",
                    DatabaseTableTargetSchema(includeContext: true),
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Get,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName")) + "?_describe=true" + BuildContextQuery(args, hasExistingQuery: true),
                        null,
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_enumerate_table_records",
                    "Enumerates records from a table with optional filters, pagination, ordering, and field projection.",
                    SelectSchema(includeRowId: false),
                    async (args, token) =>
                    {
                        string path = "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName"));
                        path += BuildSelectQuery(args);
                        return await proxy.SendAsync(HttpMethod.Get, path, null, token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_retrieve_table_record_by_id",
                    "Retrieves a single table record by primary key, with optional additional filters and field projection.",
                    SelectSchema(includeRowId: true),
                    async (args, token) =>
                    {
                        string path = "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName"))
                            + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName"))
                            + "/" + RequireInt(args, "rowId");
                        path += BuildSelectQuery(args);
                        return await proxy.SendAsync(HttpMethod.Get, path, null, token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_search_table_records",
                    "Searches table records using an ExpressionTree expression payload, with optional querystring filters, pagination, ordering, projection, and debug output.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            tableName = new { type = "string", description = "Table name." },
                            expression = new { type = "object", description = "ExpressionTree expression payload." },
                            filters = new { type = "object", description = "Optional querystring equality filters.", additionalProperties = true },
                            indexStart = new { type = "integer", description = "Optional result index start." },
                            maxResults = new { type = "integer", description = "Optional maximum number of results." },
                            orderBy = new { description = "Optional field or array of fields to order by." },
                            orderDirection = new { type = "string", description = "asc or desc." },
                            returnFields = new { description = "Optional field or array of fields to return." },
                            debug = new { type = "boolean", description = "If true, includes x-expression debug output from RestDb." }
                        },
                        required = new[] { "databaseName", "tableName", "expression" }
                    },
                    async (args, token) =>
                    {
                        string path = "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName"));
                        path += BuildSelectQuery(args, includeDebug: true);
                        return await proxy.SendAsync(HttpMethod.Put, path, RequireRawJsonProperty(args, "expression"), token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_update_table_record_by_id",
                    "Updates a single table record by primary key.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            tableName = new { type = "string", description = "Table name." },
                            rowId = new { type = "integer", description = "Primary key value." },
                            values = new { type = "object", description = "Dictionary of updated field values." }
                        },
                        required = new[] { "databaseName", "tableName", "rowId", "values" }
                    },
                    async (args, token) =>
                    {
                        string path = "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName"))
                            + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName"))
                            + "/" + RequireInt(args, "rowId");
                        return await proxy.SendAsync(HttpMethod.Put, path, RequireRawJsonProperty(args, "values"), token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_create_table",
                    "Creates a table in a configured database using the supplied table schema payload.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            table = new { type = "object", description = "Table schema payload." }
                        },
                        required = new[] { "databaseName", "table" }
                    },
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Post,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")),
                        RequireRawJsonProperty(args, "table"),
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_insert_table_record",
                    "Inserts a single record into a table.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            tableName = new { type = "string", description = "Table name." },
                            record = new { type = "object", description = "Single record payload." }
                        },
                        required = new[] { "databaseName", "tableName", "record" }
                    },
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Post,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName")),
                        RequireRawJsonProperty(args, "record"),
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_insert_table_records",
                    "Inserts multiple records into a table using the RestDb _multiple route.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            tableName = new { type = "string", description = "Table name." },
                            records = new { type = "array", description = "Array of record payloads.", items = new { type = "object" } }
                        },
                        required = new[] { "databaseName", "tableName", "records" }
                    },
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Post,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName")) + "?_multiple=true",
                        RequireRawJsonProperty(args, "records"),
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_execute_raw_sql",
                    "Executes raw SQL against a configured database using the RestDb raw SQL route.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            databaseName = new { type = "string", description = "Configured RestDb database name." },
                            sql = new { type = "string", description = "SQL statement to execute." }
                        },
                        required = new[] { "databaseName", "sql" }
                    },
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Post,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "?raw=true",
                        RequireString(args, "sql"),
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_delete_table_records",
                    "Deletes table records that match optional querystring equality filters. If no filters are supplied, all records are deleted.",
                    DeleteSchema(includeRowId: false),
                    async (args, token) =>
                    {
                        string path = "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName"));
                        path += BuildFilterQuery(args);
                        return await proxy.SendAsync(HttpMethod.Delete, path, null, token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_delete_table_record_by_id",
                    "Deletes a single record by primary key, with optional additional equality filters.",
                    DeleteSchema(includeRowId: true),
                    async (args, token) =>
                    {
                        string path = "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName"))
                            + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName"))
                            + "/" + RequireInt(args, "rowId");
                        path += BuildFilterQuery(args);
                        return await proxy.SendAsync(HttpMethod.Delete, path, null, token).ConfigureAwait(false);
                    }),

                new RestMcpToolDefinition(
                    "restdb_truncate_table",
                    "Removes all records from a table using the provider-specific truncate or clear-table path.",
                    DatabaseTableTargetSchema(),
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Delete,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName")) + "?_truncate=true",
                        null,
                        token).ConfigureAwait(false)),

                new RestMcpToolDefinition(
                    "restdb_drop_table",
                    "Drops a table from a configured database.",
                    DatabaseTableTargetSchema(),
                    async (args, token) => await proxy.SendAsync(
                        HttpMethod.Delete,
                        "/" + RestMcpRestProxy.Escape(RequireString(args, "databaseName")) + "/" + RestMcpRestProxy.Escape(RequireString(args, "tableName")) + "?_drop=true",
                        null,
                        token).ConfigureAwait(false))
            };
        }

        private static object EmptyObjectSchema()
        {
            return new
            {
                type = "object",
                properties = new { }
            };
        }

        private static object DatabaseTargetSchema(bool includeContext = false)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                ["databaseName"] = new { type = "string", description = "Configured RestDb database name." }
            };

            if (includeContext)
            {
                properties["includeContext"] = new { type = "boolean", description = "If true, appends `_context=true` so database and table context are included when the HTTP response shape supports it." };
            }

            return new
            {
                type = "object",
                properties,
                required = new[] { "databaseName" }
            };
        }

        private static object DatabaseTableTargetSchema(bool includeContext = false)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                ["databaseName"] = new { type = "string", description = "Configured RestDb database name." },
                ["tableName"] = new { type = "string", description = "Table name." }
            };

            if (includeContext)
            {
                properties["includeContext"] = new { type = "boolean", description = "If true, appends `_context=true` so table context is included when the HTTP response shape supports it." };
            }

            return new
            {
                type = "object",
                properties,
                required = new[] { "databaseName", "tableName" }
            };
        }

        private static object SelectSchema(bool includeRowId)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                ["databaseName"] = new { type = "string", description = "Configured RestDb database name." },
                ["tableName"] = new { type = "string", description = "Table name." },
                ["filters"] = new { type = "object", description = "Optional querystring equality filters.", additionalProperties = true },
                ["indexStart"] = new { type = "integer", description = "Optional result index start." },
                ["maxResults"] = new { type = "integer", description = "Optional maximum number of results." },
                ["orderBy"] = new { description = "Optional field or array of fields to order by." },
                ["orderDirection"] = new { type = "string", description = "asc or desc." },
                ["returnFields"] = new { description = "Optional field or array of fields to return." }
            };

            if (includeRowId)
            {
                properties["rowId"] = new { type = "integer", description = "Primary key value." };
            }

            return new
            {
                type = "object",
                properties,
                required = includeRowId
                    ? new[] { "databaseName", "tableName", "rowId" }
                    : new[] { "databaseName", "tableName" }
            };
        }

        private static object DeleteSchema(bool includeRowId)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                ["databaseName"] = new { type = "string", description = "Configured RestDb database name." },
                ["tableName"] = new { type = "string", description = "Table name." },
                ["filters"] = new { type = "object", description = "Optional querystring equality filters.", additionalProperties = true }
            };

            if (includeRowId)
            {
                properties["rowId"] = new { type = "integer", description = "Primary key value." };
            }

            return new
            {
                type = "object",
                properties,
                required = includeRowId
                    ? new[] { "databaseName", "tableName", "rowId" }
                    : new[] { "databaseName", "tableName" }
            };
        }

        private static string BuildSelectQuery(JsonElement? args, bool includeDebug = false)
        {
            List<string> pairs = new List<string>();
            AppendFilterPairs(pairs, args);
            AppendIntPair(pairs, "_index", GetOptionalInt(args, "indexStart"));
            AppendIntPair(pairs, "_max", GetOptionalInt(args, "maxResults"));
            AppendStringPair(pairs, "_order_by", GetOptionalCsv(args, "orderBy"));
            AppendStringPair(pairs, "_order", GetOptionalString(args, "orderDirection"));
            AppendStringPair(pairs, "_return", GetOptionalCsv(args, "returnFields"));

            if (includeDebug && GetOptionalBool(args, "debug"))
            {
                pairs.Add("_debug=true");
            }

            return ToQueryString(pairs);
        }

        private static string BuildFilterQuery(JsonElement? args)
        {
            List<string> pairs = new List<string>();
            AppendFilterPairs(pairs, args);
            return ToQueryString(pairs);
        }

        private static string BuildContextQuery(JsonElement? args, bool hasExistingQuery = false)
        {
            return GetOptionalBool(args, "includeContext")
                ? (hasExistingQuery ? "&" : "?") + "_context=true"
                : String.Empty;
        }

        private static void AppendFilterPairs(List<string> pairs, JsonElement? args)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (!TryGetProperty(args, "filters", out JsonElement filters)) return;
            if (filters.ValueKind != JsonValueKind.Object) return;

            foreach (JsonProperty property in filters.EnumerateObject())
            {
                pairs.Add(RestMcpRestProxy.Escape(property.Name) + "=" + RestMcpRestProxy.Escape(GetQueryValue(property.Value)));
            }
        }

        private static void AppendIntPair(List<string> pairs, string name, int? value)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (!value.HasValue) return;

            pairs.Add(RestMcpRestProxy.Escape(name) + "=" + value.Value);
        }

        private static void AppendStringPair(List<string> pairs, string name, string? value)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrWhiteSpace(value)) return;

            pairs.Add(RestMcpRestProxy.Escape(name) + "=" + RestMcpRestProxy.Escape(value));
        }

        private static string ToQueryString(List<string> pairs)
        {
            if (pairs == null || pairs.Count < 1) return String.Empty;
            return "?" + String.Join("&", pairs);
        }

        private static string GetQueryValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? String.Empty;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.ToString();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return String.Empty;
                default:
                    return element.GetRawText();
            }
        }

        private static string RequireString(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element))
            {
                throw new ArgumentException(propertyName + " is required.");
            }

            string? value = element.GetString();
            if (String.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(propertyName + " is required.");
            }

            return value;
        }

        private static int RequireInt(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element))
            {
                throw new ArgumentException(propertyName + " is required.");
            }

            return element.GetInt32();
        }

        private static string? GetOptionalString(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element)) return null;
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined) return null;
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }

        private static int? GetOptionalInt(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element)) return null;
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value)) return value;
            return null;
        }

        private static bool GetOptionalBool(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element)) return false;
            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;
            if (element.ValueKind == JsonValueKind.String && Boolean.TryParse(element.GetString(), out bool value)) return value;
            return false;
        }

        private static string? GetOptionalCsv(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element)) return null;

            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                return String.Join(",", element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()));
            }

            return element.ToString();
        }

        private static object? GetOptionalJsonObject(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element)) return null;
            using JsonDocument document = JsonDocument.Parse(element.GetRawText());
            return document.RootElement.Clone();
        }

        private static string RequireRawJsonProperty(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement element))
            {
                throw new ArgumentException(propertyName + " is required.");
            }

            return element.GetRawText();
        }

        private static bool TryGetProperty(JsonElement? args, string propertyName, out JsonElement element)
        {
            element = default;
            if (!args.HasValue) return false;
            if (args.Value.ValueKind != JsonValueKind.Object) return false;
            return args.Value.TryGetProperty(propertyName, out element);
        }
    }
}
