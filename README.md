# RestDb

RESTful HTTP/HTTPS server for Microsoft SQL Server, MySQL, and PostgreSQL database tables written in C#.  

## Description

RestDb spawns a RESTful HTTP/HTTPS server that exposes a series of APIs allowing you to perform SELECT, INSERT, UPDATE, DELETE, TRUNCATE, and DROP against tables in Microsoft SQL Server, MySQL, PostgreSQL, and Sqlite.
 
## New in v2.0.7

- Retargeted the library to `net8.0` and `net10.0`.
- Removed the `DatabaseWrapper` dependency in favor of native SQL Server, MySQL, PostgreSQL, and SQLite implementations.
- Added the RestDb dashboard for browsing schemas, rows, and table operations from a browser workspace.
- Added runtime management APIs for `restdb.json` and `context.json`, including dashboard editors for server settings, global context, database context, and table context.
- Added `_context` enrichment on database and table metadata retrieval routes so callers can inline database and table context into schema responses.
- Added `RestDb.McpServer` to expose the RestDb API over MCP HTTP, TCP, WebSocket, and stdio transports.
- Migrated testing to a shared Touchstone suite with CLI, xUnit, and NUnit runners.
- Added live API validation against SQLite by default and Docker-backed MySQL, PostgreSQL, and SQL Server runs via `RestDb.Test.Automated`.
- Added direct MCP HTTP bridge coverage for the `/mcp` streamable-HTTP contract used by Codex and similar clients.
- Added bearer-token authentication support alongside the configured API key header.
- Fixed provider/runtime issues uncovered by the live test matrix, including filtered DELETE handling, MySQL `LIKE` behavior, and repeated dashboard metadata probes.

## Important Notes

- If you specify a listener other than `localhost` or `127.0.0.1`, you may have to run with elevated privileges.
- The HTTP HOST header MUST match the listener hostname, otherwise you'll get `Bad Request` errors back.
- By default, access to RestDb is **UNAUTHENTICATED**.  Configure `restdb.json` with API keys to enable authentication, and set the `RequireAuthentication` value to `true`.

## Running in Docker and Docker Compose

The easiest way to get started in Docker is to clone the repository and run the Compose stack in the `Docker` directory.
The provided `Docker/compose.yaml` builds the RestDb API, dashboard, and MCP server directly from the local source tree in this repository.
The README does not assume or require any prepublished RestDb images from Docker Hub.

```
git clone https://github.com/jchristn/restdb
cd restdb/Docker
docker compose build
docker compose up -d
```

If you change application code, Dockerfiles, or the dashboard, rerun `docker compose up --build -d` so the stack is rebuilt from your local checkout.

The Docker Compose stack exposes:

- RestDb API at `http://localhost:8000`
- RestDb Dashboard at `http://localhost:8080`
- RestDb MCP HTTP at `http://localhost:8010/mcp`
- RestDb MCP TCP at `tcp://localhost:8011`
- RestDb MCP WebSocket at `ws://localhost:8012/mcp`
- Sample SQLite database named `sample`
- Persistent `restdb.json` and `context.json` files mounted from the `Docker` directory

The bundled Docker configuration enables authentication with the API key `default`.

To connect from the dashboard:

1. Open `http://localhost:8080`
2. Enter `http://localhost:8000` as the server URL
3. Enter `default` as the API key
4. Use either `Custom header` with `x-api-key`, or `Bearer token`
5. After login, use the top-right icons to open the full `context.json` editor and `restdb.json` editor
6. Select a database or table to edit its context directly in the workspace

To stop the stack:

```
docker compose down
```

For route references and MCP tool mappings, see:

- [REST_API.md](REST_API.md)
- [MCP_API.md](MCP_API.md)

## Execution
  
```
> dotnet build src\RestDb\RestDb.csproj -c Debug -f net8.0
> cd src\RestDb\bin\Debug\net8.0
> dotnet RestDb.dll
```

`net10.0` builds are also supported; use `src\RestDb\bin\Debug\net10.0` if you build that target framework instead.

## Testing

Tests are organized around a shared Touchstone suite:

- `src/RestDb.Test.Shared` contains the actual test cases and assertions.
- `src/RestDb.Test.Automated` exposes the shared suite through the Touchstone CLI runner.
- `src/RestDb.Test.Xunit` exposes the shared suite through xUnit.
- `src/RestDb.Test.Nunit` exposes the shared suite through NUnit.

The shared suite covers:

- provider-specific query-builder generation across SQLite, PostgreSQL, SQL Server, and MySQL
- live REST API semantics against the selected provider
- MCP streamable-HTTP bridge behavior on `/mcp`, including `notifications/initialized -> 202`, `tools/list`, and the immediate SSE prelude expected by Codex-class clients

Default automated behavior uses a temporary SQLite database. To target another provider, pass connection details on the CLI or use `--docker` for MySQL, PostgreSQL, or SQL Server.
See [TESTING.md](TESTING.md) for direct live-database and Docker-backed examples.

```
> dotnet test src\RestDb.Test.Xunit\RestDb.Test.Xunit.csproj -c Debug
> dotnet test src\RestDb.Test.Nunit\RestDb.Test.Nunit.csproj -c Debug
> dotnet run --project src\RestDb.Test.Automated\RestDb.Test.Automated.csproj -c Debug -f net8.0
```

Example external provider run:

```
> dotnet run --project src\RestDb.Test.Automated\RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type postgresql --host localhost --port 5432 --database restdb --user postgres --pass password
```

Example Docker-backed provider run:

```
> dotnet run --project src\RestDb.Test.Automated\RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type mysql --docker
```

## Dashboard and Context

The dashboard supports:

- API-key header or bearer-token login
- database and table browsing
- row insert, update, delete, truncate, drop, and raw SQL
- editing database context and table context in-place
- editing and reloading the full `restdb.json` and `context.json` documents via modal editors

`context.json` is a shared multi-database metadata file shaped like:

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

In Docker Compose, both `Docker/restdb.json` and `Docker/context.json` are mounted into the running RestDb container so updates made through the API or dashboard persist on the host filesystem.

## MCP

`RestDb.McpServer` is included in the solution and Compose stack. It proxies every meaningful RestDb REST route over MCP so agents can work with the same behavior exposed by the HTTP API.

Common entry points:

- HTTP MCP: `http://localhost:8010/mcp`
- TCP MCP: `tcp://localhost:8011`
- WebSocket MCP: `ws://localhost:8012/mcp`
- stdio MCP: `dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- --stdio`

`RestDb.McpServer` also includes an installer for agent environments:

```
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --yes
```

Dry-run preview:

```
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --dry-run
```

The installer updates user-level MCP definitions for Claude Code, Codex, Gemini CLI, and Cursor. It only writes client-side MCP endpoint definitions. Configure protected RestDb downstream auth on the `RestDb.McpServer` process itself with `--api-key`, `--bearer-token`, or `RESTDB_MCP_*` environment variables.

See [ADD_TO_AGENTS.md](ADD_TO_AGENTS.md) for the full manual and automatic setup details.
  
## Setup
 
1) Start RestDb as described above.  You will be guided through a setup process to connect to your databases.  Alternatively, you can start with `Sqlite` which requires no database setup.

```
                 _      _ _
   _ __ ___  ___| |_ __| | |__
  | '__/ _ \/ __| __/ _  |  _ \
  | | |  __/\__ \ || (_| | |_) |
  |_|  \___||___/\__\__,_|_.__/


Listening for requests on http://localhost:8000

```

2) Verify Connectivity

Point your browser to `http://localhost:8000`.  You should see a default webpage for RestDb.

## Simple Examples

### List databases
```
GET http://localhost:8000/_databases

Resp:
200/OK
[
  "test"
]
```

### Create a Table
```
POST http://localhost:8000/test
Data: 
{
  "Name": "person",
  "PrimaryKey": "person_id",
  "Columns": [
    {
      "Name": "person_id",
      "Type": "int",
      "Nullable": false
    },
    {
      "Name": "first_name",
      "Type": "nvarchar",
      "MaxLength": 32,
      "Nullable": false
    },
    {
      "Name": "last_name",
      "Type": "nvarchar",
      "MaxLength": 32,
      "Nullable": true
    },
    {
      "Name": "age",
      "Type": "int",
      "Nullable": false
    },
    {
      "Name": "created",
      "Type": "datetime",
      "Nullable": true
    }
  ]
}

Resp:
201/Created
```

### Retrieve a Database
```
GET http://localhost:8000/test

Resp:
200/OK
{
  "Name": "test",
  "Type": "mssql",
  "Hostname": "localhost",
  "Port": 1433,
  "Instance": "sqlexpress",
  "Debug": false,
  "TableNames": [
    "person"
  ]
}
```

### Retrieve a Database with Context
```
GET http://localhost:8000/sample?_context=true

Resp:
200/OK
{
  "Name": "sample",
  "Type": "Sqlite",
  "Debug": false,
  "TableNames": [
    "department",
    "person",
    "project",
    "task"
  ],
  "Context": "This SQLite database contains example business data used to explore RestDb and the dashboard.",
  "TableContexts": {
    "department": "Reference table for departments. Primary key is department_id.",
    "person": "Stores people records. Primary key is person_id and department_id relates each person to a department.",
    "project": "Stores projects owned by departments. Primary key is project_id and department_id links each project to a department.",
    "task": "Stores task records assigned to people and projects. Primary key is task_id, with project_id and person_id describing ownership and assignment."
  }
}
```

### Describe a Table
```
GET http://localhost:8000/test/person?_describe

Resp:
200/OK
{
  "Name": "person",
  "PrimaryKey": "person_id",
  "Columns": [
    {
      "Name": "person_id",
      "Type": "int",
      "Nullable": false
    },
    {
      "Name": "first_name",
      "Type": "nvarchar",
      "MaxLength": 32,
      "Nullable": false
    },
    {
      "Name": "last_name",
      "Type": "nvarchar",
      "MaxLength": 32,
      "Nullable": true
    },
    {
      "Name": "age",
      "Type": "int",
      "Nullable": false
    },
    {
      "Name": "created",
      "Type": "datetime",
      "Nullable": true
    }
  ]
}
```

### Create an Object

Be sure to use timestamps appropriate to your database type, for instance:

- MsSql:  MM/dd/yyyy HH:mm:ss
- MySql:  yyyy-MM-dd HH:mm:ss
- PgSql:  MM/dd/yyyy HH:mm:ss
- Sqlite: yyyy-MM-dd HH:mm:ss

```
POST http://localhost:8000/test/person
Data: { first_name: 'joel', last_name: 'christner', age: 40, created: '05/03/2017' }

Resp:
201/Created
{
  "person_id": 1,
  "first_name": "joel",
  "last_name": "christner",
  "age": 40,
  "created": "05/03/2017 00:00:00"
}
```

### Create Multiple Objects

To create multiple objects, send a JSON array containing a series of dictionaries appropriate for the specified table.

```
POST http://localhost:8080/test/person?_multiple
Data: [ { first_name: 'person1', last_name: 'last', age: 50, created '5/1/2017' }, ... ]

Resp:
201/Created
```

### Retrieve Objects

You can retrieve all of a table's contents, retrieve by a specific ID, and filter by key-value pairs (using the querystring). 

```
GET http://localhost:8000/test/person 
GET http://localhost:8000/test/person/1
GET http://localhost:8000/test/person?first_name=joel 

Resp:
200/OK
[
  {
    "person_id": 1,
    "first_name": "joel",
    "last_name": "christner",
    "age": 18,
    "created": "1990-04-23T00:00:00Z"
  }, 
  { ... }
]
```

### Retrieve Objects with Pagination

You can retrieve results and use pagination to return only a subset.  Include `_index`, `_max`, `_order`, and  `_order_by` in the querystring.  

- `_index` is the starting index
- `_max` is the maximum number of results to retrieve
- `_order` must be either `asc` (ascending) or `desc` (descending).
- `_order_by` is one or more column names in a comma-separated list.

By default, `SELECT` requests are ordered ASCENDING by the table's primary key.

```
GET http://localhost:8000/test/person?_max=1&_index=1&_order=asc&_order_by=person_id,first_name

Resp:
200/OK
[
  {
    "__row_num__": 1,
    "person_id": 1,
    "first_name": "joel",
    "last_name": "christner",
    "age": 40,
    "created": "05/03/2017 00:00:00"
  },
  { ... }
]
```

### Update an Object

Supply the ID in the URL and include the key-value pairs to change in the request body.

```
PUT http://localhost:8000/test/person/1
Data: { age: 18 }

Resp:
200/OK
{
  "person_id": 1,
  "first_name": "joel",
  "last_name": "christner",
  "age": 18,
  "created": "05/03/2017 00:00:00"
}
```

### Search for an Object

Uses `ExpressionTree.Expr` JSON payloads.  Each condition uses `Left`, `Operator`, and `Right`, and logical operators such as `And` and `Or` can nest expressions recursively.  Supported operators are `Equals`, `NotEquals`, `GreaterThan`, `GreaterThanOrEqualTo`, `LessThan`, `LessThanOrEqualTo`, `IsNull`, `IsNotNull`, `Contains`, `ContainsNot`, `StartsWith`, `StartsWithNot`, `EndsWith`, `EndsWithNot`, `In`, `NotIn`, `And`, and `Or`.

```
PUT http://localhost:8000/test/person
Data: 
{
  "Left": "person_id",
  "Operator": "GreaterThan",
  "Right": 0
}

Resp:
200/OK
[
  {
    "person_id": 1,
    "first_name": "joel",
    "last_name": "christner",
    "age": 18,
    "created": "05/03/2017 00:00:00"
  }
]
```

### Delete an Object
```
DELETE http://localhost:8000/test/person/1

Resp: 
204/Deleted
```

### Truncating a Table
```
DELETE http://localhost:8000/test/person?_truncate

Resp: 
204/Deleted
```

### Dropping a Table
```
DELETE http://localhost:8000/test/person?_drop

Resp:
204/Deleted
```

### Executing a Raw Query
```
POST http://localhost:8000/test?raw
Data:
SELECT * FROM person;

Resp:
200/OK 
[
  {
    "person_id": 1,
    "first_name": "joel",
    "last_name": "christner",
    "age": 18,
    "created": "05/03/2017 00:00:00"
  }
]
```

## Enabling Authentication

To enable authentication, set `Server.RequireAuthentication` to `true` and specify an API key header in `Server.ApiKeyHeader` in the `restdb.json` file. Then, add a section called `ApiKeys` with each of the keys you wish to allow or disallow. RestDb accepts either:

- the configured header, for example `x-api-key: default`
- `Authorization: Bearer default`

An example with one API key is below.

```
{
  "Version": "1.1.0",
  "Server": {
    "ListenerHostname": "localhost",
    "ListenerPort": 8000,
    "Ssl": false,
    "Debug": false,
    "RequireAuthentication": true,
    "ApiKeyHeader": "x-api-key"
  },
  "Logging": { 
    "ServerIp": "127.0.0.1",
    "ServerPort": 514,
    "MinimumLevel": 1,
    "LogHttpRequests": false,
    "LogHttpResponses": false,
    "ConsoleLogging": true
  },
  "Databases": [
    {
      "Name": "test",
      "Type": "MsSql",
      "Hostname": "localhost",
      "Port": 1433,
      "Instance": "SQLEXPRESS",
      "Username": "root",
      "Password": "password",
      "Debug": false
    }
  ],
  "ApiKeys": [
    {
      "Key": "default",
      "AllowGet": true,
      "AllowPost": true,
      "AllowPut": true,
      "AllowDelete": true
    }
  ]
}
```

To inline context into metadata responses, add `?_context=true`:

- `GET /sample?_context=true`
- `GET /sample?_describe=true&_context=true`
- `GET /sample/person?_describe=true&_context=true`

Example authenticated requests:

```
GET http://localhost:8000/_databases
x-api-key: default
```

```
GET http://localhost:8000/_databases
Authorization: Bearer default
```

## Postman Collection

The included `RestDb.postman_collection.json` is organized into:

- `System Operations` for connectivity, auth, top-level metadata, runtime `restdb.json` management, and runtime `context.json` management
- `Database Operations` for database metadata, schema, record retrieval, record mutation, and raw SQL

Default collection variables assume the Docker Compose stack:

- `baseUrl = http://localhost:8000`
- `apiKey = default`
- `apiKeyHeader = x-api-key`
- `databaseName = sample`
- `tableName = person`
- `rowId = 1`

With the default variables, the read-oriented requests work immediately against the bundled `sample.person` table. Create, update, delete, truncate, and drop requests target `{{tableName}}` directly, so point `tableName` at the table you actually want to manage before running destructive requests.

The Postman collection includes the runtime configuration and context routes as well:

- `GET` / `PUT` / `POST reload` for `/_settings`
- `GET` / `PUT` / `POST reload` for `/_context`
- database and table context requests under `/_context/{database}` and `/_context/{database}/{table}`

The collection defines collection-level API-key authentication using `{{apiKeyHeader}}: {{apiKey}}`. If you prefer bearer auth, change the collection authorization type to `Bearer Token` and set the token to `{{apiKey}}`.

The metadata requests in `Database Operations` use `_context=true` so the database and table context values are visible in the default responses.

## Version History

Please refer to CHANGELOG.md for details.
