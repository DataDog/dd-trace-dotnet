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
        // Setting up OTEL to see if there's any conflict
        

        // var tracerProvider = Sdk.CreateTracerProviderBuilder()
        //                         .AddQuartzInstrumentation()
        //                         .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OTEL_QUARTZ_SAMPLE_APP"))
        //                         .AddConsoleExporter(options =>
        //                          {
        //                              options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
        //                          })
        //                         .AddOtlpExporter(otlpOptions =>
        //                          {
        //                              otlpOptions.Endpoint = new Uri("http://localhost:4318/v1/traces");
        //                              otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        //                          })
        //                         .Build();

        var factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();
        SchedulerHolder.Scheduler = scheduler;

        // Register listeners for all jobs
        var helloKey = new JobKey("helloJob", "group1");
        var exceptionKey = new JobKey("exceptionJob", "group2");
        var vetoableKey = new JobKey("vetoableJob", "group3");

        scheduler.ListenerManager.AddJobListener(
            new FinalJobListener(helloKey, JobCompletion.HelloTcs),
            KeyMatcher<JobKey>.KeyEquals(helloKey));

        scheduler.ListenerManager.AddJobListener(
            new FinalJobListener(exceptionKey, JobCompletion.ExceptionTcs),
            KeyMatcher<JobKey>.KeyEquals(exceptionKey));

        // Add trigger listener for veto functionality
        scheduler.ListenerManager.AddTriggerListener(
            new VetoTriggerListener(vetoableKey, JobCompletion.VetoTcs));

        await scheduler.Start();

        // HelloJob: logs a greeting (independent)
        var helloJob = JobBuilder.Create<HelloJob>()
                                 .WithIdentity(helloKey)
                                 .Build();

        var helloTrigger = TriggerBuilder.Create()
                                         .WithIdentity("helloTrigger", "group1")
                                         .StartNow()
                                         .Build();

        await scheduler.ScheduleJob(helloJob, helloTrigger);

        // ExceptionJob: scheduled independently
        var exceptionJob = JobBuilder.Create<ExceptionJob>()
                                     .WithIdentity(exceptionKey)
                                     .Build();

        var exceptionTrigger = TriggerBuilder.Create()
                                             .WithIdentity("exceptionTrigger", "group2")
                                             .StartNow()
                                             .Build();

        await scheduler.ScheduleJob(exceptionJob, exceptionTrigger);

        // VetoableJob: will be vetoed by the trigger listener
        var vetoableJob = JobBuilder.Create<VetoableJob>()
                                    .WithIdentity(vetoableKey)
                                    .Build();

        var vetoableTrigger = TriggerBuilder.Create()
                                           .WithIdentity("vetoableTrigger", "group3")
                                           .StartNow()
                                           .Build();

        await scheduler.ScheduleJob(vetoableJob, vetoableTrigger);

        // Wait for all jobs to finish (success, failure, or veto)
        await Task.WhenAll(JobCompletion.HelloTcs.Task, JobCompletion.ExceptionTcs.Task, JobCompletion.VetoTcs.Task);

        await scheduler.Shutdown(); // or Shutdown(waitForJobsToComplete: true)
        // tracerProvider?.Dispose();
    }
}
