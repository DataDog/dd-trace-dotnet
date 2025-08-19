using System;
using System.Threading.Tasks;

namespace Samples.Hangfire.Jobs;

public class TestJob
{
    public Task Execute()
    {
        Console.WriteLine("TestJob.Execute running...");
        return Task.CompletedTask;
    }

    public Task ThrowException()
    {
        Console.WriteLine("TestJob.ThrowException running...");
        throw new InvalidOperationException("Boom from TestJob.ThrowException");
    }
}
