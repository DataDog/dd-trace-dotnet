using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using MessagePack;

namespace Datadog.Trace.TestHelpers
{
    public class MockTracerAgent : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;

        public MockTracerAgent(int port = 8126, int retries = 5)
        {
            // try up to 5 consecutive ports before giving up
            while (true)
            {
                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");

                try
                {
                    listener.Start();

                    // successfully listening
                    Port = port;
                    _listener = listener;

                    _listenerThread = new Thread(HandleHttpRequests);
                    _listenerThread.Start();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port++;
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        /// <summary>
        /// Gets the TCP port that this Agent is listening on.
        /// Can be different from <see cref="MockTracerAgent(int, int)"/>'s <c>initialPort</c>
        /// parameter if listening on that port fails.
        /// </summary>
        public int Port { get; }

        public IImmutableList<Span> Spans { get; private set; } = ImmutableList<Span>.Empty;

        /// <summary>
        /// Wait for the given number of spans to appear.
        /// </summary>
        /// <param name="count">The expected number of spans.</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <param name="operationName">The integration we're testing</param>
        /// <param name="minDateTime">Minimum time to check for spans from</param>
        /// <param name="returnAllOperations">When true, returns every span regardless of operation name</param>
        /// <returns>The list of spans.</returns>
        public IImmutableList<Span> WaitForSpans(
            int count,
            int timeoutInMilliseconds = 20000,
            string operationName = null,
            DateTime? minDateTime = null,
            bool returnAllOperations = false)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);

            var minimumOffset =
                new DateTimeOffset(minDateTime ?? DateTime.MinValue).ToUnixTimeNanoseconds();

            IImmutableList<Span> relevantSpans = null;

            while (DateTime.Now < deadline)
            {
                relevantSpans =
                    Spans
                       .Where(s => s.Start > minimumOffset)
                       .ToImmutableList();

                if (relevantSpans.Count(s => operationName == null || s.Name == operationName) >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (!returnAllOperations)
            {
                relevantSpans =
                    relevantSpans
                          .Where(s => operationName == null || s.Name == operationName)
                          .ToImmutableList();
            }

            return relevantSpans;
        }

        public void Dispose()
        {
            _listener?.Stop();
        }

        private static List<Span> ToSpans(dynamic data)
        {
            if (data is IDictionary dict)
            {
                var span = new Span
                {
                    TraceId = dict.Get<ulong>("trace_id"),
                    SpanId = dict.Get<ulong>("span_id"),
                    Name = dict.Get<string>("name"),
                    Resource = dict.Get<string>("resource"),
                    Service = dict.Get<string>("service"),
                    Type = dict.Get<string>("type"),
                    Start = dict.Get<long>("start"),
                    Duration = dict.Get<ulong>("duration"),
                    Tags = dict.Get<Dictionary<string, string>>("meta"),
                };

                return new List<Span> { span };
            }

            if (data is IEnumerable rawSpans)
            {
                var allSpans = new List<Span>();

                foreach (var rawSpan in rawSpans)
                {
                    allSpans.AddRange(ToSpans(rawSpan));
                }

                return allSpans;
            }

            return new List<Span>();
        }

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    var rawSpans = MessagePackSerializer.Deserialize<dynamic>(ctx.Request.InputStream);
                    var spans = ToSpans(rawSpans);

                    lock (this)
                    {
                        // we only need to lock when replacing the span collection,
                        // not when reading it because it is immutable
                        Spans = Spans.AddRange(spans);
                    }

                    ctx.Response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes("{}");
                    ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    ctx.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // listener was stopped,
                    // ignore to let the loop end and the method return
                }
            }
        }

        [DebuggerDisplay("TraceId={TraceId}, SpanId={SpanId}, Service={Service}, Name={Name}, Resource={Resource}")]
        public struct Span
        {
            public ulong TraceId { get; set; }

            public ulong SpanId { get; set; }

            public string Name { get; set; }

            public string Resource { get; set; }

            public string Service { get; set; }

            public string Type { get; set; }

            public long Start { get; set; }

            public ulong Duration { get; set; }

            public Dictionary<string, string> Tags { get; set; }
        }
    }
}
