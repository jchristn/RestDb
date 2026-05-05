# RestDb REST API

Base URL examples:

- `http://localhost:8000`
- `https://restdb.example.com:8000`

Authentication:

- Header mode: send the configured API key in `x-api-key` or whatever `Server.ApiKeyHeader` is set to.
- Bearer mode: send `Authorization: Bearer <api-key>`.
- If `Server.RequireAuthentication` is `false`, no API key is required.

## System Routes

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/` | Root health/status page. |
| `OPTIONS` | `/*` | Browser CORS preflight handling. Preflight requests are processed before authentication. |
| `GET` | `/_databaseclients` | Lists the supported provider client names. |
| `GET` | `/_databases` | Lists configured database names. |

## Runtime Configuration Routes

These routes let you inspect, update, and reload `restdb.json` and `context.json` without restarting for non-listener changes.

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/_settings` | Returns the live `restdb.json` document. |
| `PUT` | `/_settings` | Replaces and persists `restdb.json`. |
| `POST` | `/_settings/reload` | Reloads `restdb.json` from disk. |
| `GET` | `/_context` | Returns the full shared `context.json` document. |
| `PUT` | `/_context` | Replaces and persists the full `context.json` document. |
| `POST` | `/_context/reload` | Reloads `context.json` from disk. |
| `GET` | `/_context/{database}` | Returns database-level context plus table context entries. |
| `PUT` | `/_context/{database}` | Updates a database context entry. |
| `GET` | `/_context/{database}/{table}` | Returns table-level context. |
| `PUT` | `/_context/{database}/{table}` | Updates table-level context. |

Notes:

- `PUT /_settings` and `POST /_settings/reload` return a body containing `Result` and `Settings`.
- If listener hostname, port, or SSL changes, the response includes `x-restart-required: true`.
- `PUT /_context*` and `POST /_context/reload` return a body containing `Result` and the updated context payload.

## Database Metadata Routes

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/{database}` | Returns database metadata and table names. Add `?_context=true` to also include database context plus a `TableContexts` map for the returned table names. |
| `GET` | `/{database}?_describe=true` | Returns database metadata and full table schema for every table. Add `&_context=true` to also include database context plus per-table `Context` values in `Tables`. |
| `GET` | `/{database}/{table}?_describe=true` | Returns the schema for one table. Add `&_context=true` to also include that table's `Context` value. |

## Record Routes

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/{database}/{table}` | Enumerates rows. Supports filters, pagination, ordering, and projection. |
| `GET` | `/{database}/{table}/{id}` | Retrieves one row by primary key. |
| `PUT` | `/{database}/{table}` | Searches rows using an `ExpressionTree` payload. |
| `PUT` | `/{database}/{table}/{id}` | Updates one row by primary key. |
| `POST` | `/{database}/{table}` | Inserts one row. |
| `POST` | `/{database}/{table}?_multiple=true` | Inserts multiple rows from a JSON array. |
| `DELETE` | `/{database}/{table}` | Deletes rows matching optional querystring equality filters. |
| `DELETE` | `/{database}/{table}/{id}` | Deletes one row by primary key, with optional extra filters. |
| `DELETE` | `/{database}/{table}?_truncate=true` | Clears every row from the table. |
| `DELETE` | `/{database}/{table}?_drop=true` | Drops the table. |

Notes:

- If querystring equality filters are also supplied on `GET /{database}/{table}/{id}`, the server applies them in addition to the primary-key filter.
- `DELETE /{database}/{table}/{id}` also accepts optional querystring equality filters as additional guards before deletion.

## Schema and Raw SQL Routes

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/{database}` | Creates a table from a `Table` payload. |
| `POST` | `/{database}?raw=true` | Executes raw SQL against the selected database. |

## Query Parameters

Supported row-query parameters:

- `_describe`
- `_context`
- `_multiple`
- `_index`
- `_max`
- `_order_by`
- `_order`
- `_return`
- `_truncate`
- `_drop`
- `_debug`

Any other querystring key on row routes is treated as an equality filter.

Examples:

- `GET /sample/person?_order_by=person_id&_order=desc&_max=25`
- `GET /sample/task?person_id=2&project_id=4`
- `DELETE /sample/person?department_id=3`
- `GET /sample?_context=true`
- `GET /sample?_describe=true&_context=true`
- `GET /sample/person?_describe=true&_context=true`

## Context Document Shape

`context.json` uses a shared multi-database structure:

```json
{
  "Databases": {
    "sample": {
      "Context": "This database has example information.",
      "Tables": {
        "person": "Contains information about people, primary key is person_id."
      }
    }
  }
}
```

## Related Docs

- [MCP_API.md](MCP_API.md)
- [TESTING.md](TESTING.md)
