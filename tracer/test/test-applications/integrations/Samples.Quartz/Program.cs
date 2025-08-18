using System.Diagnostics;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using QuartzSampleApp.Infrastructure;
using QuartzSampleApp.Jobs;

namespace QuartzSampleApp;

public class Program
{
    private static async Task Main(string[] args)
    {
        var factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();
        SchedulerHolder.Scheduler = scheduler;

        // Listen for completion of ExceptionJob in group2
        var targetKey = new JobKey("exceptionJob", "group2");
        scheduler.ListenerManager.AddJobListener(
            new FinalJobListener(targetKey),
            KeyMatcher<JobKey>.KeyEquals(targetKey));

        await scheduler.Start();

        // HelloJob: logs a greeting and schedules ExceptionJob
        var helloJob = JobBuilder.Create<HelloJob>()
                                 .WithIdentity("helloJob", "group1")
                                 .Build();

        var helloTrigger = TriggerBuilder.Create()
                                         .WithIdentity("helloTrigger", "group1")
                                         .StartNow()
                                         .Build();

        await scheduler.ScheduleJob(helloJob, helloTrigger);

        // Deterministic wait for final completion
        await JobCompletion.Tcs.Task;

        await scheduler.Shutdown(); // or Shutdown(waitForJobsToComplete: true)
    }
}
