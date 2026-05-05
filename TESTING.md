# Testing

RestDb testing is organized into four projects:

- `src/RestDb.Test.Shared`: the shared Touchstone suites and assertions.
- `src/RestDb.Test.Automated`: the console runner for direct live database and Docker-backed runs.
- `src/RestDb.Test.Xunit`: xUnit host over the shared suites.
- `src/RestDb.Test.Nunit`: NUnit host over the shared suites.

If you need to point tests at a specific database engine or a specific database instance, use `RestDb.Test.Automated`.

## Test Surfaces

Default local runs:

```powershell
dotnet test src/RestDb.Test.Xunit/RestDb.Test.Xunit.csproj -c Debug
dotnet test src/RestDb.Test.Nunit/RestDb.Test.Nunit.csproj -c Debug
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0
```

The automated runner defaults to a temporary SQLite database when no provider arguments are supplied.

## Test Against Your Own Database or File

These commands run the full shared suite, including the live CRUD integration flow, against the database you specify.

Notes:

- The live suite creates and drops its own uniquely named test table.
- It does not drop your database.
- SQLite accepts either a specific file path or no file path. If no file path is provided, the runner creates a temporary file.

### SQLite File

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type sqlite --filename .\test.db
```

### MySQL

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type mysql --host 127.0.0.1 --port 3306 --user root --pass password --dbname testdb
```

### PostgreSQL

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type postgresql --host 127.0.0.1 --port 5432 --user postgres --pass password --dbname testdb
```

### SQL Server

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type sqlserver --host 127.0.0.1 --port 1433 --user sa --pass "RestDb!Pass123" --dbname testdb
```

## Test a Specific Database Type Using Docker

`--docker` provisions a disposable container for `mysql`, `postgresql`, or `sqlserver`, waits for readiness, runs the suite, and removes the container when the run completes.

If you omit `--port`, Docker publishes the provider port to a random available local port. The runner prints the resolved port before tests start.

If you want a longer-lived provider container that you control yourself, use the helper assets in `Docker/`. The `test-compose-up` scripts wait until the selected provider is actually ready:

```powershell
cd Docker
./test-compose-up.sh mysql
./test-compose-up.sh postgresql
./test-compose-up.sh sqlserver
./test-compose-down.sh
```

On Windows:

```powershell
cd Docker
test-compose-up.bat mysql
test-compose-up.bat postgresql
test-compose-up.bat sqlserver
test-compose-down.bat
```

Those assets expose the default ports and credentials documented below, so you can point `RestDb.Test.Automated` at them with explicit `--host`, `--port`, `--user`, `--pass`, and `--dbname` arguments.

If one of the default Docker ports is already in use, override it before starting the stack:

```powershell
$env:RESTDB_TEST_MYSQL_PORT="13306"
$env:RESTDB_TEST_POSTGRESQL_PORT="15432"
$env:RESTDB_TEST_SQLSERVER_PORT="11433"
```

### MySQL via Docker

Minimal:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type mysql --docker
```

Explicit database and credentials:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type mysql --docker --dbname testdb --user root --pass password
```

### PostgreSQL via Docker

Minimal:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type postgresql --docker
```

Explicit database and credentials:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type postgresql --docker --dbname testdb --user postgres --pass password
```

### SQL Server via Docker

Minimal:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type sqlserver --docker
```

Explicit database and login:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type sqlserver --docker --dbname testdb --user sa --pass "RestDb!Pass123"
```

SQL Server note:

- If you use `sa` with `--docker`, the password must satisfy SQL Server password complexity requirements.
- If you specify a non-`sa` login, the runner creates the database and login for you inside the container.

## Docker Options

Additional runner options for Docker-backed testing:

- `--keep-docker`: leave the container running after the test run.
- `--docker-image <image>`: override the default provider image.
- `--port <port>`: bind the provider container to a specific local port instead of an automatically assigned one.

Examples:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type mysql --docker --keep-docker
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type postgresql --docker --docker-image postgres:17
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type sqlserver --docker --port 14333
```

## Results Output

To write Touchstone JSON results to disk:

```powershell
dotnet run --project src/RestDb.Test.Automated/RestDb.Test.Automated.csproj -c Debug -f net8.0 -- --type mysql --docker --results .\touchstone-results.json
```
