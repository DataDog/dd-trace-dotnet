using Datadog.Trace;
using Datadog.Trace.Configuration;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tracer = Datadog.Trace.Tracer;

namespace TraceContext.InvalidOperationException
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
                var tracer = new Tracer(ddTraceSettings);

                var totalIterations = 400_000;
                var threadRepresentation = Enumerable.Range(0, 5).ToArray();
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
                                        Span firstSpan;
                                        using (var outerScope = tracer.StartActive("outer-span"))
                                        {
                                            // Save the span so we can later re-use its TraceContext
                                            firstSpan = outerScope.Span;

                                            // Initialize scopes/spans to aggressively open
                                            var threadScopes = new Stack<IDisposable>();
                                            var i = 0;

                                            while (i++ < totalIterations)
                                            {
                                                string spanString = $"inner-span-{i}";
                                                threadScopes.Push(tracer.StartActive(spanString));
                                            }

                                            i = 0;
                                            while (threadScopes.Count > 0)
                                            {
                                                threadScopes.Pop().Dispose();
                                            }
                                        }

                                        Thread.Sleep(500);

                                        // Now that the entire set of spans has been closed and queued
                                        // to be written to the agent, re-open that same TraceContext
                                        // Repeat the operation to trigger the exception
                                        using (var outerScope = tracer.ActivateSpan(firstSpan))
                                        {
                                            // Initialize scopes/spans to aggressively open
                                            var threadScopes = new Stack<IDisposable>();
                                            var i = 0;

                                            while (i++ < totalIterations)
                                            {
                                                string spanString = $"second inner-span-{i}";
                                                threadScopes.Push(tracer.StartActive(spanString));
                                            }

                                            i = 0;
                                            while (threadScopes.Count > 0)
                                            {
                                                threadScopes.Pop().Dispose();
                                            }
                                        }

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
                    Thread.Sleep(1000);
                }

                if (exceptionBag.Any())
                {
                    // No exceptions are acceptable
                    throw new AggregateException(exceptionBag.ToArray());
                }

                var loggingEvents = InMemoryLog4NetLogger.InMemoryAppender.GetEvents();
                var invalidOperationExceptionEvents = loggingEvents.Where(e => e.RenderedMessage.Contains("InvalidOperationException"));
                int invalidOperationExceptionCount = invalidOperationExceptionEvents.Count();

                Console.WriteLine($"Expecting {expectedLogCount} total log events.");
                Console.WriteLine($"Received {loggingEvents.Length} total log events.");
                Console.WriteLine($"Received {invalidOperationExceptionCount} log events containing 'InvalidOperationException'.");

                if (loggingEvents.Length != expectedLogCount && invalidOperationExceptionEvents.Count() > 0)
                {
                    throw new Exception($"Expected log count: {expectedLogCount}, actual log count: {loggingEvents.Length}, logs containing 'InvalidOperationException': {invalidOperationExceptionCount}");
                }

                Console.WriteLine("All is well!");
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
