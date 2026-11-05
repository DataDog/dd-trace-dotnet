using Datadog.Trace.Ci;
using NUnit.Framework;

namespace Samples.NUnitTestsRetries;

// Kept out of TestSuite because these tests only run in the automation exclusion scenarios.
public class AutomationExclusionsTestSuite
{
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
}
