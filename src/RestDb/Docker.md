# Running RestDb in Docker
 
Getting an ```HttpListener``` application (such as RestDb or any application using Watson Webserver, HTTP.sys, etc) up and running in Docker can be rather tricky given how 1) Docker acts as a network proxy and 2) HttpListener isn't friendly to ```HOST```

## Before you Begin

RestDb relies on a configuration file ```System.json``` that must be present.  It is automatically generated on the first run, so it is recommended that you first run RestDb outside of Docker and make the appropriate configuration changes.  This will need to be copied in either as part of build or overridden when the image is run as described below.

Set ```Logging.ConsoleLogging``` to ```false``` when running in Docker.

Set ```Server.ListenerHostname``` to ```*``` when running in Docker.

## Steps to Run RestDb in Docker

1) View and modify the ```Dockerfile``` as appropriate.

2) Execute the Docker build process:
```
$ docker build -t restdb -f Dockerfile .
```

3) Verify the image exists:
```
$ docker images
REPOSITORY                              TAG                 IMAGE ID            CREATED             SIZE
restdb                                  latest              047e29f37f9c        2 seconds ago       328MB
mcr.microsoft.com/dotnet/core/sdk       3.1                 abbb476b7b81        11 days ago         737MB
mcr.microsoft.com/dotnet/core/runtime   3.1                 4b555235dfc0        11 days ago         327MB
```
 
4) Execute the container:
```
Windows
$ docker run --user ContainerAdministrator -d -p 8000:8000 restdb

Linux or Mac
$ docker run --user root -d -p 8000:8000 restdb
```

To run using a ```System.json``` from your filesystem (or external storage) use the following.  Note that the first parameter to ```-v``` is the path to the file outside of the container image and the second parameter is the path within the image.  The app is in ```/app``` so the path will need to reflect that.
```
Windows
$ docker run --user ContainerAdministrator -p 8000:8000 -v /[PathOnLocalFilesystem]/System.json:/app/System.json restdb

Linux or Mac 
$ docker run --user root -p 8000:8000 -v /[PathOnLocalFilesystem]/System.json:/app/System.json restdb
```

5) Connect to RestDb in your browser:
```
http://localhost:8000
```

6) Get the container name:
```
$ docker ps
CONTAINER ID        IMAGE               COMMAND                  CREATED              STATUS              PORTS                    NAMES
3627b4e812fd        restdb              "dotnet RestDb.dll"      About a minute ago   Up About a minute   0.0.0.0:8000->8000/tcp   silly_khayyam
```

7) Kill a running container:
```
$ docker kill [CONTAINER ID]
```

8) Delete a container image:
```
$ docker rmi [IMAGE ID] -f
```

## Example System.json File
```
{
  "Version": "1.2.2",
  "Server": {
    "ListenerHostname": "*",
    "ListenerPort": 8000,
    "Ssl": false,
    "Debug": false,
    "RequireAuthentication": false,
    "ApiKeyHeader": "x-api-key"
  },
  "Logging": {
    "ServerIp": "127.0.0.1",
    "ServerPort": 514,
    "MinimumLevel": 1,
    "LogHttpRequests": false,
    "LogHttpResponses": false,
    "ConsoleLogging": false
  },
  "Databases": [
    {
      "Name": "db1",
      "Type": "MySql",
      "Hostname": "localhost",
      "Port": 3306,
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
