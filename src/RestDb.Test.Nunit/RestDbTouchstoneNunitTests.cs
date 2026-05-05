namespace RestDb.Test.Nunit;

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RestDb.Test.Shared;
using Touchstone.Core;
using Touchstone.NunitAdapter;

[TestFixture]
public sealed class RestDbTouchstoneNunitTests
{
    private static IEnumerable TestCases()
    {
        return new TouchstoneTestCaseSource(RestDbTestSuites.All);
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await testCase.ExecuteAsync(CancellationToken.None);
    }
}
