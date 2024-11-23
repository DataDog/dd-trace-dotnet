using System;
using Hangfire;
using System.Threading;

namespace Samples.Hangfire;

public static class Program
{
    private static readonly CountdownEvent _individualJobsDone = new(2);

    public static void Main(string[] args)
    {
        GlobalConfiguration.Configuration.UseInMemoryStorage();
        GlobalJobFilters.Filters.Add(new ContinuationsSupportAttribute());
        using var server = new BackgroundJobServer();

        RunBackgroundJobs();
        _individualJobsDone.Wait();
    }

    private static void RunBackgroundJobs()
    {
        var jobId = BackgroundJob.Enqueue(
            () => FireAndForget());

        var delayedJobId = BackgroundJob.Schedule(
            () => FireDelayedMesssage(),
            TimeSpan.FromMilliseconds(7));
    }

    public static void FireAndForget()
    {
        Console.WriteLine("Fire-and-forget!");
        _individualJobsDone.Signal();
    }

    public static void FireDelayedMesssage()
    {
        Console.WriteLine("Delayed!");
        _individualJobsDone.Signal();
    }
}
