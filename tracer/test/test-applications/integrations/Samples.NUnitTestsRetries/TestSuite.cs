using System;
using System.Threading;
using NUnit.Framework;

namespace Samples.NUnitTestsRetries;

public class TestSuite
{
    private static int _retryCount;
    private static int _trueAtLastRetryCount = -1;
    private static int _trueAtThirdRetryCount = -1;

    static TestSuite()
    {
        var strRetryCount = Environment.GetEnvironmentVariable("DD_CIVISIBILITY_FLAKY_RETRY_COUNT");
        int.TryParse(strRetryCount, out _retryCount);
    }

    [Test]
    public void AlwaysPasses()
    {
    }
    
    [Test]
    public void AlwaysFails()
    {
        Assert.True(false);
    }

    [Test]
    public void TrueAtLastRetry()
    {
        Assert.AreEqual(_retryCount, Interlocked.Increment(ref _trueAtLastRetryCount));
    }
    
    [Test]
    public void TrueAtThirdRetry()
    {
        Assert.AreEqual(3, Interlocked.Increment(ref _trueAtThirdRetryCount));
    }
}
