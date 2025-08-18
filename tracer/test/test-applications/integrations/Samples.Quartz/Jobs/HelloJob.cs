using Quartz;
using QuartzSampleApp.Infrastructure;

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

        // Create and schedule ExceptionJob
        var exceptionJob = JobBuilder.Create<ExceptionJob>()
                                     .WithIdentity("exceptionJob", "group2")
                                     .Build();

        var exceptionTrigger = TriggerBuilder.Create()
                                             .WithIdentity("exceptionTrigger", "group2")
                                             .StartNow()
                                             .Build();

        await SchedulerHolder.Scheduler.ScheduleJob(exceptionJob, exceptionTrigger);
    }
}
