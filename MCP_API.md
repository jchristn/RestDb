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
- `--stdio`
- `--dry-run`
- `--yes`

## Agent Installer

`RestDb.McpServer` includes a built-in installer for the common agent clients:

- Claude Code
- Codex
- Gemini CLI
- Cursor

Example:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --api-key default --yes
```

Preview the generated config without writing files:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --api-key default --dry-run
```

The installer writes user-scoped config files:

- `~/.claude.json`
- `~/.codex/config.toml`
- `~/.gemini/settings.json`
- `~/.cursor/mcp.json`

When `--api-key` is supplied, the generated client definitions include both `Authorization: Bearer <api-key>` and the configured API-key header so the installed entry works with either RestDb auth style.

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
| `restdb_inspect_database` | `GET /{database}` |
| `restdb_inspect_database_with_schema` | `GET /{database}?_describe=true` |
| `restdb_inspect_table_schema` | `GET /{database}/{table}?_describe=true` |

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
- Row-by-ID tools use `rowId`.
- Equality query filters are passed as a `filters` object.
- `restdb_search_table_records` accepts an `expression` payload compatible with the `ExpressionTree` REST body.
- Bulk insert uses a `records` array.
- Full-document config editors use `settings` or `context`.

## Transport Notes

- HTTP and stdio expose proper MCP tools and tool metadata.
- TCP and WebSocket expose the same operations as registered methods and also publish `tools/list`.
- The MCP service uses the configured RestDb API key or bearer token when proxying requests downstream.

## Related Docs

- [REST_API.md](REST_API.md)
- [README.md](README.md)
