using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.Impl;
using Quartz.Logging;

namespace QuartzSampleApp;

public class Program
{
    private static async Task Main(string[] args)
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
                                .AddQuartzInstrumentation()
                                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("QuartzSampleApp"))
                                .AddConsoleExporter(options =>
                                 {
                                     options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
                                 })
                                .AddOtlpExporter(otlpOptions =>
                                 {
                                     otlpOptions.Endpoint = new Uri("http://localhost:4318/v1/traces");
                                     otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                                 })
                                .Build();

        LogProvider.SetCurrentLogProvider(new ConsoleLogProvider());

        StdSchedulerFactory factory = new StdSchedulerFactory();
        IScheduler scheduler = await factory.GetScheduler();

        await scheduler.Start();

        // HelloJob: logs a greeting
        IJobDetail helloJob = JobBuilder.Create<HelloJob>()
                                        .WithIdentity("helloJob", "group1")
                                        .Build();

        ITrigger helloTrigger = TriggerBuilder.Create()
                                              .WithIdentity("helloTrigger", "group1")
                                              .StartNow()
                                              .WithSimpleSchedule(x => x
                                                                     .WithIntervalInSeconds(60))
                                              .Build();

        await scheduler.ScheduleJob(helloJob, helloTrigger);

        // ExceptionJob: throws an exception
        IJobDetail exceptionJob = JobBuilder.Create<ExceptionJob>()
                                            .WithIdentity("exceptionJob", "group2")
                                            .Build();

        ITrigger exceptionTrigger = TriggerBuilder.Create()
                                                  .WithIdentity("exceptionTrigger", "group2")
                                                  .StartNow()
                                                  .WithSimpleSchedule(x => x
                                                                          .WithIntervalInSeconds(15)
                                                                          .RepeatForever())
                                                  .Build();

        await scheduler.ScheduleJob(exceptionJob, exceptionTrigger);

        await Task.Delay(TimeSpan.FromSeconds(60));

        await scheduler.Shutdown();

        Console.WriteLine("Press any key to close the application");
        Console.ReadKey();

        tracerProvider?.Dispose();
    }

    private class ConsoleLogProvider : ILogProvider
    {
        public Logger GetLogger(string name)
        {
            return (level, func, exception, parameters) =>
            {
                if (level >= LogLevel.Info && func != null)
                {
                    Console.WriteLine("[" + DateTime.Now.ToLongTimeString() + "] [" + level + "] " + func(), parameters);
                }
                return true;
            };
        }

        public IDisposable OpenNestedContext(string message)
        {
            throw new NotImplementedException();
        }

        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            throw new NotImplementedException();
        }
    }
}

// The original HelloJob
public class HelloJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await Console.Out.WriteLineAsync("Greetings from HelloJob!");
    }
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
}
