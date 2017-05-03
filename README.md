# RestDb

RESTful HTTP/HTTPS server for Microsoft SQL Server and MySQL database tables written in C#.  

## Description
RestDb spawns a RESTful HTTP/HTTPS server that exposes a series of APIs allowing you to perform SELECT, INSERT, UPDATE, DELETE, and TRUNCATE against tables in Microsoft SQL Server and MySQL.
 
## New in v1.0.0
- Initial release

## Running in Mono
Before starting in Linux or Mac environments, you should run the Mono AOT.
```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server RestDb.exe
mono --server RestDb.exe
```

## Setup
Simply compile from source, run ```RestDb.exe```, and a system configuration file will be created for you.  Setup scripts for both MSSQL and MySQL are included in the Docs directory of the project, which create the test database and person table used in the examples below.  

1) Start RestDb.exe
```
Windows   : C:\RestDb\RestDb.exe
Linux/Mac : $ mono --server RestDb.exe
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

### Retrieve a database.
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

### Describe a table.
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

### Create an object.
Be sure to use timestamps appropriate to your database type, for instance:
MSSQL: MM/dd/yyyy HH:mm:ss
MySQL: yyyy-MM-dd HH:mm:ss
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

### Retrieve objects.
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

### Retrieve objects with pagination.
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

### Update an object.
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

### Search for an object.
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

### Delete an object.
```
DELETE http://localhost:8000/test/person/1
Resp: 200/OK (no data)
```

## Version history
Notes from previous versions (starting with v1.0.0) will be moved here.
