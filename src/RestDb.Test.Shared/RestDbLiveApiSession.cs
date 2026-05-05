namespace RestDb.Test.Shared;

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RestDb;

internal static class RestDbLiveApiHost
{
    private static readonly SemaphoreSlim SyncRoot = new SemaphoreSlim(1, 1);
    private static RestDbLiveApiSession? _Session;
    private static bool _CleanupRegistered;

    public static async Task<RestDbLiveApiSession> GetAsync()
    {
        if (_Session != null) return _Session;

        await SyncRoot.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_Session == null)
            {
                _Session = await RestDbLiveApiSession.StartAsync(RestDbTestRuntime.Configuration).ConfigureAwait(false);

                if (!_CleanupRegistered)
                {
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeOnProcessExit();
                    _CleanupRegistered = true;
                }
            }

            return _Session;
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public static async ValueTask DisposeAsync()
    {
        await SyncRoot.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_Session != null)
            {
                RestDbLiveApiSession session = _Session;
                _Session = null;
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    private static void DisposeOnProcessExit()
    {
        try
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }
}

internal sealed class RestDbLiveApiSession : IAsyncDisposable
{
    private readonly Process _Process;
    private readonly StringBuilder _Output;
    private readonly StringBuilder _Error;
    private readonly string _WorkingDirectory;
    private readonly string? _SqliteFile;
    private readonly bool _DeleteSqliteArtifacts;
    private readonly string? _ApiKey;
    private readonly string _ApiKeyHeader;

    private RestDbLiveApiSession(
        TestRuntimeConfiguration runtime,
        string databaseName,
        Uri baseAddress,
        HttpClient client,
        string? apiKey,
        string apiKeyHeader,
        Process process,
        StringBuilder output,
        StringBuilder error,
        string workingDirectory,
        string? sqliteFile,
        bool deleteSqliteArtifacts)
    {
        Runtime = runtime;
        DatabaseName = databaseName;
        BaseAddress = baseAddress;
        Client = client;
        _ApiKey = apiKey;
        _ApiKeyHeader = apiKeyHeader;
        _Process = process;
        _Output = output;
        _Error = error;
        _WorkingDirectory = workingDirectory;
        _SqliteFile = sqliteFile;
        _DeleteSqliteArtifacts = deleteSqliteArtifacts;
    }

    public TestRuntimeConfiguration Runtime { get; }

    public string DatabaseName { get; }

    public Uri BaseAddress { get; }

    public HttpClient Client { get; }

    public string? ApiKey => _ApiKey;

    public string ApiKeyHeader => _ApiKeyHeader;

    public static async Task<RestDbLiveApiSession> StartAsync(
        TestRuntimeConfiguration runtime,
        bool requireAuthentication = false,
        string? apiKey = null,
        string apiKeyHeader = "x-api-key")
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));

        TestRuntimeConfiguration effectiveRuntime = runtime.Copy();
        string repositoryRoot = FindRepositoryRoot();
        string workingDirectory = Path.Combine(repositoryRoot, ".tmp", "restdb-live-api", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        string? sqliteFile = null;
        bool deleteSqliteArtifacts = false;

        if (effectiveRuntime.DatabaseType == DbTypeEnum.Sqlite)
        {
            if (!string.IsNullOrWhiteSpace(effectiveRuntime.Filename))
            {
                sqliteFile = Path.GetFullPath(effectiveRuntime.Filename, Directory.GetCurrentDirectory());
            }
            else
            {
                sqliteFile = Path.Combine(workingDirectory, "restdb-live.db");
                deleteSqliteArtifacts = true;
            }

            effectiveRuntime.Filename = sqliteFile;
        }

        int port = GetFreeTcpPort();
        Database database = TestData.CreateDatabase(effectiveRuntime, sqliteFile);

        string? effectiveApiKey = null;
        if (requireAuthentication)
        {
            effectiveApiKey = string.IsNullOrWhiteSpace(apiKey) ? "default" : apiKey;
        }

        Settings settings = new Settings
        {
            Server = new ServerSettings
            {
                ListenerHostname = "127.0.0.1",
                ListenerPort = port,
                Debug = effectiveRuntime.Debug,
                RequireAuthentication = requireAuthentication,
                Ssl = false,
                ApiKeyHeader = string.IsNullOrWhiteSpace(apiKeyHeader) ? "x-api-key" : apiKeyHeader
            },
            Logging = new LoggingSettings
            {
                ConsoleLogging = false,
                LogHttpRequests = false,
                LogHttpResponses = false,
                MinimumLevel = effectiveRuntime.Debug ? 0 : 1
            }
        };

        settings.Databases.Add(database);
        if (requireAuthentication && !string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            settings.ApiKeys.Add(new ApiKey
            {
                Key = effectiveApiKey,
                AllowGet = true,
                AllowPost = true,
                AllowPut = true,
                AllowDelete = true
            });
        }

        settings.ToFile(Path.Combine(workingDirectory, "restdb.json"));

        string restDbAssembly = typeof(Database).Assembly.Location;
        if (string.IsNullOrWhiteSpace(restDbAssembly) || !File.Exists(restDbAssembly))
        {
            throw new InvalidOperationException("Unable to locate RestDb.dll for live API test execution.");
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(restDbAssembly);

        Process process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        StringBuilder output = new StringBuilder();
        StringBuilder error = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                lock (output)
                {
                    output.AppendLine(args.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                lock (error)
                {
                    error.AppendLine(args.Data);
                }
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start RestDb live API test process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        Uri baseAddress = new Uri("http://127.0.0.1:" + port);
        HttpClient client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };
        if (requireAuthentication && !string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            client.DefaultRequestHeaders.Add(settings.Server.ApiKeyHeader, effectiveApiKey);
        }

        RestDbLiveApiSession session = new RestDbLiveApiSession(
            effectiveRuntime,
            database.Name,
            baseAddress,
            client,
            effectiveApiKey,
            settings.Server.ApiKeyHeader,
            process,
            output,
            error,
            workingDirectory,
            sqliteFile,
            deleteSqliteArtifacts);

        try
        {
            await session.WaitForReadyAsync().ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        try
        {
            if (!_Process.HasExited)
            {
                _Process.Kill(true);
                await _Process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            _Process.Dispose();
        }

        try
        {
            if (Directory.Exists(_WorkingDirectory))
            {
                Directory.Delete(_WorkingDirectory, true);
            }
        }
        catch
        {
        }

        if (_DeleteSqliteArtifacts && !string.IsNullOrWhiteSpace(_SqliteFile))
        {
            DeleteIfExists(_SqliteFile);
            DeleteIfExists(_SqliteFile + "-wal");
            DeleteIfExists(_SqliteFile + "-shm");
        }
    }

    public string GetCapturedLogs()
    {
        lock (_Output)
        {
            string output = _Output.ToString().Trim();
            string error;

            lock (_Error)
            {
                error = _Error.ToString().Trim();
            }

            return "STDOUT:" + Environment.NewLine
                + (string.IsNullOrWhiteSpace(output) ? "<empty>" : output)
                + Environment.NewLine
                + "STDERR:" + Environment.NewLine
                + (string.IsNullOrWhiteSpace(error) ? "<empty>" : error);
        }
    }

    private async Task WaitForReadyAsync()
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_Process.HasExited)
            {
                throw new InvalidOperationException(
                    "RestDb live API process exited before it became ready." + Environment.NewLine + GetCapturedLogs());
            }

            try
            {
                using HttpResponseMessage response = await Client.GetAsync("/").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception e)
            {
                lastException = e;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            "Timed out waiting for RestDb live API process readiness."
            + (lastException == null ? string.Empty : Environment.NewLine + lastException.Message)
            + Environment.NewLine
            + GetCapturedLogs());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "RestDb.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int GetFreeTcpPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
