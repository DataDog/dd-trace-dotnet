using Quartz;
using Quartz.Impl;

namespace QuartzSampleApp;

public class Program
{
    private static async Task Main(string[] args)
    {
        StdSchedulerFactory factory = new StdSchedulerFactory();
        IScheduler scheduler = await factory.GetScheduler();
        SchedulerHolder.Scheduler = scheduler;
        
        await scheduler.Start();

        // HelloJob: logs a greeting
        IJobDetail helloJob = JobBuilder.Create<HelloJob>()
                                        .WithIdentity("helloJob", "group1")
                                        .Build();

        ITrigger helloTrigger = TriggerBuilder.Create()
                                              .WithIdentity("helloTrigger", "group1")
                                              .StartNow()
                                              .Build();
        
        await scheduler.ScheduleJob(helloJob, helloTrigger);
        
        await Task.Delay(TimeSpan.FromSeconds(10));

        await scheduler.Shutdown();
    }
}

public static class SchedulerHolder
{
    public static IScheduler Scheduler { get; set; }
}
// The original HelloJob
public class HelloJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await Console.Out.WriteLineAsync("Greetings from HelloJob!");

        // Create and schedule ExceptionJob
        IJobDetail exceptionJob = JobBuilder.Create<ExceptionJob>()
                                            .WithIdentity("exceptionJob", "group2")
                                            .Build();

        ITrigger exceptionTrigger = TriggerBuilder.Create()
                                                  .WithIdentity("exceptionTrigger", "group2")
                                                  .StartNow()
                                                  .Build();

        await SchedulerHolder.Scheduler.ScheduleJob(exceptionJob, exceptionTrigger);
    }
    
    // async ValueTask IJob.Execute(IJobExecutionContext context)
    // {
    //     await Console.Out.WriteLineAsync("Greetings from HelloJob!");
    //
    //     // Create and schedule ExceptionJob
    //     IJobDetail exceptionJob = JobBuilder.Create<ExceptionJob>()
    //                                         .WithIdentity("exceptionJob", "group2")
    //                                         .Build();
    //
    //     ITrigger exceptionTrigger = TriggerBuilder.Create()
    //                                               .WithIdentity("exceptionTrigger", "group2")
    //                                               .StartNow()
    //                                               .Build();
    //
    //     await SchedulerHolder.Scheduler.ScheduleJob(exceptionJob, exceptionTrigger);
    // }
}

// A new job that throws an exception
public class ExceptionJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // Normal work that might blow up
            await Console.Out.WriteLineAsync("Doing work...");
            throw new InvalidOperationException("Something went wrong");
        }
        catch (Exception ex)
        {
            // Let Quartz decide what to do next
            throw new JobExecutionException(ex, refireImmediately: false);
        }                              // set true to retry instantly
    }
    //
    // async ValueTask IJob.Execute(IJobExecutionContext context)
    // {
    //     try
    //     {
    //         // Normal work that might blow up
    //         await Console.Out.WriteLineAsync("Doing work...");
    //         throw new InvalidOperationException("Something went wrong");
    //     }
    //     catch (Exception ex)
    //     {
    //         // Let Quartz decide what to do next
    //         throw new JobExecutionException(ex, refireImmediately: false);
    //     }                              // set true to retry instantly
    // }
}
