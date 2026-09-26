# RestDb MCP API

`RestDb.McpServer` exposes the RestDb HTTP API over MCP using a REST-proxy model. The MCP server does not implement database logic itself; it forwards to RestDb so behavior stays aligned with the HTTP API.

## Transports

- HTTP MCP: `http://localhost:8010/mcp`
- TCP JSON-RPC/MCP: `tcp://localhost:8011`
- WebSocket MCP: `ws://localhost:8012/mcp`
- stdio: `dotnet run --project src/RestDb.McpServer/RestDb.McpServer.csproj -- --stdio`

Compose defaults:

- `RESTDB_MCP_SERVER_URL=http://restdb:8000`
- `RESTDB_MCP_API_KEY=default`
- `RESTDB_MCP_API_KEY_HEADER=x-api-key`

`RESTDB_MCP_API_KEY`, `RESTDB_MCP_API_KEY_HEADER`, and `RESTDB_MCP_BEARER_TOKEN` are used by `RestDb.McpServer` when it calls the protected RestDb HTTP API. MCP clients connecting to the HTTP endpoint do not need to send those RestDb auth headers. `RESTDB_MCP_TOKEN` and `RESTDB_MCP_ALLOWED_ORIGINS` control who may connect to `RestDb.McpServer` itself; see [Access Control](#access-control).

Optional CLI flags:

- `install`
- `--server-url`
- `--api-key`
- `--api-key-header`
- `--bearer-token`
- `--http-host`
- `--http-port`
- `--tcp-host`
- `--tcp-port`
- `--ws-host`
- `--ws-port`
- `--allowed-origins`
- `--mcp-token`
- `--stdio`
- `--dry-run`
- `--yes`

## Access Control

Voltaic (2.1.0 or later) enforces these rules on every request, before it reaches a tool. TCP and stdio are intentionally unauthenticated.

- **Loopback by default.** HTTP and WebSocket listen on `localhost` and TCP on `127.0.0.1`. When a listener is bound to a loopback host, requests from non-loopback addresses are refused with `403`, even if they spoof `Host: localhost`. Use `--http-host +`, `--ws-host +`, and `--tcp-host 0.0.0.0` (the Docker Compose defaults) to accept remote clients.
- **Origin allowlist (HTTP and WebSocket).** Browsers send an `Origin` header. Requests from origins other than loopback (`http(s)://localhost`, `127.0.0.1`, or `[::1]` on any port) are refused with `403` before anything else, including CORS preflight and WebSocket upgrades. This keeps a web page you visit from driving a locally running server. Add origins with `--allowed-origins https://app.example,https://other.example` or `RESTDB_MCP_ALLOWED_ORIGINS`. `*` allows any origin. Requests without an `Origin` header (Claude Code, Codex, MCP Inspector CLI, curl) are unaffected. CORS responses echo the allowed origin and never use `*`.
- **Optional bearer token (HTTP and WebSocket).** With `--mcp-token <value>` or `RESTDB_MCP_TOKEN`, clients must send `Authorization: Bearer <value>`, on every HTTP request and on the WebSocket upgrade request alike. Missing or wrong tokens get `401` with an RFC 6750 `WWW-Authenticate: Bearer` challenge (`error="invalid_token"` when a wrong token was sent). The health check (`GET /`) and CORS preflight do not require the token. This token authenticates MCP clients to `RestDb.McpServer`; it is separate from, and never forwarded as, the RestDb API key or bearer token. `install` writes it into each client config as an `Authorization` header when it is set.
- **JSON only on `/mcp`.** `POST /mcp` requires `Content-Type: application/json` and answers anything else with `415`. This closes the `text/plain` path that browsers can use without a CORS preflight.
- **Sessions come only from `initialize`.** `POST /mcp` without an `Mcp-Session-Id` gets `400`, except `initialize` and `ping`. An unknown, expired, or terminated session ID gets `404`, which tells the client to initialize again. The stateless `2026-07-28` path does not use sessions.
- **TCP framing.** TCP accepts only `Content-Length` and `Content-Type` framing header lines. Anything else, including an HTTP request a browser can send to the TCP port, closes the connection before any method runs. TCP has no Origin or token; keep it on loopback (the default) or restrict the port at the network level.

## Agent Installer

`RestDb.McpServer` includes a built-in installer for the common agent clients:

- Claude Code
- Codex
- Gemini CLI
- Cursor

Example:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --yes
```

Preview the generated config without writing files:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --dry-run
```

The installer writes user-scoped config files:

- `~/.claude.json`
- `~/.codex/config.toml`
- `~/.gemini/settings.json`
- `~/.cursor/mcp.json`

The installer only writes client-side MCP endpoint definitions. Configure downstream RestDb authentication on the `RestDb.McpServer` process itself with `--api-key`, `--bearer-token`, or `RESTDB_MCP_*` environment variables. The generated MCP client definitions remain plain HTTP endpoint definitions and do not inject RestDb API-key headers into the client transport.

## Tool Coverage

Each tool returns the proxied REST result with:

- `Success`
- `StatusCode`
- `ReasonPhrase`
- `Headers`
- `Body`

### System and Configuration

| Tool | REST route |
| --- | --- |
| `restdb_check_system_health` | `GET /` |
| `restdb_retrieve_database_client_capabilities` | `GET /_databaseclients` |
| `restdb_retrieve_database_list` | `GET /_databases` |
| `restdb_retrieve_server_settings` | `GET /_settings` |
| `restdb_update_server_settings` | `PUT /_settings` |
| `restdb_reload_server_settings` | `POST /_settings/reload` |
| `restdb_retrieve_context_document` | `GET /_context` |
| `restdb_update_context_document` | `PUT /_context` |
| `restdb_reload_context_document` | `POST /_context/reload` |
| `restdb_retrieve_database_context` | `GET /_context/{database}` |
| `restdb_update_database_context` | `PUT /_context/{database}` |
| `restdb_retrieve_table_context` | `GET /_context/{database}/{table}` |
| `restdb_update_table_context` | `PUT /_context/{database}/{table}` |

### Metadata and Schema

| Tool | REST route |
| --- | --- |
| `restdb_inspect_database` | `GET /{database}` or `GET /{database}?_context=true` |
| `restdb_inspect_database_with_schema` | `GET /{database}?_describe=true` or `GET /{database}?_describe=true&_context=true` |
| `restdb_inspect_table_schema` | `GET /{database}/{table}?_describe=true` or `GET /{database}/{table}?_describe=true&_context=true` |

### Records and SQL

| Tool | REST route |
| --- | --- |
| `restdb_enumerate_table_records` | `GET /{database}/{table}` |
| `restdb_retrieve_table_record_by_id` | `GET /{database}/{table}/{id}` |
| `restdb_search_table_records` | `PUT /{database}/{table}` |
| `restdb_update_table_record_by_id` | `PUT /{database}/{table}/{id}` |
| `restdb_create_table` | `POST /{database}` |
| `restdb_insert_table_record` | `POST /{database}/{table}` |
| `restdb_insert_table_records` | `POST /{database}/{table}?_multiple=true` |
| `restdb_execute_raw_sql` | `POST /{database}?raw=true` |
| `restdb_delete_table_records` | `DELETE /{database}/{table}` |
| `restdb_delete_table_record_by_id` | `DELETE /{database}/{table}/{id}` |
| `restdb_truncate_table` | `DELETE /{database}/{table}?_truncate=true` |
| `restdb_drop_table` | `DELETE /{database}/{table}?_drop=true` |

## Input Notes

- Database tools use `databaseName`.
- Table tools use `databaseName` and `tableName`.
- Metadata inspection tools also accept `includeContext`. When `true`, the MCP proxy appends `?_context=true` to the underlying REST metadata route.
- Row-by-ID tools use `rowId`.
- Equality query filters are passed as a `filters` object.
- `restdb_search_table_records` accepts an `expression` payload compatible with the `ExpressionTree` REST body.
- Bulk insert uses a `records` array.
- Full-document config editors use `settings` or `context`.

## Transport Notes

- HTTP and WebSocket both use `/mcp` on their respective transports.
- On HTTP streamable transport, `notifications/initialized` and other notification-only POSTs return `202 Accepted` with an empty body.
- HTTP `/mcp` serves both handshake-era clients (`initialize` plus an `Mcp-Session-Id`, protocol revisions `2024-11-05` through `2025-11-25`) and stateless `2026-07-28` clients such as Claude Code 2.1.x (`server/discover` plus per-request `MCP-Protocol-Version` and `Mcp-Method` headers). Stateless results carry `resultType`, and list results also carry `ttlMs` and `cacheScope`.
- HTTP, stdio, TCP, and WebSocket all expose proper MCP tools and tool metadata through `tools/list` and `tools/call`. `tools/list` returns only the RestDb tools listed above.
- `ping` is the MCP protocol method and returns an empty result (`{}`, or `{"resultType":"complete"}` under `2026-07-28`).
- Tool arguments are validated against each tool's input schema before the tool runs. Missing required arguments and values that violate the schema (for example a non-string entry in the `tables` map of `restdb_update_database_context`) return JSON-RPC error `-32602`.
- Every tool is also registered as a JSON-RPC method of the same name, so callers can invoke it directly (for example `{"method":"restdb_retrieve_database_list"}`).
- When the downstream RestDb request fails (non-2xx), `tools/call` returns the usual text content (`Success`, `StatusCode`, `ReasonPhrase`, `Headers`, `Body`) with `isError: true`. Direct JSON-RPC method calls return the raw response object without the `isError` wrapper.
- The MCP service uses the configured RestDb API key or bearer token when proxying requests downstream.

## Related Docs

- [REST_API.md](REST_API.md)
- [README.md](README.md)
