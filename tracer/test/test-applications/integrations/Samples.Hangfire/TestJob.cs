using System;

namespace Samples.Hangfire;

internal class TestJob
{
    public void Execute()
    {
        Console.WriteLine("TestJob.Execute()");
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}
