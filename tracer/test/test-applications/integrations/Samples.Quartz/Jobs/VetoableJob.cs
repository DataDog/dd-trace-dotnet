using Quartz;

namespace QuartzSampleApp.Jobs;

public class VetoableJob : IJob
{
    // Quartz 4x uses ValueTask instead of Task
#if QUARTZ_4_0
    async ValueTask IJob.Execute(IJobExecutionContext context)
#else
    async ValueTask IJob.Execute(IJobExecutionContext context)
#endif
    {
        // This job should never execute because it will be vetoed
        await Console.Out.WriteLineAsync("VetoableJob executed - this should NOT happen!");
    }
}
