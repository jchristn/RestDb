namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RestDb;

internal static class LiveApiAssertions
{
    private const string SearchExpressionJson =
        "{" +
        "\"Left\":{" +
        "\"Left\":\"age\"," +
        "\"Operator\":\"In\"," +
        "\"Right\":[18,19]" +
        "}," +
        "\"Operator\":\"Or\"," +
        "\"Right\":{" +
        "\"Left\":{\"Left\":\"created\",\"Operator\":\"IsNotNull\"}," +
        "\"Operator\":\"And\"," +
        "\"Right\":{\"Left\":\"last_name\",\"Operator\":\"StartsWith\",\"Right\":\"Chr\"}" +
        "}" +
        "}";

    private const string CombinedSearchExpressionJson =
        "{" +
        "\"Left\":{\"Left\":\"age\",\"Operator\":\"GreaterThanOrEqualTo\",\"Right\":18}," +
        "\"Operator\":\"And\"," +
        "\"Right\":{\"Left\":\"first_name\",\"Operator\":\"Equals\",\"Right\":\"joel\"}" +
        "}";

    public static async Task RootPathReturnsStatusPageAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);

        using HttpResponseMessage response = await session.Client.GetAsync("/").ConfigureAwait(false);
        byte[] bodyBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        string body = Encoding.UTF8.GetString(bodyBytes);

        AssertStatus(response, HttpStatusCode.OK, body);
        TestAssert.Contains("RestDb is running.", body, StringComparison.Ordinal, body);
    }

    public static async Task OptionsPathAdvertisesAllowedMethodsAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/");
        using HttpResponseMessage response = await session.Client.SendAsync(request).ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK);
        string headerDump = response.Headers.ToString();
        if (response.Headers.TryGetValues("Allow", out IEnumerable<string>? values))
        {
            headerDump = string.Join(",", values);
        }

        if (!string.IsNullOrWhiteSpace(headerDump))
        {
            TestAssert.Contains("GET", headerDump, StringComparison.Ordinal, headerDump);
            TestAssert.Contains("PUT", headerDump, StringComparison.Ordinal, headerDump);
            TestAssert.Contains("POST", headerDump, StringComparison.Ordinal, headerDump);
            TestAssert.Contains("DELETE", headerDump, StringComparison.Ordinal, headerDump);
            TestAssert.Contains("OPTIONS", headerDump, StringComparison.Ordinal, headerDump);
        }
    }

    public static async Task PreflightPathAllowsProtectedCrossOriginRequestWithoutAuthenticationAsync()
    {
        await using RestDbLiveApiSession session = await RestDbLiveApiSession.StartAsync(
            RestDbTestRuntime.Configuration,
            requireAuthentication: true).ConfigureAwait(false);
        using HttpClient unauthenticatedClient = new HttpClient
        {
            BaseAddress = session.BaseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/_databases");
        request.Headers.Add("Origin", "http://localhost:18080");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", session.ApiKeyHeader + ", x-dashboard-trace");

        using HttpResponseMessage response = await unauthenticatedClient.SendAsync(request).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK, body);
        TestAssert.DoesNotContain("Login failed", body, StringComparison.OrdinalIgnoreCase, body);
        TestAssert.Equal("*", GetSingleHeaderValue(response, "Access-Control-Allow-Origin"), response.Headers.ToString());

        string allowMethods = GetSingleHeaderValue(response, "Access-Control-Allow-Methods");
        TestAssert.Contains("GET", allowMethods, StringComparison.Ordinal, allowMethods);
        TestAssert.Contains("OPTIONS", allowMethods, StringComparison.Ordinal, allowMethods);

        string allowHeaders = GetSingleHeaderValue(response, "Access-Control-Allow-Headers");
        TestAssert.Contains(session.ApiKeyHeader, allowHeaders, StringComparison.OrdinalIgnoreCase, allowHeaders);
        TestAssert.Contains("x-dashboard-trace", allowHeaders, StringComparison.OrdinalIgnoreCase, allowHeaders);

        string exposeHeaders = GetSingleHeaderValue(response, "Access-Control-Expose-Headers");
        TestAssert.Contains("x-expression", exposeHeaders, StringComparison.OrdinalIgnoreCase, exposeHeaders);
        TestAssert.Equal("86400", GetSingleHeaderValue(response, "Access-Control-Max-Age"), response.Headers.ToString());
        TestAssert.Contains("GET", GetSingleHeaderValue(response, "Allow"), StringComparison.Ordinal, response.Headers.ToString());
        TestAssert.Contains("OPTIONS", GetSingleHeaderValue(response, "Allow"), StringComparison.Ordinal, response.Headers.ToString());
    }

    public static async Task ApiKeyHeaderAuthAllowsProtectedRequestsAsync()
    {
        await using RestDbLiveApiSession session = await RestDbLiveApiSession.StartAsync(
            RestDbTestRuntime.Configuration,
            requireAuthentication: true).ConfigureAwait(false);

        using HttpResponseMessage response = await session.Client.GetAsync("/_databases").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK, body);
        using JsonDocument json = ParseJson(body);
        TestAssert.Equal(JsonValueKind.Array, json.RootElement.ValueKind, body);
        TestAssert.True(
            json.RootElement.EnumerateArray().Any(e => string.Equals(e.GetString(), session.DatabaseName, StringComparison.OrdinalIgnoreCase)),
            body);
    }

    public static async Task BearerTokenAuthAllowsProtectedRequestsAsync()
    {
        await using RestDbLiveApiSession session = await RestDbLiveApiSession.StartAsync(
            RestDbTestRuntime.Configuration,
            requireAuthentication: true).ConfigureAwait(false);
        using HttpClient bearerClient = new HttpClient
        {
            BaseAddress = session.BaseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.ApiKey);

        using HttpResponseMessage response = await bearerClient.GetAsync("/_databases").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK, body);
        using JsonDocument json = ParseJson(body);
        TestAssert.Equal(JsonValueKind.Array, json.RootElement.ValueKind, body);
        TestAssert.True(
            json.RootElement.EnumerateArray().Any(e => string.Equals(e.GetString(), session.DatabaseName, StringComparison.OrdinalIgnoreCase)),
            body);
    }

    public static async Task GetDatabasesPathReturnsConfiguredDatabaseNamesAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);

        using HttpResponseMessage response = await session.Client.GetAsync("/_databases").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK, body);
        using JsonDocument json = ParseJson(body);
        TestAssert.Equal(JsonValueKind.Array, json.RootElement.ValueKind, body);
        TestAssert.True(
            json.RootElement.EnumerateArray().Any(e => string.Equals(e.GetString(), session.DatabaseName, StringComparison.OrdinalIgnoreCase)),
            body);
    }

    public static async Task GetDatabaseClientsPathReturnsActiveDatabaseClientsAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);

        using HttpResponseMessage response = await session.Client.GetAsync("/_databaseclients").ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        AssertStatus(response, HttpStatusCode.OK, body);
        using JsonDocument json = ParseJson(body);
        TestAssert.Equal(JsonValueKind.Array, json.RootElement.ValueKind, body);
        TestAssert.True(
            json.RootElement.EnumerateArray().Any(e => string.Equals(e.GetString(), session.DatabaseName, StringComparison.OrdinalIgnoreCase)),
            body);
    }

    public static async Task GetDatabasePathListsTablesAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("getdb");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName)).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            JsonElement tableNames = RequireProperty(json.RootElement, "TableNames", body);

            TestAssert.True(
                tableNames.EnumerateArray().Any(e => string.Equals(e.GetString(), tableName, StringComparison.OrdinalIgnoreCase)),
                body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetDatabasePathReturnsContextWhenRequestedAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("getdbcontext");
        const string databaseContext = "Database context for metadata retrieval.";
        const string tableContext = "Table context for metadata retrieval.";

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await UpdateDatabaseContextAsync(session, databaseContext, new Dictionary<string, string>
            {
                [tableName] = tableContext
            }).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "?_context=true").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(databaseContext, GetStringProperty(json.RootElement, "Context", body), body);

            JsonElement tableNames = RequireProperty(json.RootElement, "TableNames", body);
            TestAssert.True(
                tableNames.EnumerateArray().Any(e => string.Equals(e.GetString(), tableName, StringComparison.OrdinalIgnoreCase)),
                body);

            JsonElement tableContexts = RequireProperty(json.RootElement, "TableContexts", body);
            TestAssert.Equal(tableContext, GetStringProperty(tableContexts, tableName, body), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetDatabaseDescribePathReturnsDescribedTablesAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("getdbdescribe");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "?_describe=true").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            JsonElement tables = RequireProperty(json.RootElement, "Tables", body);
            JsonElement table = FindArrayObjectByStringProperty(tables, "Name", tableName, body);

            TestAssert.Equal("person_id", GetStringProperty(table, "PrimaryKey", body), body);
            TestAssert.Equal(5, RequireProperty(table, "Columns", body).GetArrayLength(), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetDatabaseDescribePathReturnsContextWhenRequestedAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("getdbdescribectx");
        const string databaseContext = "Database context for described metadata.";
        const string tableContext = "Table context for described metadata.";

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await UpdateDatabaseContextAsync(session, databaseContext, new Dictionary<string, string>
            {
                [tableName] = tableContext
            }).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "?_describe=true&_context=true").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(databaseContext, GetStringProperty(json.RootElement, "Context", body), body);

            JsonElement tables = RequireProperty(json.RootElement, "Tables", body);
            JsonElement table = FindArrayObjectByStringProperty(tables, "Name", tableName, body);
            TestAssert.Equal(tableContext, GetStringProperty(table, "Context", body), body);
            TestAssert.Equal("person_id", GetStringProperty(table, "PrimaryKey", body), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetTableDescribePathReturnsTableMetadataAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("gettabledescribe");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_describe=true").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(tableName, GetStringProperty(json.RootElement, "Name", body), body);
            TestAssert.Equal("person_id", GetStringProperty(json.RootElement, "PrimaryKey", body), body);
            TestAssert.Equal(5, RequireProperty(json.RootElement, "Columns", body).GetArrayLength(), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetTableDescribePathReturnsContextWhenRequestedAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("gettabledescribectx");
        const string tableContext = "Table context for single-table describe.";

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await UpdateTableContextAsync(session, tableName, tableContext).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_describe=true&_context=true").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(tableName, GetStringProperty(json.RootElement, "Name", body), body);
            TestAssert.Equal(tableContext, GetStringProperty(json.RootElement, "Context", body), body);
            TestAssert.Equal("person_id", GetStringProperty(json.RootElement, "PrimaryKey", body), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PostTableCreatePathCreatesTableAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("createtable");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage describeResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_describe=true").ConfigureAwait(false);
            string describeBody = await describeResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(describeResponse, HttpStatusCode.OK, describeBody);
            using JsonDocument json = ParseJson(describeBody);
            TestAssert.Equal(tableName, GetStringProperty(json.RootElement, "Name", describeBody), describeBody);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetTableSelectPathReturnsDefaultUnpagedResultsAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("selectdefault");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "doe", 35, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);

            TestAssert.Equal(2, json.RootElement.GetArrayLength(), body);
            JsonElement firstRow = json.RootElement[0];
            JsonElement secondRow = json.RootElement[1];
            TestAssert.False(firstRow.TryGetProperty("__row_num__", out _), body);
            AssertPersonRow(firstRow, 1, "joel", "christner", 40, body);
            AssertPersonRow(secondRow, 2, "jane", "doe", 35, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetTableSelectPathReturnsFilteredPagedProjectedResultsAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("selectpaged");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "one", 18, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "two", 18, "2024-01-02 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "three", 35, "2024-01-03 00:00:00")).ConfigureAwait(false);

            string path = "/"
                + Encode(session.DatabaseName)
                + "/"
                + Encode(tableName)
                + "?first_name=joel&age=18&_index=2&_max=1&_order_by=person_id&_order=asc&_return=first_name,age";

            using HttpResponseMessage response = await session.Client.GetAsync(path).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);

            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            JsonElement row = json.RootElement[0];
            TestAssert.Equal("joel", GetStringProperty(row, "first_name", body), body);
            TestAssert.Equal(18L, GetInt64Property(row, "age", body), body);
            TestAssert.Equal(2L, GetInt64Property(row, "__row_num__", body), body);
            TestAssert.False(row.TryGetProperty("person_id", out _), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetTableByIdPathReturnsMatchingRowAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("getbyid");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            long personId = await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "/" + personId).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            JsonElement row = json.RootElement[0];
            AssertPersonRow(row, personId, "joel", "christner", 40, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task GetTableByIdPathCombinesIdAndQuerystringFiltersAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("getbyidfilters");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            long personId = await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage response = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "/" + personId + "?first_name=joel").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            AssertPersonRow(json.RootElement[0], personId, "joel", "christner", 40, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PutSearchPathReturnsExpressionMatchesAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("putsearch");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("alpha", "one", 18, null)).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "Christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "doe", 35, null)).ConfigureAwait(false);

            using HttpResponseMessage response = await PutJsonStringAsync(session, "/" + Encode(session.DatabaseName) + "/" + Encode(tableName), SearchExpressionJson).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);

            TestAssert.Equal(2, json.RootElement.GetArrayLength(), body);
            JsonElement alphaRow = FindArrayObjectByStringProperty(json.RootElement, "first_name", "alpha", body);
            JsonElement joelRow = FindArrayObjectByStringProperty(json.RootElement, "first_name", "joel", body);
            AssertPersonRow(alphaRow, 1, "alpha", "one", 18, body, expectCreated: false);
            AssertPersonRow(joelRow, 2, "joel", "Christner", 40, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PutSearchPathCombinesExpressionAndQuerystringFiltersAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("putsearchfilters");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 18, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "other", 18, "2024-01-02 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 17, "2024-01-03 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "christner", 18, "2024-01-04 00:00:00")).ConfigureAwait(false);

            string path = "/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?last_name=christner";
            using HttpResponseMessage response = await PutJsonStringAsync(session, path, CombinedSearchExpressionJson).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);

            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            JsonElement row = json.RootElement[0];
            AssertPersonRow(row, 1, "joel", "christner", 18, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PostTableInsertPathReturnsInsertedRowAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("postinsert");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage response = await PostJsonAsync(
                session,
                "/" + Encode(session.DatabaseName) + "/" + Encode(tableName),
                CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.Created, body);
            using JsonDocument json = ParseJson(body);
            AssertPersonRow(json.RootElement, 1, "joel", "christner", 40, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PostTableInsertPathCoercesTypedStringValuesAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("posttyped");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage response = await PostJsonAsync(
                session,
                "/" + Encode(session.DatabaseName) + "/" + Encode(tableName),
                TestData.SampleStringTypedInsertValues()).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.Created, body);
            using JsonDocument json = ParseJson(body);
            long personId = GetInt64Property(json.RootElement, "person_id", body);
            AssertPersonRow(json.RootElement, personId, "joel", "christner", 40, body);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string getBody = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, getBody);
            using JsonDocument getJson = ParseJson(getBody);
            TestAssert.Equal(1, getJson.RootElement.GetArrayLength(), getBody);
            AssertPersonRow(getJson.RootElement[0], personId, "joel", "christner", 40, getBody);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PostTableInsertMultiplePathCreatesAllRowsAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("postmultiple");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage response = await PostJsonAsync(
                session,
                "/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_multiple=true",
                new List<Dictionary<string, object>>
                {
                    CreatePerson("alpha", "one", 22, "2024-01-03 00:00:00"),
                    CreatePerson("beta", "two", 23, "2024-01-04 00:00:00")
                }).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            AssertStatus(response, HttpStatusCode.Created, body);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string getBody = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, getBody);
            using JsonDocument json = ParseJson(getBody);
            TestAssert.Equal(2, json.RootElement.GetArrayLength(), getBody);
            AssertPersonRow(json.RootElement[0], 1, "alpha", "one", 22, getBody);
            AssertPersonRow(json.RootElement[1], 2, "beta", "two", 23, getBody);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PutUpdateByIdPathUpdatesMatchingRowAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("putupdate");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            long personId = await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "doe", 35, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage updateResponse = await PutJsonAsync(
                session,
                "/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "/" + personId,
                new Dictionary<string, object> { ["age"] = 18 }).ConfigureAwait(false);
            string updateBody = await updateResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(updateResponse, HttpStatusCode.OK, updateBody);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string getBody = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, getBody);
            using JsonDocument json = ParseJson(getBody);
            TestAssert.Equal(2, json.RootElement.GetArrayLength(), getBody);
            AssertPersonRow(json.RootElement[0], personId, "joel", "christner", 18, getBody);
            AssertPersonRow(json.RootElement[1], 2, "jane", "doe", 35, getBody);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task DeleteByIdPathRemovesMatchingRowAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("deletebyid");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            long personId = await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "doe", 35, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage deleteResponse = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "/" + personId).ConfigureAwait(false);
            AssertStatus(deleteResponse, HttpStatusCode.NoContent);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string body = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            AssertPersonRow(json.RootElement[0], 2, "jane", "doe", 35, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task DeleteByIdPathCombinesIdAndQuerystringFiltersAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("deletebyidfilters");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            long personId = await InsertPersonAsync(session, tableName, CreatePerson("joel", "christner", 40, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "doe", 35, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage deleteResponse = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "/" + personId + "?first_name=joel").ConfigureAwait(false);
            string deleteBody = await deleteResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            AssertStatus(deleteResponse, HttpStatusCode.NoContent, deleteBody);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string body = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            AssertPersonRow(json.RootElement[0], 2, "jane", "doe", 35, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task DeletePathRemovesAllRowsAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("deleteall");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("alpha", "one", 22, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("beta", "two", 23, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage deleteResponse = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            AssertStatus(deleteResponse, HttpStatusCode.NoContent);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string body = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(0, json.RootElement.GetArrayLength(), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task DeletePathRemovesQuerystringMatchesAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("deletefiltered");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "one", 18, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("joel", "two", 19, "2024-01-02 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("jane", "three", 18, "2024-01-03 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage deleteResponse = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?first_name=joel&age=18").ConfigureAwait(false);
            string deleteBody = await deleteResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            AssertStatus(deleteResponse, HttpStatusCode.NoContent, deleteBody);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string body = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(2, json.RootElement.GetArrayLength(), body);
            AssertPersonRow(json.RootElement[0], 2, "joel", "two", 19, body);
            AssertPersonRow(json.RootElement[1], 3, "jane", "three", 18, body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task DeleteTruncatePathClearsTableAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("deletetruncate");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("alpha", "one", 22, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("beta", "two", 23, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage deleteResponse = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_truncate=true").ConfigureAwait(false);
            AssertStatus(deleteResponse, HttpStatusCode.NoContent);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName)).ConfigureAwait(false);
            string body = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(0, json.RootElement.GetArrayLength(), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task DeleteDropPathRemovesTableAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("deletedrop");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);

            using HttpResponseMessage deleteResponse = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_drop=true").ConfigureAwait(false);
            AssertStatus(deleteResponse, HttpStatusCode.NoContent);

            using HttpResponseMessage getResponse = await session.Client.GetAsync("/" + Encode(session.DatabaseName)).ConfigureAwait(false);
            string body = await getResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(getResponse, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            JsonElement tableNames = RequireProperty(json.RootElement, "TableNames", body);
            TestAssert.False(
                tableNames.EnumerateArray().Any(e => string.Equals(e.GetString(), tableName, StringComparison.OrdinalIgnoreCase)),
                body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    public static async Task PostRawQueryPathExecutesAgainstSelectedProviderAsync()
    {
        RestDbLiveApiSession session = await RestDbLiveApiHost.GetAsync().ConfigureAwait(false);
        string tableName = CreateUniqueTableName("rawquery");

        try
        {
            await CreateTableAsync(session, tableName).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("alpha", "one", 22, "2024-01-01 00:00:00")).ConfigureAwait(false);
            await InsertPersonAsync(session, tableName, CreatePerson("beta", "two", 23, "2024-01-02 00:00:00")).ConfigureAwait(false);

            using HttpResponseMessage response = await PostTextAsync(
                session,
                "/" + Encode(session.DatabaseName) + "?raw=true",
                "SELECT COUNT(*) AS record_count FROM " + tableName + ";").ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            AssertStatus(response, HttpStatusCode.OK, body);
            using JsonDocument json = ParseJson(body);
            TestAssert.Equal(1, json.RootElement.GetArrayLength(), body);
            TestAssert.Equal(2L, GetInt64Property(json.RootElement[0], "record_count", body), body);
        }
        finally
        {
            await DropTableIfExistsAsync(session, tableName).ConfigureAwait(false);
        }
    }

    private static async Task CreateTableAsync(RestDbLiveApiSession session, string tableName)
    {
        using HttpResponseMessage response = await PostJsonAsync(
            session,
            "/" + Encode(session.DatabaseName),
            new Table
            {
                Name = tableName,
                PrimaryKey = "person_id",
                Columns = TestData.SampleColumns()
            }).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.Created, body);
    }

    private static async Task<long> InsertPersonAsync(RestDbLiveApiSession session, string tableName, Dictionary<string, object> values)
    {
        using HttpResponseMessage response = await PostJsonAsync(
            session,
            "/" + Encode(session.DatabaseName) + "/" + Encode(tableName),
            values).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.Created, body);

        using JsonDocument json = ParseJson(body);
        return GetInt64Property(json.RootElement, "person_id", body);
    }

    private static async Task UpdateDatabaseContextAsync(RestDbLiveApiSession session, string databaseContext, Dictionary<string, string> tableContexts)
    {
        using HttpResponseMessage response = await PutJsonAsync(
            session,
            "/_context/" + Encode(session.DatabaseName),
            new
            {
                Database = session.DatabaseName,
                Context = databaseContext,
                Tables = tableContexts
            }).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);
    }

    private static async Task UpdateTableContextAsync(RestDbLiveApiSession session, string tableName, string tableContext)
    {
        using HttpResponseMessage response = await PutJsonAsync(
            session,
            "/_context/" + Encode(session.DatabaseName) + "/" + Encode(tableName),
            new
            {
                Database = session.DatabaseName,
                Table = tableName,
                Context = tableContext
            }).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        AssertStatus(response, HttpStatusCode.OK, body);
    }

    private static async Task DropTableIfExistsAsync(RestDbLiveApiSession session, string tableName)
    {
        try
        {
            using HttpResponseMessage response = await session.Client.DeleteAsync("/" + Encode(session.DatabaseName) + "/" + Encode(tableName) + "?_drop=true").ConfigureAwait(false);
            _ = response.StatusCode;
        }
        catch
        {
        }
    }

    private static Dictionary<string, object> CreatePerson(string firstName, string lastName, object age, string? created)
    {
        Dictionary<string, object> values = new Dictionary<string, object>
        {
            ["first_name"] = firstName,
            ["last_name"] = lastName,
            ["age"] = age
        };

        if (created != null)
        {
            values["created"] = created;
        }

        return values;
    }

    private static string CreateUniqueTableName(string prefix)
    {
        return "restdb_live_" + prefix + "_" + Guid.NewGuid().ToString("N");
    }

    private static string Encode(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(RestDbLiveApiSession session, string path, object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        return await session.Client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PutJsonAsync(RestDbLiveApiSession session, string path, object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        return await session.Client.PutAsync(path, new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PutJsonStringAsync(RestDbLiveApiSession session, string path, string json)
    {
        return await session.Client.PutAsync(path, new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostTextAsync(RestDbLiveApiSession session, string path, string content)
    {
        return await session.Client.PostAsync(path, new StringContent(content, Encoding.UTF8, "text/plain")).ConfigureAwait(false);
    }

    private static void AssertStatus(HttpResponseMessage response, HttpStatusCode expected, string? body = null)
    {
        TestAssert.Equal(expected, response.StatusCode, body ?? ("Expected " + expected + " but found " + response.StatusCode + "."));
    }

    private static string GetSingleHeaderValue(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
        {
            return string.Join(",", values);
        }

        if (response.Content.Headers.TryGetValues(headerName, out IEnumerable<string>? contentValues))
        {
            return string.Join(",", contentValues);
        }

        throw new InvalidOperationException("Expected HTTP header '" + headerName + "'." + Environment.NewLine + response.Headers.ToString());
    }

    private static JsonDocument ParseJson(string body)
    {
        TestAssert.False(string.IsNullOrWhiteSpace(body), "Expected JSON response body.");
        return JsonDocument.Parse(body);
    }

    private static JsonElement RequireProperty(JsonElement element, string propertyName, string body)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidOperationException("Expected JSON property '" + propertyName + "'." + Environment.NewLine + body);
        }

        return value;
    }

    private static JsonElement FindArrayObjectByStringProperty(JsonElement array, string propertyName, string expectedValue, string body)
    {
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out JsonElement value)
                && string.Equals(value.GetString(), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return item.Clone();
            }
        }

        throw new InvalidOperationException("Expected array item with " + propertyName + " = '" + expectedValue + "'." + Environment.NewLine + body);
    }

    private static void AssertPersonRow(
        JsonElement row,
        long expectedPersonId,
        string expectedFirstName,
        string expectedLastName,
        long expectedAge,
        string body,
        bool expectCreated = true)
    {
        TestAssert.Equal(expectedPersonId, GetInt64Property(row, "person_id", body), body);
        TestAssert.Equal(expectedFirstName, GetStringProperty(row, "first_name", body), body);
        TestAssert.Equal(expectedLastName, GetStringProperty(row, "last_name", body), body);
        TestAssert.Equal(expectedAge, GetInt64Property(row, "age", body), body);

        if (expectCreated)
        {
            JsonElement created = RequireProperty(row, "created", body);
            TestAssert.False(created.ValueKind == JsonValueKind.Null, body);
            TestAssert.False(created.ValueKind == JsonValueKind.Undefined, body);
            if (created.ValueKind == JsonValueKind.String)
            {
                TestAssert.False(string.IsNullOrWhiteSpace(created.GetString()), body);
            }
        }
    }

    private static string GetStringProperty(JsonElement element, string propertyName, string body)
    {
        JsonElement value = RequireProperty(element, propertyName, body);
        return value.GetString() ?? string.Empty;
    }

    private static long GetInt64Property(JsonElement element, string propertyName, string body)
    {
        JsonElement value = RequireProperty(element, propertyName, body);

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long numericValue))
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out long parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidOperationException("Expected numeric JSON property '" + propertyName + "'." + Environment.NewLine + body);
    }
}
