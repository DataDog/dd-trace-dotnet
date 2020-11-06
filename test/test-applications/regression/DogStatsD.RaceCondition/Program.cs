using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;
using Tracer = Datadog.Trace.Tracer;

namespace DogStatsD.RaceCondition
{
    class Program
    {
        internal static readonly string ThreadFinishedMessage = "The current thread has finished";

        static int Main(string[] args)
        {
            try
            {
                InMemoryLog4NetLogger.Setup();
                var logger = LogManager.GetLogger(typeof(Program));
                var ddTraceSettings = TracerSettings.FromDefaultSources();
                ddTraceSettings.AnalyticsEnabled = true;
                ddTraceSettings.LogsInjectionEnabled = true;
                ddTraceSettings.TraceEnabled = true;
                ddTraceSettings.TracerMetricsEnabled = true;
                GlobalSettings.SetDebugEnabled(true);

                var tracer = new Tracer(ddTraceSettings);
                var totalIterations = 100;
                var threadRepresentation = Enumerable.Range(0, 25).ToArray();
                var threadCount = threadRepresentation.Length;

                // Two logs per thread iteration + 1 extra log at the end of each thread
                var expectedLogCount = threadCount;
                var exceptionBag = new ConcurrentBag<Exception>();

                Console.WriteLine($"Running {threadRepresentation.Length} threads with {totalIterations} iterations.");

                var threads =
                    threadRepresentation
                       .Select(
                            idx => new Thread(
                                thread =>
                                {
                                    try
                                    {
                                        var i = 0;

                                        while (i++ < totalIterations)
                                        {
                                            using (var outerScope = tracer.StartActive("outer-span"))
                                            {
                                            }
                                        }

                                        Thread.Sleep(100);

                                        // Verify everything is cleaned up on this thread
                                        logger.Info(ThreadFinishedMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        exceptionBag.Add(ex);
                                    }
                                }))
                       .ToList();

                foreach (var thread in threads)
                {
                    thread.Start();
                }

                while (threads.Any(x => x.IsAlive))
                {
                    Thread.Sleep(500);
                }

                if (exceptionBag.Any())
                {
                    // No exceptions are acceptable
                    throw new AggregateException(exceptionBag.ToArray());
                }

                var loggingEvents = InMemoryLog4NetLogger.InMemoryAppender.GetEvents();
                var systemOutOfRangeException = loggingEvents.Where(e => e.RenderedMessage.Contains("Index was outside the bounds of the array"));
                int systemOutOfRangeExceptionCount = systemOutOfRangeException.Count();

                Console.WriteLine($"Received {systemOutOfRangeExceptionCount} log events containing 'Index was outside the bounds of the array'.");

                if (systemOutOfRangeException.Count() > 0)
                {
                    throw new Exception("Got exception with 'System.IndexOutOfRangeException'");
                }

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
