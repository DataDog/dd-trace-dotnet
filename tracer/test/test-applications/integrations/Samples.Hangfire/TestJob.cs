using System;

namespace Samples.Hangfire;

internal class TestJob
{
    public void Execute()
    {
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}
