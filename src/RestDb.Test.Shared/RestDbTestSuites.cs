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
            new() { CaseId = "RawQuery", DisplayName = "POST /{db}?raw executes provider SQL over HTTP", ExecuteAsync = LiveApiAssertions.PostRawQueryPathExecutesAgainstSelectedProviderAsync }
        };

    public static IReadOnlyList<TestSuiteDescriptor> All
    {
        get
        {
            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>
            {
                SerializationSuite(),
                McpBridgeSuite()
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
                    executeAsync: _ => McpBridgeAssertions.StreamableHttpSendsImmediateSsePreludeAsync())
            });
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
