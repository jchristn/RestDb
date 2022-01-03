# RestDb

RESTful HTTP/HTTPS server for Microsoft SQL Server, MySQL, and PostgreSQL database tables written in C#.  

## Description

RestDb spawns a RESTful HTTP/HTTPS server that exposes a series of APIs allowing you to perform SELECT, INSERT, UPDATE, DELETE, TRUNCATE, and DROP against tables in Microsoft SQL Server, MySQL, PostgreSQL, and Sqlite.
 
## New in v2.0.1

- Breaking change caused by dependency updates
- Multiple insert API
- Internal refactor
- More complete Postman environment
- Error codes

## Important Notes

- If you specify a listener other than ```localhost``` or ```127.0.0.1```, you may have to run with elevated privileges.
- The HTTP HOST header MUST match the listener hostname, otherwise you'll get ```Bad Request``` errors back.
- By default, access to RestDb is **UNAUTHENTICATED**.  Configure ```system.json``` with API keys to enable authentication, and set the ```RequireAuthentication``` value to ```true```.

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

You can retrieve results and use pagination to return only a subset.  Include ```_index```, ```_max```, ```_order```, and  ```_order_by``` in the querystring.  

- ```_index``` is the starting index
- ```_max``` is the maximum number of results to retrieve
- ```_order``` must be either ```asc``` (ascending) or ```desc``` (descending).
- ```_order_by``` is one or more column names in a comma-separated list.

By default, ```SELECT``` requests are ordered ASCENDING by the table's primary key.

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

Uses the Expression syntax found in DatabaseWrapper (refer to examples here: https://github.com/jchristn/DatabaseWrapper).  These can be nested in a hierarchical manner.

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
