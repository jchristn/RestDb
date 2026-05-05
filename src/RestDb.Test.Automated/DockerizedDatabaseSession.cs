namespace RestDb.Test.Automated;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using RestDb;
using RestDb.Test.Shared;

internal sealed class DockerizedDatabaseSession : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);
    private static readonly Regex StrongSqlServerPasswordPattern =
        new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,128}$", RegexOptions.Compiled);
    private static readonly object LifecycleSyncRoot = new object();
    private static readonly Dictionary<string, bool> ActiveContainers = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    private readonly bool _keepContainer;
    private int _disposed;
    private static bool _LifecycleHandlersRegistered;

    private DockerizedDatabaseSession(string containerName, string imageName, TestRuntimeConfiguration configuration, bool keepContainer)
    {
        ContainerName = containerName;
        ImageName = imageName;
        Configuration = configuration;
        _keepContainer = keepContainer;
    }

    public string ContainerName { get; }

    public string ImageName { get; }

    public TestRuntimeConfiguration Configuration { get; }

    public static async Task<DockerizedDatabaseSession> StartAsync(
        TestRuntimeConfiguration requestedConfiguration,
        string? dockerImageOverride,
        bool keepContainer)
    {
        if (requestedConfiguration == null) throw new ArgumentNullException(nameof(requestedConfiguration));
        if (requestedConfiguration.DatabaseType == DbTypeEnum.Sqlite)
        {
            throw new InvalidOperationException("The --docker option is only supported for mysql, postgresql, and sqlserver.");
        }

        if (!string.IsNullOrWhiteSpace(requestedConfiguration.Filename))
        {
            throw new InvalidOperationException("Do not supply --filename with --docker.");
        }

        if (!string.IsNullOrWhiteSpace(requestedConfiguration.Instance))
        {
            throw new InvalidOperationException("The --instance option is not supported with --docker.");
        }

        if (!string.IsNullOrWhiteSpace(requestedConfiguration.Hostname)
            && !requestedConfiguration.Hostname.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !requestedConfiguration.Hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The --host option must be localhost or 127.0.0.1 when using --docker.");
        }

        await EnsureDockerIsAvailableAsync();

        ProviderDockerSettings settings = ProviderDockerSettings.Create(requestedConfiguration, dockerImageOverride);
        string containerName = "restdb-touchstone-" + settings.ProviderSlug + "-" + Guid.NewGuid().ToString("N")[..12];

        try
        {
            await RunDockerCommandAsync(BuildRunArguments(containerName, settings).ToArray());
            RegisterActiveContainer(containerName, keepContainer);

            int hostPort = settings.HostPort ?? await ResolvePublishedPortAsync(containerName, settings.ContainerPort);

            TestRuntimeConfiguration effectiveConfiguration = settings.BuildEffectiveConfiguration(hostPort);
            DockerizedDatabaseSession session = new DockerizedDatabaseSession(
                containerName,
                settings.ImageName,
                effectiveConfiguration,
                keepContainer);

            await session.InitializeProviderAsync(settings);

            Console.WriteLine(
                "Started dockerized " + settings.ProviderSlug
                + " test database in container '" + containerName
                + "' using image '" + settings.ImageName
                + "' on 127.0.0.1:" + hostPort.ToString(CultureInfo.InvariantCulture) + ".");

            if (keepContainer)
            {
                Console.WriteLine("Container will be left running because --keep-docker was supplied.");
            }

            return session;
        }
        catch
        {
            await ReleaseContainerAsync(containerName, keepContainer: false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_keepContainer)
        {
            UnregisterActiveContainer(ContainerName);
            Console.WriteLine("Leaving docker container '" + ContainerName + "' running.");
            return;
        }

        await ReleaseContainerAsync(ContainerName, keepContainer: false);
    }

    private async Task InitializeProviderAsync(ProviderDockerSettings settings)
    {
        switch (Configuration.DatabaseType)
        {
            case DbTypeEnum.Mysql:
                await WaitForMysqlAsync(Configuration);
                break;
            case DbTypeEnum.Postgresql:
                await WaitForPostgresqlAsync(Configuration);
                break;
            case DbTypeEnum.SqlServer:
                await WaitForSqlServerServerAsync(settings.SqlServerAdminPassword!);
                await EnsureSqlServerDatabaseAndLoginAsync(settings);
                await WaitForSqlServerDatabaseAsync(Configuration);
                break;
            default:
                throw new InvalidOperationException("Unsupported dockerized database type " + Configuration.DatabaseType + ".");
        }
    }

    private async Task WaitForSqlServerServerAsync(string saPassword)
    {
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
        {
            DataSource = Configuration.Hostname + "," + Configuration.Port,
            UserID = "sa",
            Password = saPassword,
            InitialCatalog = "master",
            Encrypt = false,
            TrustServerCertificate = true
        };

        await WaitUntilAsync(
            "SQL Server container readiness",
            async () =>
            {
                await using SqlConnection connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                await using SqlCommand command = new SqlCommand("SELECT 1;", connection);
                await command.ExecuteScalarAsync();
            });
    }

    private async Task EnsureSqlServerDatabaseAndLoginAsync(ProviderDockerSettings settings)
    {
        SqlConnectionStringBuilder masterBuilder = new SqlConnectionStringBuilder
        {
            DataSource = Configuration.Hostname + "," + Configuration.Port,
            UserID = "sa",
            Password = settings.SqlServerAdminPassword,
            InitialCatalog = "master",
            Encrypt = false,
            TrustServerCertificate = true
        };

        await using SqlConnection masterConnection = new SqlConnection(masterBuilder.ConnectionString);
        await masterConnection.OpenAsync();

        string databaseName = QuoteSqlServerIdentifier(Configuration.DatabaseName);
        string databaseLiteral = EscapeSqlLiteral(Configuration.DatabaseName);

        string createDatabase =
            "IF DB_ID(N'" + databaseLiteral + "') IS NULL " +
            "BEGIN EXEC('CREATE DATABASE " + databaseName + "'); END;";

        await ExecuteNonQueryAsync(masterConnection, createDatabase);

        string username = Configuration.Username ?? throw new InvalidOperationException("SQL Server docker configuration did not provide a username.");

        if (!username.Equals("sa", StringComparison.OrdinalIgnoreCase))
        {
            string loginName = QuoteSqlServerIdentifier(username);
            string loginLiteral = EscapeSqlLiteral(username);
            string passwordLiteral = EscapeSqlLiteral(Configuration.Password!);

            string createLogin =
                "IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'" + loginLiteral + "') " +
                "BEGIN EXEC('CREATE LOGIN " + loginName + " WITH PASSWORD = ''''" + passwordLiteral + "'''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF'); END;";

            await ExecuteNonQueryAsync(masterConnection, createLogin);

            SqlConnectionStringBuilder databaseBuilder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString)
            {
                InitialCatalog = Configuration.DatabaseName
            };

            await using SqlConnection databaseConnection = new SqlConnection(databaseBuilder.ConnectionString);
            await databaseConnection.OpenAsync();

            string createUser =
                "IF DATABASE_PRINCIPAL_ID(N'" + loginLiteral + "') IS NULL " +
                "BEGIN EXEC('CREATE USER " + loginName + " FOR LOGIN " + loginName + "'); END;" +
                "IF NOT EXISTS (" +
                "SELECT 1 FROM sys.database_role_members drm " +
                "INNER JOIN sys.database_principals rolep ON drm.role_principal_id = rolep.principal_id " +
                "INNER JOIN sys.database_principals memberp ON drm.member_principal_id = memberp.principal_id " +
                "WHERE rolep.name = N'db_owner' AND memberp.name = N'" + loginLiteral + "')" +
                "BEGIN ALTER ROLE [db_owner] ADD MEMBER " + loginName + "; END;";

            await ExecuteNonQueryAsync(databaseConnection, createUser);
        }
    }

    private async Task WaitForSqlServerDatabaseAsync(TestRuntimeConfiguration configuration)
    {
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
        {
            DataSource = configuration.Hostname + "," + configuration.Port,
            UserID = configuration.Username,
            Password = configuration.Password,
            InitialCatalog = configuration.DatabaseName,
            Encrypt = false,
            TrustServerCertificate = true
        };

        await WaitUntilAsync(
            "SQL Server database readiness",
            async () =>
            {
                await using SqlConnection connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                await using SqlCommand command = new SqlCommand("SELECT 1;", connection);
                await command.ExecuteScalarAsync();
            });
    }

    private static async Task WaitForMysqlAsync(TestRuntimeConfiguration configuration)
    {
        MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
        {
            Server = configuration.Hostname,
            Port = (uint)(configuration.Port ?? 3306),
            UserID = configuration.Username,
            Password = configuration.Password,
            Database = configuration.DatabaseName,
            AllowUserVariables = true,
            Pooling = true
        };

        await WaitUntilAsync(
            "MySQL container readiness",
            async () =>
            {
                await using MySqlConnection connection = new MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                await using MySqlCommand command = new MySqlCommand("SELECT 1;", connection);
                await command.ExecuteScalarAsync();
            });
    }

    private static async Task WaitForPostgresqlAsync(TestRuntimeConfiguration configuration)
    {
        NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
        {
            Host = configuration.Hostname,
            Port = configuration.Port ?? 5432,
            Username = configuration.Username,
            Password = configuration.Password,
            Database = configuration.DatabaseName,
            Pooling = true
        };

        await WaitUntilAsync(
            "PostgreSQL container readiness",
            async () =>
            {
                await using NpgsqlConnection connection = new NpgsqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                await using NpgsqlCommand command = new NpgsqlCommand("SELECT 1;", connection);
                await command.ExecuteScalarAsync();
            });
    }

    private static async Task WaitUntilAsync(string description, Func<Task> action)
    {
        DateTime deadline = DateTime.UtcNow.Add(StartupTimeout);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception e)
            {
                lastException = e;
                await Task.Delay(PollDelay);
            }
        }

        throw new TimeoutException("Timed out waiting for " + description + ".", lastException);
    }

    private static async Task<int> ResolvePublishedPortAsync(string containerName, int containerPort)
    {
        DockerCommandResult result = await RunDockerCommandAsync(
            "inspect",
            "--format",
            "{{(index (index .NetworkSettings.Ports \"" + containerPort.ToString(CultureInfo.InvariantCulture) + "/tcp\") 0).HostPort}}",
            containerName);

        if (!int.TryParse(result.StandardOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
        {
            throw new InvalidOperationException(
                "Unable to determine published port for container '" + containerName + "'. Docker reported: " + result.StandardOutput);
        }

        return port;
    }

    private static IReadOnlyList<string> BuildRunArguments(string containerName, ProviderDockerSettings settings)
    {
        List<string> arguments = new List<string>
        {
            "run",
            "--detach",
            "--name",
            containerName,
            "--label",
            "restdb.touchstone=true"
        };

        string publishedPort = settings.HostPort.HasValue
            ? "127.0.0.1:" + settings.HostPort.Value.ToString(CultureInfo.InvariantCulture) + ":" + settings.ContainerPort.ToString(CultureInfo.InvariantCulture)
            : "127.0.0.1::" + settings.ContainerPort.ToString(CultureInfo.InvariantCulture);

        arguments.Add("--publish");
        arguments.Add(publishedPort);

        foreach (KeyValuePair<string, string> environmentVariable in settings.EnvironmentVariables)
        {
            arguments.Add("--env");
            arguments.Add(environmentVariable.Key + "=" + environmentVariable.Value);
        }

        arguments.Add(settings.ImageName);
        return arguments;
    }

    private static async Task EnsureDockerIsAvailableAsync()
    {
        await RunDockerCommandAsync("info", "--format", "{{.ServerVersion}}");
    }

    private static void RegisterActiveContainer(string containerName, bool keepContainer)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return;
        }

        lock (LifecycleSyncRoot)
        {
            if (!_LifecycleHandlersRegistered)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupTrackedContainersOnShutdown("process exit");
                AppDomain.CurrentDomain.UnhandledException += (_, _) => CleanupTrackedContainersOnShutdown("unhandled exception");
                Console.CancelKeyPress += HandleConsoleCancelKeyPress;
                _LifecycleHandlersRegistered = true;
            }

            ActiveContainers[containerName] = keepContainer;
        }
    }

    private static void UnregisterActiveContainer(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return;
        }

        lock (LifecycleSyncRoot)
        {
            ActiveContainers.Remove(containerName);
        }
    }

    private static async Task ReleaseContainerAsync(string containerName, bool keepContainer)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return;
        }

        bool leaveRunning = keepContainer;

        lock (LifecycleSyncRoot)
        {
            if (ActiveContainers.TryGetValue(containerName, out bool trackedKeepContainer))
            {
                leaveRunning = trackedKeepContainer;
                ActiveContainers.Remove(containerName);
            }
        }

        if (leaveRunning)
        {
            Console.WriteLine("Leaving docker container '" + containerName + "' running.");
            return;
        }

        await TryStopContainerAsync(containerName);
        bool removed = await TryRemoveContainerAsync(containerName);
        if (!removed)
        {
            throw new InvalidOperationException("Failed to remove docker container '" + containerName + "'.");
        }

        Console.WriteLine("Stopped and removed docker container '" + containerName + "'.");
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using SqlCommand command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<DockerCommandResult> RunDockerCommandAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Docker command failed: docker " + string.Join(" ", arguments) + Environment.NewLine +
                "Exit code: " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                "Stdout: " + stdout + Environment.NewLine +
                "Stderr: " + stderr);
        }

        return new DockerCommandResult(stdout, stderr);
    }

    private static async Task<bool> TryStopContainerAsync(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return true;
        }

        try
        {
            await RunDockerCommandAsync("stop", "--time", "15", containerName);
            return true;
        }
        catch (Exception e)
        {
            if (IsMissingContainerException(e))
            {
                return true;
            }

            Console.Error.WriteLine("Failed to stop docker container '" + containerName + "': " + e.Message);
            return false;
        }
    }

    private static async Task<bool> TryRemoveContainerAsync(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return true;
        }

        try
        {
            await RunDockerCommandAsync("rm", "--force", "--volumes", containerName);
            return true;
        }
        catch (Exception e)
        {
            if (IsMissingContainerException(e))
            {
                return true;
            }

            Console.Error.WriteLine("Failed to remove docker container '" + containerName + "': " + e.Message);
            return false;
        }
    }

    private static void HandleConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        CleanupTrackedContainersOnShutdown("console cancel");
    }

    private static void CleanupTrackedContainersOnShutdown(string reason)
    {
        KeyValuePair<string, bool>[] containers;

        lock (LifecycleSyncRoot)
        {
            containers = ActiveContainers.ToArray();
            ActiveContainers.Clear();
        }

        foreach (KeyValuePair<string, bool> container in containers)
        {
            if (container.Value)
            {
                Console.WriteLine(
                    "Leaving docker container '" + container.Key + "' running during " + reason + " because --keep-docker was supplied.");
                continue;
            }

            try
            {
                _ = TryStopContainerAsync(container.Key).GetAwaiter().GetResult();
                bool removed = TryRemoveContainerAsync(container.Key).GetAwaiter().GetResult();
                if (!removed)
                {
                    Console.Error.WriteLine(
                        "Failed to remove docker container '" + container.Key + "' during " + reason + ".");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    "Failed to stop and remove docker container '" + container.Key + "' during " + reason + ": " + e.Message);
            }
        }
    }

    private static bool IsMissingContainerException(Exception exception)
    {
        return exception.Message.Contains("No such container", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("is not running", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string QuoteSqlServerIdentifier(string value)
    {
        return "[" + value.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    private readonly record struct DockerCommandResult(string StandardOutput, string StandardError);

    private sealed class ProviderDockerSettings
    {
        private ProviderDockerSettings()
        {
        }

        public string ProviderSlug { get; private init; } = string.Empty;

        public string ImageName { get; private init; } = string.Empty;

        public int ContainerPort { get; private init; }

        public int? HostPort { get; private init; }

        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; private init; } = new Dictionary<string, string>();

        public DbTypeEnum DatabaseType { get; private init; }

        public string DatabaseName { get; private init; } = string.Empty;

        public string Username { get; private init; } = string.Empty;

        public string Password { get; private init; } = string.Empty;

        public string? SqlServerAdminPassword { get; private init; }

        public bool Debug { get; private init; }

        public string? Schema { get; private init; }

        public static ProviderDockerSettings Create(TestRuntimeConfiguration configuration, string? dockerImageOverride)
        {
            return configuration.DatabaseType switch
            {
                DbTypeEnum.Mysql => CreateMysql(configuration, dockerImageOverride),
                DbTypeEnum.Postgresql => CreatePostgresql(configuration, dockerImageOverride),
                DbTypeEnum.SqlServer => CreateSqlServer(configuration, dockerImageOverride),
                _ => throw new InvalidOperationException("Unsupported dockerized database type " + configuration.DatabaseType + ".")
            };
        }

        public TestRuntimeConfiguration BuildEffectiveConfiguration(int hostPort)
        {
            return new TestRuntimeConfiguration
            {
                DatabaseType = DatabaseType,
                DatabaseName = DatabaseName,
                Hostname = "127.0.0.1",
                Port = hostPort,
                Username = Username,
                Password = Password,
                Debug = Debug,
                Schema = Schema
            };
        }

        private static ProviderDockerSettings CreateMysql(TestRuntimeConfiguration configuration, string? dockerImageOverride)
        {
            string username = string.IsNullOrWhiteSpace(configuration.Username) ? "root" : configuration.Username;
            string password = string.IsNullOrWhiteSpace(configuration.Password) ? "password" : configuration.Password;
            string databaseName = string.IsNullOrWhiteSpace(configuration.DatabaseName) ? "restdb_touchstone" : configuration.DatabaseName;

            Dictionary<string, string> environmentVariables = new Dictionary<string, string>
            {
                ["MYSQL_ROOT_PASSWORD"] = string.IsNullOrWhiteSpace(configuration.Password) ? "password" : configuration.Password,
                ["MYSQL_DATABASE"] = databaseName
            };

            if (!username.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                environmentVariables["MYSQL_USER"] = username;
                environmentVariables["MYSQL_PASSWORD"] = password;
                environmentVariables["MYSQL_ROOT_PASSWORD"] = "root-password";
            }

            return new ProviderDockerSettings
            {
                ProviderSlug = "mysql",
                ImageName = string.IsNullOrWhiteSpace(dockerImageOverride) ? "mysql:8.4" : dockerImageOverride,
                ContainerPort = 3306,
                HostPort = configuration.Port,
                EnvironmentVariables = environmentVariables,
                DatabaseType = DbTypeEnum.Mysql,
                DatabaseName = databaseName,
                Username = username,
                Password = password,
                Debug = configuration.Debug,
                Schema = configuration.Schema
            };
        }

        private static ProviderDockerSettings CreatePostgresql(TestRuntimeConfiguration configuration, string? dockerImageOverride)
        {
            string username = string.IsNullOrWhiteSpace(configuration.Username) ? "postgres" : configuration.Username;
            string password = string.IsNullOrWhiteSpace(configuration.Password) ? "password" : configuration.Password;
            string databaseName = string.IsNullOrWhiteSpace(configuration.DatabaseName) ? "restdb_touchstone" : configuration.DatabaseName;

            return new ProviderDockerSettings
            {
                ProviderSlug = "postgresql",
                ImageName = string.IsNullOrWhiteSpace(dockerImageOverride) ? "postgres:16" : dockerImageOverride,
                ContainerPort = 5432,
                HostPort = configuration.Port,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["POSTGRES_DB"] = databaseName,
                    ["POSTGRES_USER"] = username,
                    ["POSTGRES_PASSWORD"] = password
                },
                DatabaseType = DbTypeEnum.Postgresql,
                DatabaseName = databaseName,
                Username = username,
                Password = password,
                Debug = configuration.Debug,
                Schema = configuration.Schema
            };
        }

        private static ProviderDockerSettings CreateSqlServer(TestRuntimeConfiguration configuration, string? dockerImageOverride)
        {
            string username = string.IsNullOrWhiteSpace(configuration.Username) ? "sa" : configuration.Username;
            string databaseName = string.IsNullOrWhiteSpace(configuration.DatabaseName) ? "restdb_touchstone" : configuration.DatabaseName;
            string saPassword;
            string effectivePassword;

            if (username.Equals("sa", StringComparison.OrdinalIgnoreCase))
            {
                effectivePassword = string.IsNullOrWhiteSpace(configuration.Password) ? "RestDb!Pass123" : configuration.Password;
                if (!StrongSqlServerPasswordPattern.IsMatch(effectivePassword))
                {
                    throw new InvalidOperationException(
                        "When using --type sqlserver --docker with the sa login, --pass must contain upper, lower, number, and symbol characters and be at least 8 characters long.");
                }

                saPassword = effectivePassword;
            }
            else
            {
                effectivePassword = string.IsNullOrWhiteSpace(configuration.Password) ? "password" : configuration.Password;
                saPassword = "RestDb!SaPass123";
            }

            return new ProviderDockerSettings
            {
                ProviderSlug = "sqlserver",
                ImageName = string.IsNullOrWhiteSpace(dockerImageOverride) ? "mcr.microsoft.com/mssql/server:2022-latest" : dockerImageOverride,
                ContainerPort = 1433,
                HostPort = configuration.Port,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["ACCEPT_EULA"] = "Y",
                    ["MSSQL_PID"] = "Developer",
                    ["MSSQL_SA_PASSWORD"] = saPassword
                },
                DatabaseType = DbTypeEnum.SqlServer,
                DatabaseName = databaseName,
                Username = username,
                Password = effectivePassword,
                SqlServerAdminPassword = saPassword,
                Debug = configuration.Debug,
                Schema = configuration.Schema
            };
        }
    }
}
