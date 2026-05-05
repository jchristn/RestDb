using System;
using RestDb;
using RestDb.Test.Automated;
using RestDb.Test.Shared;
using Touchstone.Cli;

string? resultsPath = null;
bool providerSpecified = false;
bool externalConnectionArgumentsSpecified = false;
bool useDocker = false;
bool keepDocker = false;
string? dockerImageOverride = null;
TestRuntimeConfiguration configuration = new TestRuntimeConfiguration();
DockerizedDatabaseSession? dockerSession = null;
int exitCode = 1;

try
{
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];

        switch (arg.ToLowerInvariant())
        {
            case "--help":
            case "-h":
                PrintUsage();
                return 0;

            case "--results":
                resultsPath = ReadNextValue(args, ref i, arg);
                break;

            case "--docker":
                useDocker = true;
                break;

            case "--keep-docker":
                keepDocker = true;
                break;

            case "--docker-image":
                dockerImageOverride = ReadNextValue(args, ref i, arg);
                break;

            case "--type":
            case "--provider":
                configuration.DatabaseType = ParseProvider(ReadNextValue(args, ref i, arg));
                providerSpecified = true;
                break;

            case "--database":
            case "--dbname":
            case "--name":
                configuration.DatabaseName = ReadNextValue(args, ref i, arg);
                externalConnectionArgumentsSpecified = true;
                break;

            case "--filename":
            case "--file":
                configuration.Filename = ReadNextValue(args, ref i, arg);
                break;

            case "--host":
            case "--hostname":
                configuration.Hostname = ReadNextValue(args, ref i, arg);
                externalConnectionArgumentsSpecified = true;
                break;

            case "--port":
                configuration.Port = int.Parse(ReadNextValue(args, ref i, arg));
                externalConnectionArgumentsSpecified = true;
                break;

            case "--instance":
                configuration.Instance = ReadNextValue(args, ref i, arg);
                externalConnectionArgumentsSpecified = true;
                break;

            case "--user":
            case "--username":
                configuration.Username = ReadNextValue(args, ref i, arg);
                externalConnectionArgumentsSpecified = true;
                break;

            case "--pass":
            case "--password":
                configuration.Password = ReadNextValue(args, ref i, arg);
                externalConnectionArgumentsSpecified = true;
                break;

            case "--schema":
                configuration.Schema = ReadNextValue(args, ref i, arg);
                externalConnectionArgumentsSpecified = true;
                break;

            case "--debug":
                configuration.Debug = true;
                break;

            default:
                throw new ArgumentException("Unknown argument '" + arg + "'. Use --help for usage.");
        }
    }

    if (keepDocker && !useDocker)
    {
        throw new ArgumentException("The --keep-docker option requires --docker.");
    }

    if (!useDocker && !string.IsNullOrWhiteSpace(dockerImageOverride))
    {
        throw new ArgumentException("The --docker-image option requires --docker.");
    }

    if (useDocker && !providerSpecified)
    {
        throw new ArgumentException("Specify --type when using --docker.");
    }

    if (externalConnectionArgumentsSpecified && !providerSpecified && string.IsNullOrWhiteSpace(configuration.Filename))
    {
        throw new ArgumentException("Specify --type when providing external database connection arguments.");
    }

    if (useDocker)
    {
        dockerSession = await DockerizedDatabaseSession.StartAsync(configuration, dockerImageOverride, keepDocker);
        configuration = dockerSession.Configuration;
    }

    configuration.Validate();
    RestDbTestRuntime.Configure(configuration);
    exitCode = await ConsoleRunner.RunAsync(RestDbTestSuites.All, resultsPath: resultsPath);
}
catch (Exception e)
{
    Console.Error.WriteLine(e.Message);
    PrintUsage();
    exitCode = 1;
}
finally
{
    try
    {
        await RestDbTestRuntime.CleanupAsync();
    }
    catch (Exception e)
    {
        Console.Error.WriteLine("Failed to clean up shared test resources: " + e.Message);
        exitCode = 1;
    }

    if (dockerSession != null)
    {
        try
        {
            await dockerSession.DisposeAsync();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Failed to clean up dockerized test database: " + e.Message);
            exitCode = 1;
        }
    }
}

return exitCode;

static string ReadNextValue(string[] args, ref int index, string option)
{
    if (index + 1 >= args.Length)
    {
        throw new ArgumentException("Missing value for " + option + ".");
    }

    index++;
    return args[index];
}

static DbTypeEnum ParseProvider(string value)
{
    return value.ToLowerInvariant() switch
    {
        "sqlite" => DbTypeEnum.Sqlite,
        "postgres" => DbTypeEnum.Postgresql,
        "postgresql" => DbTypeEnum.Postgresql,
        "pgsql" => DbTypeEnum.Postgresql,
        "sqlserver" => DbTypeEnum.SqlServer,
        "mssql" => DbTypeEnum.SqlServer,
        "mysql" => DbTypeEnum.Mysql,
        _ => throw new ArgumentException("Unknown provider '" + value + "'.")
    };
}

static void PrintUsage()
{
    Console.WriteLine("RestDb Touchstone automated runner");
    Console.WriteLine("Default behavior: run against SQLite using a temporary database.");
    Console.WriteLine("Options:");
    Console.WriteLine("  --results <path>           Write Touchstone JSON results to a file.");
    Console.WriteLine("  --docker                   Start a disposable dockerized database for mysql, postgresql, or sqlserver.");
    Console.WriteLine("  --keep-docker              Leave the docker container running after the test run.");
    Console.WriteLine("  --docker-image <image>     Override the default docker image for the selected provider.");
    Console.WriteLine("  --type <provider>          sqlite | postgresql | sqlserver | mysql");
    Console.WriteLine("  --database <name>          Database name for external providers.");
    Console.WriteLine("  --filename <path>          SQLite file path override.");
    Console.WriteLine("  --host <hostname>          Hostname for external providers.");
    Console.WriteLine("  --port <port>              Port for external providers.");
    Console.WriteLine("  --instance <name>          SQL Server instance name.");
    Console.WriteLine("  --user <username>          Username for external providers.");
    Console.WriteLine("  --pass <password>          Password for external providers.");
    Console.WriteLine("  --schema <schema>          Optional schema metadata for the run.");
    Console.WriteLine("  --debug                    Enable provider debug logging.");
}
