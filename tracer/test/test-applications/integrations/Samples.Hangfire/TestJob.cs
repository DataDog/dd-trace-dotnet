using System;

namespace Samples.Hangfire;

internal partial class TestJob
{
    public void Execute()
    {
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}
