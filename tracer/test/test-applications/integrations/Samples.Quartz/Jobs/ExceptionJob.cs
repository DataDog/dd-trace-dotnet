using Quartz;
using QuartzSampleApp.Infrastructure;

namespace QuartzSampleApp.Jobs;

public class ExceptionJob : IJob
{
#if QUARTZ_4_0
    async ValueTask IJob.Execute(IJobExecutionContext context)
#else
    async Task IJob.Execute(IJobExecutionContext context)
#endif
    {
        try
        {
            await Console.Out.WriteLineAsync("Doing work...");
            throw new InvalidOperationException("Something went wrong");
        }
        catch (Exception ex)
        {
            // Let Quartz decide what to do next
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
