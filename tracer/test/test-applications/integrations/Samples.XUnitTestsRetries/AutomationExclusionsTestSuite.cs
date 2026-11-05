using Datadog.Trace.Ci;
using Xunit;

namespace Samples.XUnitTestsRetries;

// Kept out of TestSuite because these tests only run in the automation exclusion scenarios.
public class AutomationExclusionsTestSuite
{
    [Fact]
    [PreventFailureSuppression]
    public void ProtectedFailure()
    {
        Assert.True(false);
    }

    [Fact]
    [NoAutoRetry]
    public void NoRetryFailure()
    {
        Assert.True(false);
    }
}
