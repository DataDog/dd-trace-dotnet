using log4net;
using Samples;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DogStatsD.RaceCondition
{
    class Program
    {
        internal static readonly string ThreadFinishedMessage = "The current thread has finished";
        private static readonly Type GlobalSettingsType = Type.GetType("Datadog.Trace.Configuration.GlobalSettings, Datadog.Trace");
        private static readonly MethodInfo SetDebugEnabledMethod = GlobalSettingsType?.GetMethod("SetDebugEnabled");
        static int Main(string[] args)
        {
            try
            {
                InMemoryLog4NetLogger.Setup();
                var logger = LogManager.GetLogger(typeof(Program));

                

                Environment.SetEnvironmentVariable("DD_TRACE_METRICS_ENABLED", "true");
                Environment.SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
                SetDebugEnabledMethod.Invoke(null, new object[] { true });
                SampleHelpers.ConfigureTracer("DogStatsD.RaceCondition");
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
                                            using (var outerScope = SampleHelpers.CreateScope("outer-span"))
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
