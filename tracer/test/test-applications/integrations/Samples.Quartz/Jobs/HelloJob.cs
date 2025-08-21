using Quartz;

namespace QuartzSampleApp.Jobs;

public class HelloJob : IJob
{
#if QUARTZ_4_0
    async ValueTask IJob.Execute(IJobExecutionContext context)
#else
    async Task IJob.Execute(IJobExecutionContext context)
#endif
    {
        await Console.Out.WriteLineAsync("Greetings from HelloJob!");
        // No longer schedules any other jobs.
    }
}
