namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RestDb;
using Touchstone.Core;

public static class RestDbTestSuites
{
    private sealed class QueryBuilderCaseDefinition
    {
        public string CaseId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public Action<string> Execute { get; init; } = _ => { };
    }

    private sealed class LiveApiCaseDefinition
    {
        public string CaseId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public Func<Task> ExecuteAsync { get; init; } = static () => Task.CompletedTask;
    }

    private static readonly IReadOnlyList<QueryBuilderCaseDefinition> QueryBuilderCases =
        new List<QueryBuilderCaseDefinition>
        {
            new() { CaseId = "GetDatabaseListTables", DisplayName = "GET /{db} uses list-tables query", Execute = ProviderQueryBuilderAssertions.GetDatabasePathUsesListTablesQuery },
            new() { CaseId = "DescribePaths", DisplayName = "Describe paths use describe-table query", Execute = ProviderQueryBuilderAssertions.DescribePathsUseDescribeTableQuery },
            new() { CaseId = "GetDatabaseDescribe", DisplayName = "GET /{db}?_describe uses list-then-describe queries", Execute = ProviderQueryBuilderAssertions.GetDatabaseDescribePathUsesListThenDescribeQueries },
            new() { CaseId = "PostTableCreate", DisplayName = "POST /{db} builds provider-specific create-table DDL", Execute = ProviderQueryBuilderAssertions.PostTableCreatePathBuildsProviderSpecificCreateTableDdl },
            new() { CaseId = "GetTableSelectPaged", DisplayName = "GET /{db}/{table} builds paged filtered select", Execute = ProviderQueryBuilderAssertions.GetTableSelectPathBuildsPaginatedFilteredSelect },
            new() { CaseId = "GetTableSelectDefault", DisplayName = "GET /{db}/{table} builds default unfiltered select", Execute = ProviderQueryBuilderAssertions.GetTableSelectPathBuildsDefaultUnfilteredSelect },
            new() { CaseId = "GetTableById", DisplayName = "GET /{db}/{table}/{id} builds primary-key select", Execute = ProviderQueryBuilderAssertions.GetTableByIdPathBuildsPrimaryKeySelect },
            new() { CaseId = "GetTableByIdWithFilters", DisplayName = "GET /{db}/{table}/{id} combines id and querystring filters", Execute = ProviderQueryBuilderAssertions.GetTableByIdPathBuildsCombinedPrimaryKeyAndQuerystringFilters },
            new() { CaseId = "PutSearch", DisplayName = "PUT /{db}/{table} builds expression search select", Execute = ProviderQueryBuilderAssertions.PutSearchPathBuildsExpressionSelect },
            new() { CaseId = "PutSearchWithFilters", DisplayName = "PUT /{db}/{table} combines expression and querystring filters", Execute = ProviderQueryBuilderAssertions.PutSearchPathBuildsCombinedExpressionAndQuerystringFilters },
            new() { CaseId = "PostTableInsert", DisplayName = "POST /{db}/{table} builds insert and readback", Execute = ProviderQueryBuilderAssertions.PostTableInsertPathBuildsInsertAndReadback },
            new() { CaseId = "PostTableInsertTypedStrings", DisplayName = "POST /{db}/{table} coerces typed string values", Execute = ProviderQueryBuilderAssertions.PostTableInsertPathCoercesTypedStringValues },
            new() { CaseId = "PostTableInsertMultiple", DisplayName = "POST /{db}/{table}?_multiple builds transactional multi-insert", Execute = ProviderQueryBuilderAssertions.PostTableInsertMultiplePathBuildsTransactionalMultiInsert },
            new() { CaseId = "PutUpdateById", DisplayName = "PUT /{db}/{table}/{id} builds update", Execute = ProviderQueryBuilderAssertions.PutUpdateByIdPathBuildsUpdate },
            new() { CaseId = "TypedFilters", DisplayName = "Typed filters are coerced using column schema", Execute = ProviderQueryBuilderAssertions.TypedFiltersAreCoercedUsingColumnSchema },
            new() { CaseId = "DeleteById", DisplayName = "DELETE /{db}/{table}/{id} builds keyed delete", Execute = ProviderQueryBuilderAssertions.DeletePathsBuildDeleteQuery },
            new() { CaseId = "DeleteByIdWithFilters", DisplayName = "DELETE /{db}/{table}/{id} combines id and querystring filters", Execute = ProviderQueryBuilderAssertions.DeletePathBuildsCombinedPrimaryKeyAndQuerystringFilteredDelete },
            new() { CaseId = "DeleteUnfiltered", DisplayName = "DELETE /{db}/{table} builds unfiltered delete", Execute = ProviderQueryBuilderAssertions.DeletePathBuildsUnfilteredDeleteQuery },
            new() { CaseId = "DeleteFiltered", DisplayName = "DELETE /{db}/{table} builds querystring-filtered delete", Execute = ProviderQueryBuilderAssertions.DeletePathBuildsQuerystringFilteredDeleteQuery },
            new() { CaseId = "DeleteTruncate", DisplayName = "DELETE /{db}/{table}?_truncate builds provider-specific clear query", Execute = ProviderQueryBuilderAssertions.ClearPathBuildsProviderSpecificClearQuery },
            new() { CaseId = "DeleteDrop", DisplayName = "DELETE /{db}/{table}?_drop builds drop query", Execute = ProviderQueryBuilderAssertions.DropPathBuildsDropQuery },
            new() { CaseId = "RawQuery", DisplayName = "POST /{db}?raw preserves raw query passthrough", Execute = ProviderQueryBuilderAssertions.RawQueryPathPassthroughIsPreserved }
        };

    private static readonly IReadOnlyList<LiveApiCaseDefinition> LiveApiCases =
        new List<LiveApiCaseDefinition>
        {
            new() { CaseId = "Root", DisplayName = "GET / returns the root status page", ExecuteAsync = LiveApiAssertions.RootPathReturnsStatusPageAsync },
            new() { CaseId = "Options", DisplayName = "OPTIONS advertises supported HTTP methods", ExecuteAsync = LiveApiAssertions.OptionsPathAdvertisesAllowedMethodsAsync },
            new() { CaseId = "Preflight", DisplayName = "OPTIONS preflight succeeds against protected API routes without authentication", ExecuteAsync = LiveApiAssertions.PreflightPathAllowsProtectedCrossOriginRequestWithoutAuthenticationAsync },
            new() { CaseId = "HeaderAuth", DisplayName = "Configured API key headers authorize protected routes", ExecuteAsync = LiveApiAssertions.ApiKeyHeaderAuthAllowsProtectedRequestsAsync },
            new() { CaseId = "BearerAuth", DisplayName = "Bearer tokens authorize protected routes", ExecuteAsync = LiveApiAssertions.BearerTokenAuthAllowsProtectedRequestsAsync },
            new() { CaseId = "GetDatabases", DisplayName = "GET /_databases returns configured database names", ExecuteAsync = LiveApiAssertions.GetDatabasesPathReturnsConfiguredDatabaseNamesAsync },
            new() { CaseId = "GetDatabaseClients", DisplayName = "GET /_databaseclients returns active database clients", ExecuteAsync = LiveApiAssertions.GetDatabaseClientsPathReturnsActiveDatabaseClientsAsync },
            new() { CaseId = "GetDatabase", DisplayName = "GET /{db} lists tables over HTTP", ExecuteAsync = LiveApiAssertions.GetDatabasePathListsTablesAsync },
            new() { CaseId = "GetDatabaseWithContext", DisplayName = "GET /{db}?_context returns database and table context over HTTP", ExecuteAsync = LiveApiAssertions.GetDatabasePathReturnsContextWhenRequestedAsync },
            new() { CaseId = "GetDatabaseDescribe", DisplayName = "GET /{db}?_describe returns described tables over HTTP", ExecuteAsync = LiveApiAssertions.GetDatabaseDescribePathReturnsDescribedTablesAsync },
            new() { CaseId = "GetDatabaseDescribeWithContext", DisplayName = "GET /{db}?_describe&_context returns described tables plus context over HTTP", ExecuteAsync = LiveApiAssertions.GetDatabaseDescribePathReturnsContextWhenRequestedAsync },
            new() { CaseId = "GetTableDescribe", DisplayName = "GET /{db}/{table}?_describe returns table metadata over HTTP", ExecuteAsync = LiveApiAssertions.GetTableDescribePathReturnsTableMetadataAsync },
            new() { CaseId = "GetTableDescribeWithContext", DisplayName = "GET /{db}/{table}?_describe&_context returns table metadata plus context over HTTP", ExecuteAsync = LiveApiAssertions.GetTableDescribePathReturnsContextWhenRequestedAsync },
            new() { CaseId = "PostTableCreate", DisplayName = "POST /{db} creates tables over HTTP", ExecuteAsync = LiveApiAssertions.PostTableCreatePathCreatesTableAsync },
            new() { CaseId = "GetTableSelectDefault", DisplayName = "GET /{db}/{table} returns unpaged row collections over HTTP", ExecuteAsync = LiveApiAssertions.GetTableSelectPathReturnsDefaultUnpagedResultsAsync },
            new() { CaseId = "GetTableSelectPaged", DisplayName = "GET /{db}/{table} returns filtered paged projected rows over HTTP", ExecuteAsync = LiveApiAssertions.GetTableSelectPathReturnsFilteredPagedProjectedResultsAsync },
            new() { CaseId = "GetTableById", DisplayName = "GET /{db}/{table}/{id} returns the keyed row over HTTP", ExecuteAsync = LiveApiAssertions.GetTableByIdPathReturnsMatchingRowAsync },
            new() { CaseId = "GetTableByIdWithFilters", DisplayName = "GET /{db}/{table}/{id} combines id and querystring filters over HTTP", ExecuteAsync = LiveApiAssertions.GetTableByIdPathCombinesIdAndQuerystringFiltersAsync },
            new() { CaseId = "PutSearch", DisplayName = "PUT /{db}/{table} evaluates expression searches over HTTP", ExecuteAsync = LiveApiAssertions.PutSearchPathReturnsExpressionMatchesAsync },
            new() { CaseId = "PutSearchWithFilters", DisplayName = "PUT /{db}/{table} combines expression and querystring filters over HTTP", ExecuteAsync = LiveApiAssertions.PutSearchPathCombinesExpressionAndQuerystringFiltersAsync },
            new() { CaseId = "PostTableInsert", DisplayName = "POST /{db}/{table} inserts and returns a row over HTTP", ExecuteAsync = LiveApiAssertions.PostTableInsertPathReturnsInsertedRowAsync },
            new() { CaseId = "PostTableInsertTypedStrings", DisplayName = "POST /{db}/{table} coerces typed string values over HTTP", ExecuteAsync = LiveApiAssertions.PostTableInsertPathCoercesTypedStringValuesAsync },
            new() { CaseId = "PostTableInsertMultiple", DisplayName = "POST /{db}/{table}?_multiple inserts multiple rows over HTTP", ExecuteAsync = LiveApiAssertions.PostTableInsertMultiplePathCreatesAllRowsAsync },
            new() { CaseId = "PutUpdateById", DisplayName = "PUT /{db}/{table}/{id} updates the keyed row over HTTP", ExecuteAsync = LiveApiAssertions.PutUpdateByIdPathUpdatesMatchingRowAsync },
            new() { CaseId = "DeleteById", DisplayName = "DELETE /{db}/{table}/{id} removes the keyed row over HTTP", ExecuteAsync = LiveApiAssertions.DeleteByIdPathRemovesMatchingRowAsync },
            new() { CaseId = "DeleteByIdWithFilters", DisplayName = "DELETE /{db}/{table}/{id} combines id and querystring filters over HTTP", ExecuteAsync = LiveApiAssertions.DeleteByIdPathCombinesIdAndQuerystringFiltersAsync },
            new() { CaseId = "DeleteUnfiltered", DisplayName = "DELETE /{db}/{table} removes all rows over HTTP", ExecuteAsync = LiveApiAssertions.DeletePathRemovesAllRowsAsync },
            new() { CaseId = "DeleteFiltered", DisplayName = "DELETE /{db}/{table} applies querystring filters over HTTP", ExecuteAsync = LiveApiAssertions.DeletePathRemovesQuerystringMatchesAsync },
            new() { CaseId = "DeleteTruncate", DisplayName = "DELETE /{db}/{table}?_truncate clears tables over HTTP", ExecuteAsync = LiveApiAssertions.DeleteTruncatePathClearsTableAsync },
            new() { CaseId = "DeleteDrop", DisplayName = "DELETE /{db}/{table}?_drop removes tables over HTTP", ExecuteAsync = LiveApiAssertions.DeleteDropPathRemovesTableAsync },
            new() { CaseId = "RawQuery", DisplayName = "POST /{db}?raw executes provider SQL over HTTP", ExecuteAsync = LiveApiAssertions.PostRawQueryPathExecutesAgainstSelectedProviderAsync },
            new() { CaseId = "AuthMissingCredentials", DisplayName = "Protected routes reject requests without credentials (401)", ExecuteAsync = LiveApiAssertions.ProtectedRequestWithoutCredentialsIsRejectedAsync },
            new() { CaseId = "AuthInvalidApiKey", DisplayName = "Protected routes reject an unknown API key (401)", ExecuteAsync = LiveApiAssertions.ProtectedRequestWithInvalidApiKeyIsRejectedAsync },
            new() { CaseId = "GetDatabaseUnknown", DisplayName = "GET /{db} returns 404 for an unknown database", ExecuteAsync = LiveApiAssertions.GetDatabasePathReturnsNotFoundForUnknownDatabaseAsync },
            new() { CaseId = "GetTableUnknown", DisplayName = "GET /{db}/{table} returns 404 for an unknown table", ExecuteAsync = LiveApiAssertions.GetTableSelectPathReturnsNotFoundForUnknownTableAsync },
            new() { CaseId = "PostInsertUnknownTable", DisplayName = "POST /{db}/{table} returns 404 when inserting into an unknown table", ExecuteAsync = LiveApiAssertions.PostTableInsertPathReturnsNotFoundForUnknownTableAsync },
            new() { CaseId = "PostInsertEmptyBody", DisplayName = "POST /{db}/{table} returns 400 for an empty insert body", ExecuteAsync = LiveApiAssertions.PostTableInsertPathRejectsEmptyBodyAsync },
            new() { CaseId = "PostCreateEmptyBody", DisplayName = "POST /{db} returns 400 for an empty create-table body", ExecuteAsync = LiveApiAssertions.PostTableCreatePathRejectsEmptyBodyAsync },
            new() { CaseId = "PostCreateBadPrimaryKey", DisplayName = "POST /{db} returns 400 when the primary key is absent from the column list", ExecuteAsync = LiveApiAssertions.PostTableCreatePathRejectsPrimaryKeyNotInColumnListAsync }
        };

    public static IReadOnlyList<TestSuiteDescriptor> All
    {
        get
        {
            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>
            {
                SerializationSuite(),
                McpBridgeSuite(),
                McpTransportSuite()
            };

            foreach (string providerName in TestData.ProviderNames)
            {
                suites.Add(QueryBuilderSuite(providerName));
            }

            suites.Add(LiveApiSuite());
            return suites;
        }
    }

    private static TestSuiteDescriptor SerializationSuite()
    {
        const string suiteId = "Serialization";
        return new TestSuiteDescriptor(
            suiteId: suiteId,
            displayName: "Serialization Compatibility",
            cases: new List<TestCaseDescriptor>
            {
                SyncCase(suiteId, "TableRoundTrip", "Table payload roundtrips with expected property names", SerializationAssertions.TablePayloadRoundtripsWithExpectedPropertyNames),
                SyncCase(suiteId, "ExpressionPayload", "Search expression payload deserializes nested expression tree", SerializationAssertions.SearchExpressionPayloadDeserializesNestedExpressionTree)
            });
    }

    private static TestSuiteDescriptor QueryBuilderSuite(string providerName)
    {
        string suiteId = "QueryBuilder." + providerName;
        List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

        foreach (QueryBuilderCaseDefinition definition in QueryBuilderCases)
        {
            string currentProvider = providerName;
            cases.Add(new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: definition.CaseId,
                displayName: currentProvider + ": " + definition.DisplayName,
                executeAsync: _ =>
                {
                    definition.Execute(currentProvider);
                    return Task.CompletedTask;
                }));
        }

        return new TestSuiteDescriptor(
            suiteId: suiteId,
            displayName: providerName + " Query Builder",
            cases: cases);
    }

    private static TestSuiteDescriptor McpBridgeSuite()
    {
        const string suiteId = "McpBridge";
        return new TestSuiteDescriptor(
            suiteId: suiteId,
            displayName: "MCP HTTP Bridge",
            cases: new List<TestCaseDescriptor>
            {
                new(
                    suiteId: suiteId,
                    caseId: "StreamableHttpPostContract",
                    displayName: "Streamable HTTP accepts standard JSON content types and returns 202 for notifications",
                    executeAsync: _ => McpBridgeAssertions.StreamableHttpAcceptsStandardJsonContentTypeAndListsToolsAsync()),
                new(
                    suiteId: suiteId,
                    caseId: "StreamableHttpSsePrelude",
                    displayName: "Streamable HTTP sends an immediate SSE prelude on /mcp",
                    executeAsync: _ => McpBridgeAssertions.StreamableHttpSendsImmediateSsePreludeAsync()),
                McpCase(suiteId, "SseRelaysNotifications", "GET /mcp relays server notifications for the session", McpBridgeAssertions.SseRelaysServerNotificationsAsync),
                McpCase(suiteId, "StatelessToolsList", "Stateless 2026-07-28 tools/list carries resultType, ttlMs, and cacheScope without a session", McpBridgeAssertions.StatelessToolsListCarriesResultTypeAsync),
                McpCase(suiteId, "StatelessServerDiscover", "Stateless server/discover advertises the 2026-07-28 revision", McpBridgeAssertions.StatelessServerDiscoverAdvertisesStatelessRevisionAsync),
                McpCase(suiteId, "StatelessToolsCall", "Stateless tools/call invokes a registered RestDb tool", McpBridgeAssertions.StatelessToolsCallInvokesRegisteredToolAsync),
                McpCase(suiteId, "HandshakeToolsCall", "Session tools/call invokes a registered RestDb tool without stateless fields", McpBridgeAssertions.HandshakeToolsCallInvokesRegisteredToolAsync),
                McpCase(suiteId, "InitializeStatelessVersion", "initialize requesting 2026-07-28 negotiates the newest handshake revision", McpBridgeAssertions.InitializeNegotiatesNewestHandshakeRevisionForStatelessVersionAsync),
                McpCase(suiteId, "DeleteTerminatesSession", "DELETE /mcp terminates the session", McpBridgeAssertions.DeleteTerminatesSessionAsync),
                McpCase(suiteId, "UnknownTool", "tools/call rejects an unknown tool (-32602)", McpBridgeAssertions.ToolsCallRejectsUnknownToolAsync),
                McpCase(suiteId, "MissingRequiredArgument", "tools/call rejects a missing required argument (-32602)", McpBridgeAssertions.ToolsCallRejectsMissingRequiredArgumentAsync),
                McpCase(suiteId, "HandlerFailure", "tools/call surfaces a failing tool handler as an error", McpBridgeAssertions.ToolsCallSurfacesHandlerFailureAsync),
                McpCase(suiteId, "DownstreamFailureIsError", "tools/call flags a failed (404) RestDb response with isError", McpBridgeAssertions.ToolsCallFlagsFailedDownstreamResponseAsErrorAsync),
                McpCase(suiteId, "DownstreamSuccessNotError", "tools/call leaves a successful RestDb response unflagged", McpBridgeAssertions.ToolsCallLeavesSuccessfulDownstreamResponseUnflaggedAsync),
                McpCase(suiteId, "MismatchedMethodHeader", "Stateless request rejects an Mcp-Method header that does not match the body (400)", McpBridgeAssertions.StatelessRequestRejectsMismatchedMethodHeaderAsync),
                McpCase(suiteId, "UnknownProtocolVersion", "initialize rejects an unknown protocol version (-32602)", McpBridgeAssertions.InitializeRejectsUnknownProtocolVersionAsync),
                McpCase(suiteId, "MalformedJson", "Malformed JSON returns a parse error (-32700)", McpBridgeAssertions.MalformedJsonReturnsParseErrorAsync),
                McpCase(suiteId, "SseInvalidSession", "GET /mcp rejects a missing or unknown session (400)", McpBridgeAssertions.SseRejectsMissingOrUnknownSessionAsync),
                McpCase(suiteId, "DeleteInvalidSession", "DELETE /mcp rejects a missing (400) or unknown (404) session", McpBridgeAssertions.DeleteRejectsMissingOrUnknownSessionAsync),
                McpCase(suiteId, "UnsupportedHttpMethod", "PUT /mcp returns 405", McpBridgeAssertions.UnsupportedHttpMethodReturnsMethodNotAllowedAsync),
                McpCase(suiteId, "UnknownPath", "Unknown bridge path returns 404", McpBridgeAssertions.UnknownPathReturnsNotFoundAsync),
                McpCase(suiteId, "HandshakeToolsListExact", "Session tools/list publishes only the RestDb tools (no Voltaic demo tools)", McpBridgeAssertions.HandshakeToolsListPublishesOnlyRestDbToolsAsync),
                McpCase(suiteId, "StatelessToolsListExact", "Stateless tools/list publishes only the RestDb tools (no Voltaic demo tools)", McpBridgeAssertions.StatelessToolsListPublishesOnlyRestDbToolsAsync),
                McpCase(suiteId, "HandshakePingEmpty", "Session ping returns an empty object instead of \"pong\"", McpBridgeAssertions.HandshakePingReturnsEmptyResultAsync),
                McpCase(suiteId, "StatelessPingComplete", "Stateless ping returns only resultType: complete", McpBridgeAssertions.StatelessPingReturnsCompleteResultAsync),
                McpCase(suiteId, "RemovedDemoTools", "tools/call rejects the removed Voltaic demo tools (-32602)", McpBridgeAssertions.ToolsCallRejectsRemovedVoltaicDemoToolsAsync),
                McpCase(suiteId, "BareDemoMethods", "Bare echo/getTime/getSessions/getClients calls return method not found (-32601)", McpBridgeAssertions.BareVoltaicDemoMethodsReturnMethodNotFoundAsync),
                McpCase(suiteId, "DirectRestDbMethod", "A RestDb tool stays callable as a direct JSON-RPC method over HTTP", McpBridgeAssertions.RestDbToolRemainsCallableAsDirectMethodAsync)
            });
    }

    private static TestSuiteDescriptor McpTransportSuite()
    {
        const string suiteId = "McpTransports";
        return new TestSuiteDescriptor(
            suiteId: suiteId,
            displayName: "MCP TCP and WebSocket Transports",
            cases: new List<TestCaseDescriptor>
            {
                McpCase(suiteId, "TcpToolsListAndCall", "TCP tools/list publishes and tools/call invokes RestDb tools", McpTransportAssertions.TcpListsAndCallsRegisteredToolsAsync),
                McpCase(suiteId, "TcpDirectMethod", "TCP invokes a RestDb tool as a direct JSON-RPC method", McpTransportAssertions.TcpInvokesToolAsDirectMethodAsync),
                McpCase(suiteId, "TcpInvalidCalls", "TCP rejects unknown tools, missing arguments, unknown methods, and failing handlers", McpTransportAssertions.TcpRejectsInvalidToolCallsAsync),
                McpCase(suiteId, "TcpDownstreamErrors", "TCP tools/call flags failed RestDb responses with isError; direct methods return them raw", McpTransportAssertions.TcpFlagsFailedDownstreamResponsesAsync),
                McpCase(suiteId, "WebSocketToolsListAndCall", "WebSocket tools/list publishes and tools/call invokes RestDb tools", McpTransportAssertions.WebSocketListsAndCallsRegisteredToolsAsync),
                McpCase(suiteId, "WebSocketDirectMethod", "WebSocket invokes a RestDb tool as a direct JSON-RPC method", McpTransportAssertions.WebSocketInvokesToolAsDirectMethodAsync),
                McpCase(suiteId, "WebSocketInvalidCalls", "WebSocket rejects unknown tools, missing arguments, unknown methods, and failing handlers", McpTransportAssertions.WebSocketRejectsInvalidToolCallsAsync),
                McpCase(suiteId, "WebSocketDownstreamErrors", "WebSocket tools/call flags failed RestDb responses with isError; direct methods return them raw", McpTransportAssertions.WebSocketFlagsFailedDownstreamResponsesAsync),
                McpCase(suiteId, "TcpToolsListExact", "TCP tools/list publishes only the RestDb tools (no Voltaic demo tools)", McpTransportAssertions.TcpListsOnlyRestDbToolsAsync),
                McpCase(suiteId, "WebSocketToolsListExact", "WebSocket tools/list publishes only the RestDb tools (no Voltaic demo tools)", McpTransportAssertions.WebSocketListsOnlyRestDbToolsAsync),
                McpCase(suiteId, "TcpPingEmpty", "TCP ping returns an empty object instead of \"pong\"", McpTransportAssertions.TcpPingReturnsEmptyResultAsync),
                McpCase(suiteId, "WebSocketPingEmpty", "WebSocket ping returns an empty object instead of \"pong\"", McpTransportAssertions.WebSocketPingReturnsEmptyResultAsync),
                McpCase(suiteId, "TcpRemovedDemoTools", "TCP rejects the removed Voltaic demo tools via tools/call (-32602) and bare calls (-32601)", McpTransportAssertions.TcpRejectsRemovedVoltaicDemoToolsAsync),
                McpCase(suiteId, "WebSocketRemovedDemoTools", "WebSocket rejects the removed Voltaic demo tools via tools/call (-32602) and bare calls (-32601)", McpTransportAssertions.WebSocketRejectsRemovedVoltaicDemoToolsAsync),
                McpCase(suiteId, "CatalogToolsListExact", "tools/list publishes exactly the production RestDb tool catalog", McpTransportAssertions.TcpCatalogListsExactlyRestDbToolsAsync),
                McpCase(suiteId, "CatalogRejectsNonStringTableContext", "restdb_update_database_context rejects a non-string table context (additionalProperties schema, -32602)", McpTransportAssertions.TcpCatalogRejectsNonStringTableContextAsync),
                McpCase(suiteId, "CatalogAcceptsStringTableContext", "restdb_update_database_context accepts string table contexts", McpTransportAssertions.TcpCatalogAcceptsStringTableContextAsync),
                McpCase(suiteId, "CatalogAcceptsArbitraryFilters", "restdb_enumerate_table_records accepts arbitrary filters (additionalProperties: true)", McpTransportAssertions.TcpCatalogAcceptsArbitraryFiltersAsync)
            });
    }

    private static TestCaseDescriptor McpCase(string suiteId, string caseId, string displayName, Func<Task> execute)
    {
        return new TestCaseDescriptor(
            suiteId: suiteId,
            caseId: caseId,
            displayName: displayName,
            executeAsync: _ => execute());
    }

    private static TestSuiteDescriptor LiveApiSuite()
    {
        DbTypeEnum provider = RestDbTestRuntime.Configuration.DatabaseType;
        string suiteId = "LiveApi." + provider;
        List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

        foreach (LiveApiCaseDefinition definition in LiveApiCases)
        {
            string providerName = provider.ToString();
            cases.Add(new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: definition.CaseId,
                displayName: providerName + " live API: " + definition.DisplayName,
                executeAsync: _ => definition.ExecuteAsync()));
        }

        return new TestSuiteDescriptor(
            suiteId: suiteId,
            displayName: provider + " Live API",
            cases: cases);
    }

    private static TestCaseDescriptor SyncCase(string suiteId, string caseId, string displayName, Action action)
    {
        return new TestCaseDescriptor(
            suiteId: suiteId,
            caseId: caseId,
            displayName: displayName,
            executeAsync: _ =>
            {
                action();
                return Task.CompletedTask;
            });
    }
}
