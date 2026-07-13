using System;
using System.Linq;
using System.Threading.Tasks;

namespace Samples.Hangfire.Jobs;

public class TestJob
{
    public Task Execute()
    {
        Console.WriteLine("TestJob.Execute running...");
        return Task.CompletedTask;
    }

    public void PrintBaggage(string jobName)
    {
        var baggage = OpenTelemetry.Baggage.Current.GetBaggage()
                                   .OrderBy(x => x.Key)
                                   .Select(x => $"{x.Key}={x.Value}");

        Console.WriteLine($"Worker thread for {jobName}: {Environment.CurrentManagedThreadId}");
        Console.WriteLine($"Baggage for {jobName}: [{string.Join(", ", baggage)}]");
    }

    public Task ThrowException()
    {
        Console.WriteLine("TestJob.ThrowException running...");
        throw new InvalidOperationException("Boom from TestJob.ThrowException");
    }
}
