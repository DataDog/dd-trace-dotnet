using log4net;
using Samples;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace DogStatsD.RaceCondition
{
    class Program
    {
        internal static readonly string ThreadFinishedMessage = "The current thread has finished";
        private static readonly string TraceMetrics = "DD_TRACE_METRICS_ENABLED";
        private static readonly string LogsInjection = "DD_LOGS_INJECTION";
        private static readonly string DebugEnabled = "DD_TRACE_DEBUG";

        static int Main(string[] args)
        {
            try
            {
                InMemoryLog4NetLogger.Setup();
                var logger = LogManager.GetLogger(typeof(Program));

                // validate that our Environment Variables are set
                // the tracer is already initialized at this point, so we can't set them here via .SetEnvironmentVariable
                var envVars = Environment.GetEnvironmentVariables();
                if (!envVars.Contains(TraceMetrics) || !envVars.Contains(LogsInjection) || !envVars.Contains(DebugEnabled))
                {
                    throw new Exception($"Make sure the following environment variables are set to \"true\": {TraceMetrics}, {LogsInjection}, {DebugEnabled}");
                }

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
