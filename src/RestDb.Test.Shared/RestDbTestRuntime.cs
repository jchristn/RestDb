namespace RestDb.Test.Shared;

using System;
using System.Threading.Tasks;

public static class RestDbTestRuntime
{
    private static readonly object SyncRoot = new object();
    private static TestRuntimeConfiguration _Configuration = new TestRuntimeConfiguration();

    public static TestRuntimeConfiguration Configuration
    {
        get
        {
            lock (SyncRoot)
            {
                return _Configuration.Copy();
            }
        }
    }

    public static void Configure(TestRuntimeConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        lock (SyncRoot)
        {
            _Configuration = configuration.Copy();
        }
    }

    public static ValueTask CleanupAsync()
    {
        return RestDbLiveApiHost.DisposeAsync();
    }
}
