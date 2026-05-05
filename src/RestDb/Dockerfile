FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY RestDb.csproj ./
RUN dotnet restore RestDb.csproj

COPY . ./
RUN dotnet publish RestDb.csproj -c Release -f net8.0 -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false /p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 8000
ENTRYPOINT ["dotnet", "RestDb.dll"]
