using Datadog.Trace.Ci;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsRetries;

// Kept out of TestSuite because these tests only run in the automation exclusion scenarios.
[TestClass]
public class AutomationExclusionsTestSuite
{
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
}
