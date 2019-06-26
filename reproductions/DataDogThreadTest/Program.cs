using System;
using Datadog.Trace;
using log4net;

namespace DataDogThreadTest
{
    using System.Linq;
    using System.Threading;

    using Datadog.Trace.Configuration;

    using Tracer = Datadog.Trace.Tracer;

    class Program
    {
        internal static readonly string TraceIdKey = "dd.trace_id";
        internal static readonly string SpanIdKey = "dd.span_id";

        static void Main(string[] args)
        {
            InMemoryLog4NetLogger.Setup();
            var logger = LogManager.GetLogger(typeof(Program));

            var ddTraceSettings = TracerSettings.FromDefaultSources();
            ddTraceSettings.AnalyticsEnabled = true;
            ddTraceSettings.LogsInjectionEnabled = true;
            ddTraceSettings.TraceEnabled = true;
            var tracer = new Tracer(ddTraceSettings);

            var totalIterations = 10_000;
            var threadRepresentation = Enumerable.Range(0, 2).ToArray();

            // Two logs per thread iteration
            var expectedLogCount = totalIterations * threadRepresentation.Length * 2;

            var threads = threadRepresentation.Select(idx =>
            {
                return new Thread(o =>
                {
                    Thread.Sleep(2000);
                    var i = 0;
                    while (i++ < totalIterations)
                    {
                        try
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
                        catch (Exception ex)
                        {
                           Console.WriteLine($"Error making span. {ex}");
                        }
                    }
                });
            }).ToList();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            while (threads.Any(x => x.IsAlive))
            {
                Thread.Sleep(1000);
            }

            var loggingEvents = InMemoryLog4NetLogger.InMemoryAppender.GetEvents();

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

            // Test non-traced logging event
            logger.Info("TraceId: 0, SpanId: 0");

            var lastLog = InMemoryLog4NetLogger.InMemoryAppender.GetEvents().Last();

            var expectedOutOfTraceLog = "TraceId: 0, SpanId: 0";

            var lastLogTraceId = lastLog.Properties[TraceIdKey];
            var lastLogSpanIdId = lastLog.Properties[SpanIdKey];
            var actual = $"TraceId: {lastLogTraceId}, SpanId: {lastLogSpanIdId}";

            if (!actual.Equals(expectedOutOfTraceLog))
            {
                throw new Exception($"Unexpected TraceId or SpanId: {actual}");
            }

            Console.WriteLine("Done");
        }
    }
}
