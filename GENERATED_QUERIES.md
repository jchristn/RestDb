# Generated Queries

This file captures exact example SQL emitted by RestDb's native query builders for every SQL-emitting route shape across all supported database types:

- SQLite
- PostgreSQL
- SQL Server
- MySQL

These examples correspond to the route-shape validation covered in [`src/RestDb.Test.Shared/ProviderQueryBuilderAssertions.cs`](C:/Code/Misc/RestDb-2.0/src/RestDb.Test.Shared/ProviderQueryBuilderAssertions.cs).

## Shared Sample Inputs

Sample table schema used for the generated examples:

```json
{
  "Name": "person",
  "PrimaryKey": "person_id",
  "Columns": [
    { "Name": "person_id", "Type": "int", "Nullable": false, "PrimaryKey": true },
    { "Name": "first_name", "Type": "nvarchar", "MaxLength": 32, "Nullable": false },
    { "Name": "last_name", "Type": "nvarchar", "MaxLength": 32, "Nullable": true },
    { "Name": "age", "Type": "int", "Nullable": false },
    { "Name": "created", "Type": "datetime", "Nullable": true }
  ]
}
```

Additional sample inputs:

- Default page size when the route does not override `_max`: `100`
- Projected `GET /{db}/{table}` fields: `person_id,first_name`
- Paged `GET /{db}/{table}` filter: `age >= 18 AND first_name = 'joel'`
- `GET /{db}/{table}/{id}` sample id: `7`
- `GET /{db}/{table}/{id}` plus query filter: `first_name = 'joel' AND person_id = 7`
- `PUT /{db}/{table}` search filter: `age IN (18,19) OR (created IS NOT NULL AND last_name STARTS WITH 'Chr')`
- `PUT /{db}/{table}` search plus query filters: `last_name = 'christner' AND first_name = 'joel' AND age >= 18`
- `POST /{db}/{table}` insert payload:

```json
{
  "first_name": "joel",
  "last_name": "christner",
  "age": 40,
  "created": "2024-01-01 00:00:00"
}
```

- `POST /{db}/{table}?_multiple` payloads:

```json
[
  {
    "first_name": "joel",
    "last_name": "christner",
    "age": 40,
    "created": "2024-01-01 00:00:00"
  },
  {
    "first_name": "jane",
    "last_name": "doe",
    "age": 35,
    "created": "2024-01-02 00:00:00"
  }
]
```

- `PUT /{db}/{table}/{id}` update payload:

```json
{
  "age": 18
}
```

- `DELETE /{db}/{table}` filter sample: `age = 18 AND first_name = 'joel'`
- `DELETE /{db}/{table}/{id}` sample id: `1`
- `DELETE /{db}/{table}/{id}` plus query filter: `first_name = 'joel' AND person_id = 1`
- `POST /{db}?raw` sample SQL: `SELECT * FROM person;`

## Parameter Convention

All generated parameter placeholders use `@pN`. That is valid for the provider packages used by RestDb:

- `Microsoft.Data.Sqlite`
- `Npgsql`
- `Microsoft.Data.SqlClient`
- `MySqlConnector`

## Route Shapes Covered

- `GET /{db}`
- `GET /{db}?_describe`
- `GET /{db}/{table}?_describe`
- `GET /{db}/{table}` default unfiltered select
- `GET /{db}/{table}` filtered, projected, ordered, paged select
- `GET /{db}/{table}/{id}`
- `GET /{db}/{table}/{id}` plus querystring filters
- `PUT /{db}/{table}` body expression search
- `PUT /{db}/{table}` body expression plus querystring filters
- `PUT /{db}/{table}/{id}`
- `POST /{db}`
- `POST /{db}/{table}`
- `POST /{db}/{table}?_multiple`
- `DELETE /{db}/{table}` unfiltered
- `DELETE /{db}/{table}` querystring filters
- `DELETE /{db}/{table}/{id}`
- `DELETE /{db}/{table}/{id}` plus querystring filters
- `DELETE /{db}/{table}?_truncate`
- `DELETE /{db}/{table}?_drop`
- `POST /{db}?raw`

## SQLite

Validation basis:

- Double-quoted identifiers are valid.
- Table discovery via `sqlite_master` is valid.
- Schema discovery via `pragma_table_info(...)` is valid.
- `LIMIT` and `OFFSET` syntax is valid.
- `last_insert_rowid()` is valid for inserted-row readback.
- Table clear uses `DELETE FROM`, which is valid SQLite syntax because SQLite has no `TRUNCATE TABLE`.

### GET /{db}

```sql
SELECT name AS table_name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;
```

Parameters: none

### GET /{db}?_describe

List step:

```sql
SELECT name AS table_name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;
```

Describe step for `person`:

```sql
SELECT name AS column_name, type AS data_type, CASE WHEN "notnull" = 0 THEN 1 ELSE 0 END AS is_nullable, NULL AS max_length, CASE WHEN pk > 0 THEN 1 ELSE 0 END AS primary_key FROM pragma_table_info('person') ORDER BY cid;
```

Parameters: none

### GET /{db}/{table}?_describe

```sql
SELECT name AS column_name, type AS data_type, CASE WHEN "notnull" = 0 THEN 1 ELSE 0 END AS is_nullable, NULL AS max_length, CASE WHEN pk > 0 THEN 1 ELSE 0 END AS primary_key FROM pragma_table_info('person') ORDER BY cid;
```

Parameters: none

### GET /{db}/{table}

```sql
SELECT * FROM "person" ORDER BY "person_id" ASC LIMIT @p0;
```

Parameters:

- `@p0 = 100`

### GET /{db}/{table} with filters, projection, ordering, and paging

```sql
SELECT "person_id", "first_name" FROM "person" WHERE (("age" >= @p0) AND ("first_name" = @p1)) ORDER BY "person_id" DESC, "first_name" ASC LIMIT @p2;
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`
- `@p2 = 25`

### GET /{db}/{table}/{id}

```sql
SELECT * FROM "person" WHERE ("person_id" = @p0) ORDER BY "person_id" ASC LIMIT @p1;
```

Parameters:

- `@p0 = 7`
- `@p1 = 100`

### GET /{db}/{table}/{id} plus querystring filters

```sql
SELECT * FROM "person" WHERE (("first_name" = @p0) AND ("person_id" = @p1)) ORDER BY "person_id" ASC LIMIT @p2;
```

Parameters:

- `@p0 = joel`
- `@p1 = 7`
- `@p2 = 100`

### PUT /{db}/{table} body expression search

```sql
SELECT * FROM "person" WHERE (("age" IN (@p0, @p1)) OR (("created" IS NOT NULL) AND ("last_name" LIKE @p2 ESCAPE '\'))) ORDER BY "person_id" ASC LIMIT @p3 OFFSET @p4;
```

Parameters:

- `@p0 = 18`
- `@p1 = 19`
- `@p2 = Chr%`
- `@p3 = 10`
- `@p4 = 1`

### PUT /{db}/{table} body expression plus querystring filters

```sql
SELECT * FROM "person" WHERE (("last_name" = @p0) AND (("first_name" = @p1) AND ("age" >= @p2))) ORDER BY "person_id" ASC LIMIT @p3;
```

Parameters:

- `@p0 = christner`
- `@p1 = joel`
- `@p2 = 18`
- `@p3 = 100`

### PUT /{db}/{table}/{id}

```sql
UPDATE "person" SET "age" = @p0 WHERE ("person_id" = @p1);
```

Parameters:

- `@p0 = 18`
- `@p1 = 1`

### POST /{db}

```sql
CREATE TABLE IF NOT EXISTS "person" ("person_id" INTEGER PRIMARY KEY AUTOINCREMENT, "first_name" VARCHAR(32) NOT NULL, "last_name" VARCHAR(32), "age" INTEGER NOT NULL, "created" TEXT);
```

Parameters: none

### POST /{db}/{table}

Query 1:

```sql
INSERT INTO "person" ("first_name", "last_name", "age", "created") VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

Query 2:

```sql
SELECT * FROM "person" WHERE "person_id" = last_insert_rowid();
```

Parameters: none

### POST /{db}/{table}?_multiple

Query 1:

```sql
INSERT INTO "person" ("first_name", "last_name", "age", "created") VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

Query 2:

```sql
INSERT INTO "person" ("first_name", "last_name", "age", "created") VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = jane`
- `@p1 = doe`
- `@p2 = 35`
- `@p3 = 2024-01-02 00:00:00`

### DELETE /{db}/{table}

```sql
DELETE FROM "person";
```

Parameters: none

### DELETE /{db}/{table} with querystring filters

```sql
DELETE FROM "person" WHERE (("age" = @p0) AND ("first_name" = @p1));
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`

### DELETE /{db}/{table}/{id}

```sql
DELETE FROM "person" WHERE ("person_id" = @p0);
```

Parameters:

- `@p0 = 1`

### DELETE /{db}/{table}/{id} plus querystring filters

```sql
DELETE FROM "person" WHERE (("first_name" = @p0) AND ("person_id" = @p1));
```

Parameters:

- `@p0 = joel`
- `@p1 = 1`

### DELETE /{db}/{table}?_truncate

```sql
DELETE FROM "person";
```

Parameters: none

### DELETE /{db}/{table}?_drop

```sql
DROP TABLE IF EXISTS "person";
```

Parameters: none

### POST /{db}?raw

```sql
SELECT * FROM person;
```

Parameters: none

## PostgreSQL

Validation basis:

- Double-quoted identifiers are valid.
- Metadata discovery through `information_schema` and `current_schema()` is valid.
- `LIMIT` and `OFFSET` syntax is valid.
- `RETURNING *` is valid for inserted-row readback.
- `TRUNCATE TABLE ... RESTART IDENTITY` is valid.

### GET /{db}

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' ORDER BY table_name;
```

Parameters: none

### GET /{db}?_describe

List step:

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' ORDER BY table_name;
```

Describe step for `person`:

```sql
SELECT c.column_name, c.data_type, CASE WHEN c.is_nullable = 'YES' THEN TRUE ELSE FALSE END AS is_nullable, c.character_maximum_length AS max_length, CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN TRUE ELSE FALSE END AS primary_key FROM information_schema.columns c LEFT JOIN information_schema.key_column_usage kcu ON c.table_schema = kcu.table_schema AND c.table_name = kcu.table_name AND c.column_name = kcu.column_name LEFT JOIN information_schema.table_constraints tc ON kcu.constraint_schema = tc.constraint_schema AND kcu.constraint_name = tc.constraint_name AND tc.constraint_type = 'PRIMARY KEY' WHERE c.table_schema = current_schema() AND c.table_name = @p0 ORDER BY c.ordinal_position;
```

Parameters:

- `@p0 = person`

### GET /{db}/{table}?_describe

```sql
SELECT c.column_name, c.data_type, CASE WHEN c.is_nullable = 'YES' THEN TRUE ELSE FALSE END AS is_nullable, c.character_maximum_length AS max_length, CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN TRUE ELSE FALSE END AS primary_key FROM information_schema.columns c LEFT JOIN information_schema.key_column_usage kcu ON c.table_schema = kcu.table_schema AND c.table_name = kcu.table_name AND c.column_name = kcu.column_name LEFT JOIN information_schema.table_constraints tc ON kcu.constraint_schema = tc.constraint_schema AND kcu.constraint_name = tc.constraint_name AND tc.constraint_type = 'PRIMARY KEY' WHERE c.table_schema = current_schema() AND c.table_name = @p0 ORDER BY c.ordinal_position;
```

Parameters:

- `@p0 = person`

### GET /{db}/{table}

```sql
SELECT * FROM "person" ORDER BY "person_id" ASC LIMIT @p0;
```

Parameters:

- `@p0 = 100`

### GET /{db}/{table} with filters, projection, ordering, and paging

```sql
SELECT "person_id", "first_name" FROM "person" WHERE (("age" >= @p0) AND ("first_name" = @p1)) ORDER BY "person_id" DESC, "first_name" ASC LIMIT @p2;
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`
- `@p2 = 25`

### GET /{db}/{table}/{id}

```sql
SELECT * FROM "person" WHERE ("person_id" = @p0) ORDER BY "person_id" ASC LIMIT @p1;
```

Parameters:

- `@p0 = 7`
- `@p1 = 100`

### GET /{db}/{table}/{id} plus querystring filters

```sql
SELECT * FROM "person" WHERE (("first_name" = @p0) AND ("person_id" = @p1)) ORDER BY "person_id" ASC LIMIT @p2;
```

Parameters:

- `@p0 = joel`
- `@p1 = 7`
- `@p2 = 100`

### PUT /{db}/{table} body expression search

```sql
SELECT * FROM "person" WHERE (("age" IN (@p0, @p1)) OR (("created" IS NOT NULL) AND ("last_name" LIKE @p2 ESCAPE '\'))) ORDER BY "person_id" ASC LIMIT @p3 OFFSET @p4;
```

Parameters:

- `@p0 = 18`
- `@p1 = 19`
- `@p2 = Chr%`
- `@p3 = 10`
- `@p4 = 1`

### PUT /{db}/{table} body expression plus querystring filters

```sql
SELECT * FROM "person" WHERE (("last_name" = @p0) AND (("first_name" = @p1) AND ("age" >= @p2))) ORDER BY "person_id" ASC LIMIT @p3;
```

Parameters:

- `@p0 = christner`
- `@p1 = joel`
- `@p2 = 18`
- `@p3 = 100`

### PUT /{db}/{table}/{id}

```sql
UPDATE "person" SET "age" = @p0 WHERE ("person_id" = @p1);
```

Parameters:

- `@p0 = 18`
- `@p1 = 1`

### POST /{db}

```sql
CREATE TABLE IF NOT EXISTS "person" ("person_id" INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY, "first_name" VARCHAR(32) NOT NULL, "last_name" VARCHAR(32), "age" INTEGER NOT NULL, "created" TIMESTAMP);
```

Parameters: none

### POST /{db}/{table}

```sql
INSERT INTO "person" ("first_name", "last_name", "age", "created") VALUES (@p0, @p1, @p2, @p3) RETURNING *;
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

### POST /{db}/{table}?_multiple

Query 1:

```sql
INSERT INTO "person" ("first_name", "last_name", "age", "created") VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

Query 2:

```sql
INSERT INTO "person" ("first_name", "last_name", "age", "created") VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = jane`
- `@p1 = doe`
- `@p2 = 35`
- `@p3 = 2024-01-02 00:00:00`

### DELETE /{db}/{table}

```sql
DELETE FROM "person";
```

Parameters: none

### DELETE /{db}/{table} with querystring filters

```sql
DELETE FROM "person" WHERE (("age" = @p0) AND ("first_name" = @p1));
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`

### DELETE /{db}/{table}/{id}

```sql
DELETE FROM "person" WHERE ("person_id" = @p0);
```

Parameters:

- `@p0 = 1`

### DELETE /{db}/{table}/{id} plus querystring filters

```sql
DELETE FROM "person" WHERE (("first_name" = @p0) AND ("person_id" = @p1));
```

Parameters:

- `@p0 = joel`
- `@p1 = 1`

### DELETE /{db}/{table}?_truncate

```sql
TRUNCATE TABLE "person" RESTART IDENTITY;
```

Parameters: none

### DELETE /{db}/{table}?_drop

```sql
DROP TABLE IF EXISTS "person";
```

Parameters: none

### POST /{db}?raw

```sql
SELECT * FROM person;
```

Parameters: none

## SQL Server

Validation basis:

- Bracketed identifiers are valid.
- Metadata discovery through `INFORMATION_SCHEMA` is valid.
- `OFFSET ... FETCH NEXT ...` syntax is valid when paired with `ORDER BY`, which these route shapes always provide.
- `OUTPUT INSERTED.*` is valid for inserted-row readback.
- `TRUNCATE TABLE` and `DROP TABLE IF EXISTS` are valid SQL Server syntax on modern SQL Server versions.
- These metadata queries are valid database-wide queries; they are not schema-scoped.

### GET /{db}

```sql
SELECT TABLE_NAME AS table_name FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME;
```

Parameters: none

### GET /{db}?_describe

List step:

```sql
SELECT TABLE_NAME AS table_name FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME;
```

Describe step for `person`:

```sql
SELECT c.COLUMN_NAME AS column_name, c.DATA_TYPE AS data_type, CASE WHEN c.IS_NULLABLE = 'YES' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS is_nullable, c.CHARACTER_MAXIMUM_LENGTH AS max_length, CASE WHEN tc.CONSTRAINT_TYPE = 'PRIMARY KEY' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS primary_key FROM INFORMATION_SCHEMA.COLUMNS c LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON c.TABLE_SCHEMA = kcu.TABLE_SCHEMA AND c.TABLE_NAME = kcu.TABLE_NAME AND c.COLUMN_NAME = kcu.COLUMN_NAME LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA AND kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' WHERE c.TABLE_NAME = @p0 ORDER BY c.TABLE_SCHEMA, c.ORDINAL_POSITION;
```

Parameters:

- `@p0 = person`

### GET /{db}/{table}?_describe

```sql
SELECT c.COLUMN_NAME AS column_name, c.DATA_TYPE AS data_type, CASE WHEN c.IS_NULLABLE = 'YES' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS is_nullable, c.CHARACTER_MAXIMUM_LENGTH AS max_length, CASE WHEN tc.CONSTRAINT_TYPE = 'PRIMARY KEY' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS primary_key FROM INFORMATION_SCHEMA.COLUMNS c LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON c.TABLE_SCHEMA = kcu.TABLE_SCHEMA AND c.TABLE_NAME = kcu.TABLE_NAME AND c.COLUMN_NAME = kcu.COLUMN_NAME LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA AND kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' WHERE c.TABLE_NAME = @p0 ORDER BY c.TABLE_SCHEMA, c.ORDINAL_POSITION;
```

Parameters:

- `@p0 = person`

### GET /{db}/{table}

```sql
SELECT * FROM [person] ORDER BY [person_id] ASC OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY;
```

Parameters:

- `@p0 = 0`
- `@p1 = 100`

### GET /{db}/{table} with filters, projection, ordering, and paging

```sql
SELECT [person_id], [first_name] FROM [person] WHERE (([age] >= @p0) AND ([first_name] = @p1)) ORDER BY [person_id] DESC, [first_name] ASC OFFSET @p2 ROWS FETCH NEXT @p3 ROWS ONLY;
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`
- `@p2 = 0`
- `@p3 = 25`

### GET /{db}/{table}/{id}

```sql
SELECT * FROM [person] WHERE ([person_id] = @p0) ORDER BY [person_id] ASC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY;
```

Parameters:

- `@p0 = 7`
- `@p1 = 0`
- `@p2 = 100`

### GET /{db}/{table}/{id} plus querystring filters

```sql
SELECT * FROM [person] WHERE (([first_name] = @p0) AND ([person_id] = @p1)) ORDER BY [person_id] ASC OFFSET @p2 ROWS FETCH NEXT @p3 ROWS ONLY;
```

Parameters:

- `@p0 = joel`
- `@p1 = 7`
- `@p2 = 0`
- `@p3 = 100`

### PUT /{db}/{table} body expression search

```sql
SELECT * FROM [person] WHERE (([age] IN (@p0, @p1)) OR (([created] IS NOT NULL) AND ([last_name] LIKE @p2 ESCAPE '\'))) ORDER BY [person_id] ASC OFFSET @p3 ROWS FETCH NEXT @p4 ROWS ONLY;
```

Parameters:

- `@p0 = 18`
- `@p1 = 19`
- `@p2 = Chr%`
- `@p3 = 1`
- `@p4 = 10`

### PUT /{db}/{table} body expression plus querystring filters

```sql
SELECT * FROM [person] WHERE (([last_name] = @p0) AND (([first_name] = @p1) AND ([age] >= @p2))) ORDER BY [person_id] ASC OFFSET @p3 ROWS FETCH NEXT @p4 ROWS ONLY;
```

Parameters:

- `@p0 = christner`
- `@p1 = joel`
- `@p2 = 18`
- `@p3 = 0`
- `@p4 = 100`

### PUT /{db}/{table}/{id}

```sql
UPDATE [person] SET [age] = @p0 WHERE ([person_id] = @p1);
```

Parameters:

- `@p0 = 18`
- `@p1 = 1`

### POST /{db}

```sql
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'person') BEGIN CREATE TABLE [person] ([person_id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [first_name] NVARCHAR(32) NOT NULL, [last_name] NVARCHAR(32) NULL, [age] INT NOT NULL, [created] DATETIME2 NULL); END;
```

Parameters: none

### POST /{db}/{table}

```sql
INSERT INTO [person] ([first_name], [last_name], [age], [created]) OUTPUT INSERTED.* VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

### POST /{db}/{table}?_multiple

Query 1:

```sql
INSERT INTO [person] ([first_name], [last_name], [age], [created]) VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

Query 2:

```sql
INSERT INTO [person] ([first_name], [last_name], [age], [created]) VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = jane`
- `@p1 = doe`
- `@p2 = 35`
- `@p3 = 2024-01-02 00:00:00`

### DELETE /{db}/{table}

```sql
DELETE FROM [person];
```

Parameters: none

### DELETE /{db}/{table} with querystring filters

```sql
DELETE FROM [person] WHERE (([age] = @p0) AND ([first_name] = @p1));
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`

### DELETE /{db}/{table}/{id}

```sql
DELETE FROM [person] WHERE ([person_id] = @p0);
```

Parameters:

- `@p0 = 1`

### DELETE /{db}/{table}/{id} plus querystring filters

```sql
DELETE FROM [person] WHERE (([first_name] = @p0) AND ([person_id] = @p1));
```

Parameters:

- `@p0 = joel`
- `@p1 = 1`

### DELETE /{db}/{table}?_truncate

```sql
TRUNCATE TABLE [person];
```

Parameters: none

### DELETE /{db}/{table}?_drop

```sql
DROP TABLE IF EXISTS [person];
```

Parameters: none

### POST /{db}?raw

```sql
SELECT * FROM person;
```

Parameters: none

## MySQL

Validation basis:

- Backtick-quoted identifiers are valid.
- Metadata discovery through `information_schema` and `DATABASE()` is valid.
- `LIMIT` and `OFFSET` syntax is valid.
- `LAST_INSERT_ID()` is valid for inserted-row readback.
- `CREATE TABLE IF NOT EXISTS`, `TRUNCATE TABLE`, and `DROP TABLE IF EXISTS` are valid.

### GET /{db}

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE' ORDER BY table_name;
```

Parameters: none

### GET /{db}?_describe

List step:

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE' ORDER BY table_name;
```

Describe step for `person`:

```sql
SELECT c.column_name, c.data_type, CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END AS is_nullable, c.character_maximum_length AS max_length, CASE WHEN c.column_key = 'PRI' THEN 1 ELSE 0 END AS primary_key FROM information_schema.columns c WHERE c.table_schema = DATABASE() AND c.table_name = @p0 ORDER BY c.ordinal_position;
```

Parameters:

- `@p0 = person`

### GET /{db}/{table}?_describe

```sql
SELECT c.column_name, c.data_type, CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END AS is_nullable, c.character_maximum_length AS max_length, CASE WHEN c.column_key = 'PRI' THEN 1 ELSE 0 END AS primary_key FROM information_schema.columns c WHERE c.table_schema = DATABASE() AND c.table_name = @p0 ORDER BY c.ordinal_position;
```

Parameters:

- `@p0 = person`

### GET /{db}/{table}

```sql
SELECT * FROM `person` ORDER BY `person_id` ASC LIMIT @p0;
```

Parameters:

- `@p0 = 100`

### GET /{db}/{table} with filters, projection, ordering, and paging

```sql
SELECT `person_id`, `first_name` FROM `person` WHERE ((`age` >= @p0) AND (`first_name` = @p1)) ORDER BY `person_id` DESC, `first_name` ASC LIMIT @p2;
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`
- `@p2 = 25`

### GET /{db}/{table}/{id}

```sql
SELECT * FROM `person` WHERE (`person_id` = @p0) ORDER BY `person_id` ASC LIMIT @p1;
```

Parameters:

- `@p0 = 7`
- `@p1 = 100`

### GET /{db}/{table}/{id} plus querystring filters

```sql
SELECT * FROM `person` WHERE ((`first_name` = @p0) AND (`person_id` = @p1)) ORDER BY `person_id` ASC LIMIT @p2;
```

Parameters:

- `@p0 = joel`
- `@p1 = 7`
- `@p2 = 100`

### PUT /{db}/{table} body expression search

```sql
SELECT * FROM `person` WHERE ((`age` IN (@p0, @p1)) OR ((`created` IS NOT NULL) AND (`last_name` LIKE @p2 ESCAPE '\'))) ORDER BY `person_id` ASC LIMIT @p3 OFFSET @p4;
```

Parameters:

- `@p0 = 18`
- `@p1 = 19`
- `@p2 = Chr%`
- `@p3 = 10`
- `@p4 = 1`

### PUT /{db}/{table} body expression plus querystring filters

```sql
SELECT * FROM `person` WHERE ((`last_name` = @p0) AND ((`first_name` = @p1) AND (`age` >= @p2))) ORDER BY `person_id` ASC LIMIT @p3;
```

Parameters:

- `@p0 = christner`
- `@p1 = joel`
- `@p2 = 18`
- `@p3 = 100`

### PUT /{db}/{table}/{id}

```sql
UPDATE `person` SET `age` = @p0 WHERE (`person_id` = @p1);
```

Parameters:

- `@p0 = 18`
- `@p1 = 1`

### POST /{db}

```sql
CREATE TABLE IF NOT EXISTS `person` (`person_id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY, `first_name` VARCHAR(32) NOT NULL, `last_name` VARCHAR(32) NULL, `age` INT NOT NULL, `created` DATETIME NULL);
```

Parameters: none

### POST /{db}/{table}

Query 1:

```sql
INSERT INTO `person` (`first_name`, `last_name`, `age`, `created`) VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

Query 2:

```sql
SELECT * FROM `person` WHERE `person_id` = LAST_INSERT_ID();
```

Parameters: none

### POST /{db}/{table}?_multiple

Query 1:

```sql
INSERT INTO `person` (`first_name`, `last_name`, `age`, `created`) VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = joel`
- `@p1 = christner`
- `@p2 = 40`
- `@p3 = 2024-01-01 00:00:00`

Query 2:

```sql
INSERT INTO `person` (`first_name`, `last_name`, `age`, `created`) VALUES (@p0, @p1, @p2, @p3);
```

Parameters:

- `@p0 = jane`
- `@p1 = doe`
- `@p2 = 35`
- `@p3 = 2024-01-02 00:00:00`

### DELETE /{db}/{table}

```sql
DELETE FROM `person`;
```

Parameters: none

### DELETE /{db}/{table} with querystring filters

```sql
DELETE FROM `person` WHERE ((`age` = @p0) AND (`first_name` = @p1));
```

Parameters:

- `@p0 = 18`
- `@p1 = joel`

### DELETE /{db}/{table}/{id}

```sql
DELETE FROM `person` WHERE (`person_id` = @p0);
```

Parameters:

- `@p0 = 1`

### DELETE /{db}/{table}/{id} plus querystring filters

```sql
DELETE FROM `person` WHERE ((`first_name` = @p0) AND (`person_id` = @p1));
```

Parameters:

- `@p0 = joel`
- `@p1 = 1`

### DELETE /{db}/{table}?_truncate

```sql
TRUNCATE TABLE `person`;
```

Parameters: none

### DELETE /{db}/{table}?_drop

```sql
DROP TABLE IF EXISTS `person`;
```

Parameters: none

### POST /{db}?raw

```sql
SELECT * FROM person;
```

Parameters: none
