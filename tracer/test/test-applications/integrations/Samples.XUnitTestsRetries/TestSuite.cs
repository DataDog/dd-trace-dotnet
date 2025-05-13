using System;
using System.Threading;
using Xunit;

namespace Samples.XUnitTestsRetries;

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

    [Fact]
    public void AlwaysPasses()
    {
    }

    [Fact]
    public void AlwaysFails()
    {
        Assert.True(false);
    }

    [Fact]
    public void TrueAtLastRetry()
    {
        Assert.Equal(_retryCount, Interlocked.Increment(ref _trueAtLastRetryCount));
    }

    [Fact]
    public void TrueAtThirdRetry()
    {
        Assert.Equal(3, Interlocked.Increment(ref _trueAtThirdRetryCount));
    }
}
