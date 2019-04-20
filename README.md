# RestDb

RESTful HTTP/HTTPS server for Microsoft SQL Server, MySQL, and PostgreSQL database tables written in C#.  

## Description
RestDb spawns a RESTful HTTP/HTTPS server that exposes a series of APIs allowing you to perform SELECT, INSERT, UPDATE, DELETE, and TRUNCATE against tables in Microsoft SQL Server, MySQL, and PostgreSQL.
 
## New in v1.0.3
- Retarget to .NET Core and .NET Framework

## Important Notes
- If you specify a listener other than ```localhost``` or ```127.0.0.1```, you may have to run with elevated privileges.
- The HTTP HOST header MUST match the listener hostname, otherwise you'll get ```Bad Request``` errors back.
- By default, access to RestDb is UNAUTHENTICATED.  Configure ```System.json``` with API keys to enable authentication, and set the ```RequireAuthentication``` value to ```true```.

## Execution
In Windows, using .NET Framework
```
> cd RestDb\bin\debug\net462
> RestDb.exe
```

In Windows, using .NET Core
```
> cd RestDb\bin\debug\netcoreapp2.2
> dotnet RestDb.dll
```

In Linux/Mac, using .NET Core
```
$ cd RestDb/bin/debug/netcoreapp2.2
$ dotnet RestDb.dll
```

In Mono with .NET Framework environments, you should run the Mono AOT.
```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server RestDb.exe
mono --server RestDb.exe
```

## Setup
Simply compile from source, run ```RestDb.exe```, and a system configuration file will be created for you.  Setup scripts for both MSSQL and MySQL are included in the Docs directory of the project, which create the test database and person table used in the examples below.  

1) Start RestDb.exe (.NET Framework) or RestDb.dll (.NET Core) as described above.
```
RestDb :: Starting Watson Webserver at http://localhost:8000
```

2) Verify Connectivity

Point your browser to http://localhost:8000.  You should see:
```
RestDb

Your RestDb instance is operational!
```

## Simple Examples
### List databases.
```
GET http://localhost:8000/_databases
Resp:
[
  "test"
]
```

### Retrieve a Database.
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

### Describe a Table.
```
GET http://localhost:8000/test/person?_describe=true
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
      "Type": "datetime2",
      "Nullable": true
    }
  ]
}
```

### Create an Object.
Be sure to use timestamps appropriate to your database type, for instance:

- MSSQL: MM/dd/yyyy HH:mm:ss
- MySQL: yyyy-MM-dd HH:mm:ss
- PGSQL: MM/dd/yyyy HH:mm:ss
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

### Retrieve Objects.
You can retrieve all of a table's contents, retrieve by a specific ID, and filter by key-value pairs (using the querystring).
```
GET http://localhost:8000/test/person 
GET http://localhost:8000/test/person/1
GET http://localhost:8000/test/person?first_name=joel
GET http://localhost:8000/test/person?_max_results=1
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

### Retrieve Objects with Pagination.
You can retrieve results and use pagination to return only a subset.  Include _index_start and _order_by in the querystring.  Note that _order_by MUST be URL-encoded and should be the entire SQL ```ORDER BY``` clause.
```
GET http://localhost:8000/test/person?_max_results=1&_index_start=1&_order_by=ORDER%20BY%20created%20DESC
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

### Update an Object.
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

### Search for an Object.
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

### Delete an Object.
```
DELETE http://localhost:8000/test/person/1
Resp: 200/OK (no data)
```

## Enabling Authentication
To enable authentication, set ```Server.RequireAuthentication``` to ```true``` and specify an API key header in ```Server.ApiKeyHeader``` in the ```System.Json``` file.  Then, add a section called ```ApiKeys``` with each of the keys you wish to allow or disallow.  An example with one API key is below.
```
{
  "Version": "1.0.1.0",
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
      "Type": "mssql",
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
Notes from previous versions (starting with v1.0.0) will be moved here.

v1.0.x
- PostgreSQL support
- Authentication via API key
- Initial release

