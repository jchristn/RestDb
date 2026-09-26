# Change Log

## Current Version

v2.0.0

Platform and providers

- Retargeted to `net8.0` and `net10.0`.
- Removed `DatabaseWrapper` in favor of native SQL Server, MySQL, PostgreSQL, and SQLite implementations.
- Added bearer-token authentication alongside the configured API key header.
- Added runtime-editable `restdb.json` and `context.json` APIs.
- Added `_context` support on database and table metadata routes so context can be returned inline with database names, described tables, and table schema responses.
- Fixed filtered DELETE route handling to correctly apply querystring filters.
- Fixed provider-specific `LIKE` generation so MySQL no longer emits invalid escape syntax.
- Dependencies: Microsoft.Data.SqlClient 7.1.0, Microsoft.Data.Sqlite 10.0.12, MySqlConnector 2.6.2, Npgsql 10.0.3, Newtonsoft.Json 13.0.4, Watson 7.2.0, SyslogLogging 2.2.2, Inputty 1.0.13, and ExpressionTree 1.1.2. `SQLitePCLRaw.bundle_e_sqlite3` is pinned to 3.0.5 to ship SQLite >= 3.50.2 (CVE-2025-6965).
- NuGet packages include symbol packages (`.snupkg`).

Dashboard

- Added the RestDb dashboard for browsing schemas, rows, and table operations, with editors for server settings and database, table, and global context.
- Added a SQL query console (`/query`) with JSON copy and CSV download.
- Initial workspace loads fetch only database and selected-table metadata.

MCP server (`RestDb.McpServer`)

- Exposes the RestDb API over MCP HTTP, TCP, WebSocket, and stdio, built on Voltaic 2.1.0. `tools/list` and `tools/call` work on every transport, and each tool is also callable as a direct JSON-RPC method.
- HTTP `/mcp` serves handshake-era clients (`initialize` plus `Mcp-Session-Id`) and stateless `2026-07-28` clients such as Claude Code 2.1.x, and sends the immediate SSE prelude and `202 Accepted` notification responses that Codex expects.
- `tools/list` publishes only the RestDb tools. `ping` returns `{}` (plus `resultType: "complete"` under `2026-07-28`). Tool arguments are validated against each tool's input schema, including `additionalProperties`.
- Failed RestDb requests (non-2xx) are flagged `isError: true` in `tools/call` results. Results carry only RestDb's own `x-` response headers (`x-expression`, `x-restart-required`, `x-operation-message`) and omit `Headers` when there are none.
- RestDb API-key and bearer-token authentication are configured on the `RestDb.McpServer` process (`--api-key`, `--bearer-token`, `RESTDB_MCP_*`), not in agent client configs.
- `RestDb.McpServer install` configures Claude Code, Codex, Gemini CLI, and Cursor MCP definitions from the command line.
- **Security:** listeners bind to loopback by default (HTTP and WebSocket `localhost`, TCP `127.0.0.1`), and loopback-bound HTTP and WebSocket listeners refuse non-loopback clients even when they spoof `Host: localhost`. Pass `--http-host +`, `--ws-host +`, and `--tcp-host 0.0.0.0` to accept remote clients; Docker Compose sets these.
- **Security:** HTTP and WebSocket refuse browser origins other than loopback with `403` (extend with `--allowed-origins` / `RESTDB_MCP_ALLOWED_ORIGINS`), and CORS echoes the allowed origin instead of `*`. `POST /mcp` requires `Content-Type: application/json` (`415` otherwise). TCP accepts only `Content-Length` / `Content-Type` framing, so a browser's HTTP request is dropped.
- **Security:** an optional MCP bearer token (`--mcp-token` / `RESTDB_MCP_TOKEN`) is required on HTTP and on the WebSocket upgrade request (`Authorization: Bearer <token>`). Rejections return `401` with an RFC 6750 `WWW-Authenticate: Bearer` challenge. `install` writes the token into client configs as an `Authorization` header. TCP and stdio are unauthenticated by design.
- **Security:** Streamable HTTP sessions are created only by a successful `initialize`. `POST /mcp` without a session gets `400` (except `initialize` and `ping`), and an unknown, expired, or terminated `Mcp-Session-Id` gets `404`.

Documentation and tooling

- Added `REST_API.md` and `MCP_API.md` to document the HTTP and MCP surfaces.
- Rebuilt the Postman collection around the current routes, with runtime settings and context management requests and examples aligned with the bundled `sample.db` data.
- Docker Compose runs the published `restdb`, `restdb-dashboard`, and `restdb-mcp` images, and the root `build-*.bat` scripts publish multi-arch images.

Tests

- Shared Touchstone suite exposed through CLI, xUnit, and NUnit runners.
- Query-builder coverage for every SQL-emitting API route across all supported providers.
- Live API coverage through `RestDb.Test.Automated`, including Docker-backed MySQL, PostgreSQL, and SQL Server runs, validating inserted, updated, retrieved, and deleted data, plus negative cases (401, 404, and 400).
- MCP coverage for the stateless and session paths, SSE notification relay, session rules, `tools/call` on every transport, downstream error flagging, response-header trimming against a live server, schema validation, and negative cases (unknown tool, missing argument, failing handler, unknown protocol version, malformed JSON, mismatched `Mcp-Method`, invalid sessions, unsupported HTTP methods).
- Access-control coverage against the production server configuration: Origin allow and deny (including lookalike origins and preflight), `415` for non-JSON bodies, token accept and reject on HTTP and WebSocket with the caller reaching tool handlers, loopback-only clients against a spoofed `Host` from a LAN address, and TCP rejection of browser-style HTTP requests.

## Previous Versions

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


