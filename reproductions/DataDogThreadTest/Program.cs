using Datadog.Trace.Configuration;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using log4net.Core;
using Tracer = Datadog.Trace.Tracer;

namespace DataDogThreadTest
{
    class Program
    {
        internal static readonly string TraceIdKey = "dd.trace_id";
        internal static readonly string SpanIdKey = "dd.span_id";
        internal static readonly string NonTraceMessage = "TraceId: 0, SpanId: 0";

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

                var totalIterations = 10_000;
                var threadRepresentation = Enumerable.Range(0, 10).ToArray();
                var threadCount = threadRepresentation.Length;

                // Two logs per thread iteration + 1 extra log at the end of each thread
                var expectedLogCount = (totalIterations * threadCount * 2) + threadCount;
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
                                        Thread.Sleep(2000);
                                        var i = 0;
                                        while (i++ < totalIterations)
                                        {
                                            using (var outerScope = tracer.StartActive("thread-test"))
                                            {
                                                var outerTraceId = outerScope.Span.TraceId;
                                                var outerSpanId = outerScope.Span.SpanId;

                                                logger.Info($"TraceId: {outerTraceId}, SpanId: {outerSpanId}");

                                                using (var innerScope = tracer.StartActive("nest-thread-test"))
                                                {
                                                    var innerTraceId = innerScope.Span.TraceId;
                                                    var innerSpanId = innerScope.Span.SpanId;

                                                    if (outerTraceId != innerTraceId)
                                                    {
                                                        throw new Exception($"TraceId mismatch - outer: {outerTraceId}, inner: {innerTraceId}");
                                                    }

                                                    if (outerSpanId == innerSpanId)
                                                    {
                                                        throw new Exception($"Unexpected SpanId match - outer: {outerSpanId}, inner: {innerSpanId}");
                                                    }

                                                    logger.Info($"TraceId: {innerTraceId}, SpanId: {innerSpanId}");
                                                }
                                            }
                                        }

                                        // Verify everything is cleaned up on this thread
                                        logger.Info(NonTraceMessage);
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

                var loggingEvents = RelevantLogs();

                foreach (var group in loggingEvents.Where(e => e.RenderedMessage != NonTraceMessage).GroupBy(e => e.RenderedMessage))
                {
                    var message = group.First().RenderedMessage;
                    if (group.Count() > 1)
                    {
                        Console.WriteLine($"Has duplicate log entries ({group.Count()}): {message}");
                    }
                }

                Console.WriteLine($"Expecting {expectedLogCount} total log events.");
                Console.WriteLine($"Received {loggingEvents.Length} total log events.");

                if (loggingEvents.Length != expectedLogCount)
                {
                    throw new Exception($"Expected {expectedLogCount}, actual log count {loggingEvents.Length}");
                }

                foreach (var loggingEvent in loggingEvents)
                {
                    var attachedTraceId = loggingEvent.Properties[TraceIdKey];
                    var attachedSpanIdId = loggingEvent.Properties[SpanIdKey];
                    var expectedMessage = $"TraceId: {attachedTraceId}, SpanId: {attachedSpanIdId}";
                    if (expectedMessage.Equals(loggingEvent.RenderedMessage))
                    {
                        // all is well
                        continue;
                    }

                    throw new Exception($"LOGGING EVENT DOES NOT MATCH ({attachedTraceId}, {attachedSpanIdId}): {loggingEvent.RenderedMessage}");
                }

                Console.WriteLine("Every trace wrapped logging event has the expected TraceId and SpanId.");

                // Test non-traced logging event
                logger.Info(NonTraceMessage);

                var lastLog = RelevantLogs().Last();
                var expectedOutOfTraceLog = "TraceId: 0, SpanId: 0";
                var lastLogTraceId = lastLog.Properties[TraceIdKey];
                var lastLogSpanIdId = lastLog.Properties[SpanIdKey];
                var actual = $"TraceId: {lastLogTraceId}, SpanId: {lastLogSpanIdId}";

                if (!actual.Equals(expectedOutOfTraceLog))
                {
                    throw new Exception($"Unexpected TraceId or SpanId: {actual}");
                }

                Console.WriteLine("Non-trace wrapped logging event has 0 for TraceId and SpanId.");
                Console.WriteLine("All is well!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

        private static LoggingEvent[] RelevantLogs()
        {
            var loggingEvents = InMemoryLog4NetLogger.InMemoryAppender.GetEvents();
            var relevantLogEvents = loggingEvents.Where(e => e.RenderedMessage.Contains("TraceId: ")).ToArray();
            return relevantLogEvents;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = 10
        }
    }
}
