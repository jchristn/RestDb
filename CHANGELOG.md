# Change Log

## Current Version

v2.0.8

- Upgraded `RestDb.McpServer` to Voltaic 2.0.0.
- MCP `tools/list` now publishes only the RestDb tools on every transport. Voltaic's demo tools (`ping`, `echo`, `getTime`, `getSessions` on HTTP, `getClients` on TCP) are no longer listed or callable; `getSessions` disclosed every client's `Mcp-Session-Id`.
- MCP `ping` now returns `{}` (plus `resultType: "complete"` under `2026-07-28`) as the MCP specification requires, instead of `"pong"`.
- Tool arguments are now validated against `additionalProperties` schemas. `restdb_update_database_context` rejects non-string `tables` values with `-32602`; `filters` objects still accept any property.
- Fixed the HTTP MCP endpoint for stateless `2026-07-28` clients such as Claude Code 2.1.x, which previously connected but failed to list tools with "missing required resultType". The `/mcp` bridge now forwards to Voltaic's native Streamable HTTP endpoint instead of the `/rpc` compatibility endpoint. Handshake-era clients (`initialize` plus `Mcp-Session-Id`) continue to work unchanged.
- Fixed `tools/call` on the TCP and WebSocket MCP transports, which listed the RestDb tools but rejected calls to them. Every transport now registers real MCP tools; tools remain callable as direct JSON-RPC methods.
- MCP `tools/call` results for failed downstream RestDb requests (non-2xx) are now flagged `isError: true`. The text content is unchanged and still carries `Success`, `StatusCode`, and the response body. Direct JSON-RPC method calls still return the raw response.
- Added MCP test coverage for the stateless and session paths, SSE notification relay, session termination, TCP and WebSocket `tools/call`, downstream error flagging, and negative cases (unknown tool, missing required argument, failing handler, unknown protocol version, malformed JSON, mismatched `Mcp-Method`, invalid sessions, unsupported HTTP methods), plus Voltaic 2.0 coverage on HTTP, TCP, and WebSocket: `tools/list` contains exactly the RestDb tools, `ping` returns an empty result, removed demo tools return `-32602` through `tools/call` and `-32601` as bare methods, RestDb direct JSON-RPC methods still work, and the production catalog's `additionalProperties` schemas accept and reject arguments as declared.
- Updated dependencies: Microsoft.Data.SqlClient 7.1.0, Microsoft.Data.Sqlite 10.0.12, SyslogLogging 2.2.2, Watson 7.2.0, Microsoft.NET.Test.Sdk 18.10.1, NUnit.Analyzers 4.15.0, NUnit3TestAdapter 6.3.0.
- Refreshed .NET and dashboard dependencies, including pinning `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 to ship SQLite >= 3.50.2 (CVE-2025-6965), and negative live-API coverage (401, 404, and 400 cases).
- Added a SQL query console to the dashboard (`/query`) with JSON copy and CSV download, and resolved dashboard Dependabot alerts.
- Docker Compose now runs the published `restdb`, `restdb-dashboard`, and `restdb-mcp` images, and root `build-*.bat` scripts publish multi-arch images.

## Previous Versions

v2.0.7

- Retargeted to `net8.0` and `net10.0`.
- Removed `DatabaseWrapper` in favor of native SQL Server, MySQL, PostgreSQL, and SQLite implementations.
- Added the RestDb dashboard and Docker Compose workspace flow for the bundled sample database.
- Added runtime-editable `restdb.json` and `context.json` APIs, plus dashboard editors for server settings and context metadata.
- Added `_context` support on database and table metadata routes so context can be returned inline with database names, described tables, and table schema responses.
- Added `RestDb.McpServer` using Voltaic with HTTP, TCP, WebSocket, and stdio transports covering the RestDb API surface.
- Added a built-in `RestDb.McpServer install` workflow to configure Claude Code, Codex, Gemini CLI, and Cursor MCP definitions from the command line.
- Corrected MCP client configuration to use the HTTP MCP endpoint and to keep RestDb API-key authentication on the `RestDb.McpServer` proxy side instead of injecting it into agent client configs.
- Fixed the public MCP HTTP stream so Codex receives an immediate SSE prelude on `/mcp`, corrected streamable-HTTP notification handling to return `202 Accepted` with an empty body, and removed misleading `install --api-key ...` examples from the MCP setup docs.
- Added `REST_API.md` and `MCP_API.md` to document the HTTP and MCP surfaces.
- Migrated tests to a shared Touchstone suite exposed through CLI, xUnit, and NUnit runners.
- Added exhaustive query-builder coverage for every SQL-emitting API route across all supported providers.
- Added live API coverage through `RestDb.Test.Automated`, including Docker-backed MySQL, PostgreSQL, and SQL Server runs.
- Added direct MCP HTTP bridge coverage for the `/mcp` streamable-HTTP contract, including `application/json; charset=utf-8`, `notifications/initialized -> 202`, `tools/list`, and the immediate SSE prelude.
- Strengthened live tests to validate inserted, updated, retrieved, and deleted data rather than only status codes or row counts.
- Added bearer-token authentication alongside the configured API key header.
- Rebuilt the Postman collection around the current routes, corrected request naming, added runtime settings/context management requests, and aligned the default examples with the bundled `sample.db` data.
- Reduced repeated dashboard metadata requests by narrowing initial workspace fetches to database and selected-table metadata.
- Fixed filtered DELETE route handling to correctly apply querystring filters.
- Fixed provider-specific `LIKE` generation so MySQL no longer emits invalid escape syntax.

v2.0.1

- Breaking change caused by dependency updates
- Multiple insert API
- Internal refactor
- More complete Postman environment
- Error codes

v1.3.0

- Dependency update
- Support for ```DateTimeOffset``` types

v1.2.7

- .NET 5 support
- Dependency update
- Change to pagination

v1.2.5

- Dependency update
- Raw query API

v1.2.2

- Fix for multi-platform

v1.2.1

- Logo and listener notifications (localhost, wildcard)

v1.2.0

- Dependency updates
- Added support for Sqlite
- Table creation, drop, and truncate APIs
- .NET Core only (removed support for .NET Framework)

v1.1.0

- Dependency updates
- Async operation
- ```_describe``` no longer needs ```=true``` in the querystring

v1.0.3

- Retarget to .NET Core and .NET Framework
 
v1.0.x

- PostgreSQL support
- Authentication via API key
- Initial release


