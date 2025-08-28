using Quartz;
using QuartzSampleApp.Infrastructure;

namespace QuartzSampleApp.Jobs;

public class ExceptionJob : IJob
{
    // Quartz 4x uses ValueTask instead of Task
#if QUARTZ_4_0
    async ValueTask IJob.Execute(IJobExecutionContext context)
#else
    async Task IJob.Execute(IJobExecutionContext context)
#endif
    {
        try
        {
            await Console.Out.WriteLineAsync("Doing work...");
            throw new InvalidOperationException("Expected InvalidOperationException thrown");
        }
        catch (Exception ex)
        {
            // Let Quartz decide what to do next
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
