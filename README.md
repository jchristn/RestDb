# RestDb

RESTful HTTP/HTTPS server for Microsoft SQL Server, MySQL, and PostgreSQL database tables written in C#.  

## Description

RestDb spawns a RESTful HTTP/HTTPS server that exposes a series of APIs allowing you to perform SELECT, INSERT, UPDATE, DELETE, and TRUNCATE against tables in Microsoft SQL Server, MySQL, and PostgreSQL.
 
## New in v1.2.7

- .NET 5 support
- Dependency update
- Change to pagination

## Important Notes

- If you specify a listener other than ```localhost``` or ```127.0.0.1```, you may have to run with elevated privileges.
- The HTTP HOST header MUST match the listener hostname, otherwise you'll get ```Bad Request``` errors back.
- By default, access to RestDb is UNAUTHENTICATED.  Configure ```System.json``` with API keys to enable authentication, and set the ```RequireAuthentication``` value to ```true```.

## Execution
  
```
> cd RestDb\bin\debug\netcoreapp2.2
> dotnet RestDb.dll
```
  
## Setup
 
1) Start RestDb as described above.  You will be guided through a setup process to connect to your databases.  Alternatively, you can start with ```Sqlite``` which requires no database setup.

```
                 _      _ _
   _ __ ___  ___| |_ __| | |__
  | '__/ _ \/ __| __/ _  |  _ \
  | | |  __/\__ \ || (_| | |_) |
  |_|  \___||___/\__\__,_|_.__/


Listening for requests on http://localhost:8000

```

2) Verify Connectivity

Point your browser to http://localhost:8000.  You should see a default webpage for RestDb.

## Simple Examples

### List databases
```
GET http://localhost:8000/_databases
Resp:
[
  "test"
]
```

### Create a Table
```
POST http://localhost:8000/test
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

### Retrieve a Database
```
GET http://localhost:8000/test
Resp:
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

### Describe a Table
```
GET http://localhost:8000/test/person?_describe
Resp:
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
{
  "person_id": 1,
  "first_name": "joel",
  "last_name": "christner",
  "age": 40,
  "created": "05/03/2017 00:00:00"
}
```

### Retrieve Objects

You can retrieve all of a table's contents, retrieve by a specific ID, and filter by key-value pairs (using the querystring). 

```
GET http://localhost:8000/test/person 
GET http://localhost:8000/test/person/1
GET http://localhost:8000/test/person?first_name=joel 
[
  {
    "person_id": 1,
    "first_name": "joel",
    "last_name": "christner",
    "age": 18,
    "created": "1977-04-23T00:00:00Z"
  }, 
  { ... }
]
```

### Retrieve Objects with Pagination

You can retrieve results and use pagination to return only a subset.  Include ```_index_start```, ```_order```, and  ```_order_by``` in the querystring.  

```_order``` must be either ```asc``` (ascending) or ```desc``` (descending).

```_order_by``` is one or more column names in a comma-separated list.

```
GET http://localhost:8000/test/person?_max_results=1&_index_start=1&_order=asc&_order_by=person_id,first_name
Resp:
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
{
  "person_id": 1,
  "first_name": "joel",
  "last_name": "christner",
  "age": 18,
  "created": "05/03/2017 00:00:00"
}
```

### Search for an Object

Uses the Expression syntax found in DatabaseWrapper (refer to examples here: https://github.com/jchristn/DatabaseWrapper).  These can be nested in a hierarchical manner.

```
PUT http://localhost:8000/test/person
Data: 
{
  LeftTerm: "person_id",
  Operator: "GreaterThan",
  RightTerm: 0
}
Resp:
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
Resp: 200/OK (no data)
```

### Truncating a Table
```
DELETE http://localhost:8000/test/person?_truncate
```

### Dropping a Table
```
DELETE http://localhost:8000/test/person?_drop
```

### Executing a Raw Query
```
POST http://localhost:8000/test?raw
Data:
SELECT * FROM person;
Resp: 
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

To enable authentication, set ```Server.RequireAuthentication``` to ```true``` and specify an API key header in ```Server.ApiKeyHeader``` in the ```System.Json``` file.  Then, add a section called ```ApiKeys``` with each of the keys you wish to allow or disallow.  An example with one API key is below.

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

## Version History

Please refer to CHANGELOG.md for details.
