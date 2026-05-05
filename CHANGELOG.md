# Change Log

## Current Version

v2.0.7

- Retargeted to `net8.0` and `net10.0`.
- Removed `DatabaseWrapper` in favor of native SQL Server, MySQL, PostgreSQL, and SQLite implementations.
- Added the RestDb dashboard and Docker Compose workspace flow for the bundled sample database.
- Added runtime-editable `restdb.json` and `context.json` APIs, plus dashboard editors for server settings and context metadata.
- Added `RestDb.McpServer` using Voltaic with HTTP, TCP, WebSocket, and stdio transports covering the RestDb API surface.
- Added a built-in `RestDb.McpServer install` workflow to configure Claude Code, Codex, Gemini CLI, and Cursor MCP definitions from the command line.
- Added `REST_API.md` and `MCP_API.md` to document the HTTP and MCP surfaces.
- Migrated tests to a shared Touchstone suite exposed through CLI, xUnit, and NUnit runners.
- Added exhaustive query-builder coverage for every SQL-emitting API route across all supported providers.
- Added live API coverage through `RestDb.Test.Automated`, including Docker-backed MySQL, PostgreSQL, and SQL Server runs.
- Strengthened live tests to validate inserted, updated, retrieved, and deleted data rather than only status codes or row counts.
- Added bearer-token authentication alongside the configured API key header.
- Rebuilt the Postman collection around the current routes, corrected request naming, added runtime settings/context management requests, and aligned the default examples with the bundled `sample.db` data.
- Reduced repeated dashboard metadata requests by narrowing initial workspace fetches to database and selected-table metadata.
- Fixed filtered DELETE route handling to correctly apply querystring filters.
- Fixed provider-specific `LIKE` generation so MySQL no longer emits invalid escape syntax.

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


