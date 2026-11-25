using System.Threading;
using Datadog.Trace.Ci;
using NUnit.Framework;

namespace Samples.NUnitTestsRetries;

// Kept out of TestSuite because these tests only run in the automation exclusion scenarios.
public class AutomationExclusionsTestSuite
{
    private static int _protectedFlakyFailureExecutions;

    [Test]
    [PreventFailureSuppression]
    public void ProtectedFailure()
    {
        Assert.That(false, Is.True);
    }

    [Test]
    [NoAutoRetry]
    public void NoRetryFailure()
    {
        Assert.That(false, Is.True);
    }

    [Test]
    [PreventFailureSuppression]
    public void ProtectedFlakyFailure()
    {
        // Passes only on the first attempt, so Attempt to Fix must stop after the failing second attempt.
        Assert.That(Interlocked.Increment(ref _protectedFlakyFailureExecutions) == 1, Is.True);
    }
}
