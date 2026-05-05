namespace RestDb.Test.Xunit;

using System.Threading;
using System.Threading.Tasks;
using RestDb.Test.Shared;
using Touchstone.Core;
using global::Xunit;

public sealed class RestDbTouchstoneXunitTests
{
    public static TheoryData<TestCaseDescriptor> TestCases()
    {
        TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

        foreach (TestSuiteDescriptor suite in RestDbTestSuites.All)
        {
            foreach (TestCaseDescriptor testCase in suite.Cases)
            {
                if (!testCase.Skip)
                {
                    data.Add(testCase);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await testCase.ExecuteAsync(CancellationToken.None);
    }
}
