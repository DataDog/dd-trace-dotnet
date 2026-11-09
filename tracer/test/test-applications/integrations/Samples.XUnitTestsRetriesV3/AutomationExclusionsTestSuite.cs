using System.Threading;
using Datadog.Trace.Ci;
using Xunit;

namespace Samples.XUnitTestsRetriesV3;

// Kept out of TestSuite because these tests only run in the automation exclusion scenarios.
public class AutomationExclusionsTestSuite
{
    private static int _protectedFlakyFailureExecutions;

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

    [Fact]
    [PreventFailureSuppression]
    public void ProtectedFlakyFailure()
    {
        // Passes only on the first attempt, so Attempt to Fix must stop after the failing second attempt.
        Assert.True(Interlocked.Increment(ref _protectedFlakyFailureExecutions) == 1);
    }
}
