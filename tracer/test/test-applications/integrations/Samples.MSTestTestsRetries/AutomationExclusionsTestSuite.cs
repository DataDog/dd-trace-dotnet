using System.Threading;
using Datadog.Trace.Ci;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsRetries;

// Kept out of TestSuite because these tests only run in the automation exclusion scenarios.
[TestClass]
public class AutomationExclusionsTestSuite
{
    private static int _protectedFlakyFailureExecutions;

    [TestMethod]
    [PreventFailureSuppression]
    public void ProtectedFailure()
    {
        Assert.IsTrue(false);
    }

    [TestMethod]
    [NoAutoRetry]
    public void NoRetryFailure()
    {
        Assert.IsTrue(false);
    }

    [TestMethod]
    [PreventFailureSuppression]
    public void ProtectedFlakyFailure()
    {
        Assert.IsTrue(Interlocked.Increment(ref _protectedFlakyFailureExecutions) != 2);
    }
}
