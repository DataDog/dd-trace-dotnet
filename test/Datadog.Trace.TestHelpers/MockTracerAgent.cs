// <copyright file="MockTracerAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using MessagePack;

namespace Datadog.Trace.TestHelpers
{
    public class MockTracerAgent : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly UdpClient _udpClient;
        private readonly Thread _listenerThread;
        private readonly Thread _statsdThread;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MockTracerAgent(int port = 8126, int retries = 5, bool useStatsd = false)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            if (useStatsd)
            {
                const int basePort = 11555;

                var retriesLeft = retries;

                while (true)
                {
                    try
                    {
                        _udpClient = new UdpClient(basePort + retriesLeft);
                    }
                    catch (Exception) when (retriesLeft > 0)
                    {
                        retriesLeft--;
                        continue;
                    }

                    _statsdThread = new Thread(HandleStatsdRequests) { IsBackground = true };
                    _statsdThread.Start();

                    StatsdPort = basePort + retriesLeft;

                    break;
                }
            }

            // try up to 5 consecutive ports before giving up
            while (true)
            {
                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
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
                    port = TcpPortProvider.GetOpenPort();
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        public event EventHandler<EventArgs<IList<IList<Span>>>> RequestDeserialized;

        /// <summary>
        /// Gets the TCP port that this Agent is listening on.
        /// Can be different from <see cref="MockTracerAgent(int, int)"/>'s <c>initialPort</c>
        /// parameter if listening on that port fails.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the UDP port for statsd
        /// </summary>
        public int StatsdPort { get; }

        /// <summary>
        /// Gets the filters used to filter out spans we don't want to look at for a test.
        /// </summary>
        public List<Func<Span, bool>> SpanFilters { get; private set; } = new List<Func<Span, bool>>();

        public IImmutableList<Span> Spans { get; private set; } = ImmutableList<Span>.Empty;

        public IImmutableList<NameValueCollection> RequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public ConcurrentQueue<string> StatsdRequests { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// Gets or sets a value indicating whether to skip deserialization of traces.
        /// </summary>
        public bool ShouldDeserializeTraces { get; set; } = true;

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
            DateTimeOffset? minDateTime = null,
            bool returnAllOperations = false)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            var minimumOffset = (minDateTime ?? DateTimeOffset.MinValue).ToUnixTimeNanoseconds();

            IImmutableList<Span> relevantSpans = ImmutableList<Span>.Empty;

            while (DateTime.Now < deadline)
            {
                relevantSpans =
                    Spans
                       .Where(s => SpanFilters.All(shouldReturn => shouldReturn(s)))
                       .Where(s => s.Start > minimumOffset)
                       .ToImmutableList();

                if (relevantSpans.Count(s => operationName == null || s.Name == operationName) >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            foreach (var headers in RequestHeaders)
            {
                // This is the place to check against headers we expect
                AssertHeader(
                    headers,
                    "X-Datadog-Trace-Count",
                    header =>
                    {
                        if (int.TryParse(header, out var traceCount))
                        {
                            return traceCount >= 0;
                        }

                        return false;
                    });
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
            _cancellationTokenSource.Cancel();
            _udpClient?.Close();
        }

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            RequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(context));
        }

        protected virtual void OnRequestDeserialized(IList<IList<Span>> traces)
        {
            RequestDeserialized?.Invoke(this, new EventArgs<IList<IList<Span>>>(traces));
        }

        private bool IsAppSecTrace(HttpListenerContext context) => context.Request.Headers[AppSec.Transports.Sender.AppSecHeaderKey] != null;

        private void AssertHeader(
            NameValueCollection headers,
            string headerKey,
            Func<string, bool> assertion)
        {
            var header = headers.Get(headerKey);

            if (string.IsNullOrEmpty(header))
            {
                throw new Exception($"Every submission to the agent should have a {headerKey} header.");
            }

            if (!assertion(header))
            {
                throw new Exception($"Failed assertion for {headerKey} on {header}");
            }
        }

        private void HandleStatsdRequests()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 0);

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var buffer = _udpClient.Receive(ref endPoint);

                    StatsdRequests.Enqueue(Encoding.UTF8.GetString(buffer));
                }
                catch (Exception) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    OnRequestReceived(ctx);
                    var shouldDeserializeTraces = ShouldDeserializeTraces && !IsAppSecTrace(ctx);
                    if (shouldDeserializeTraces)
                    {
                        var spans = MessagePackSerializer.Deserialize<IList<IList<Span>>>(ctx.Request.InputStream);
                        OnRequestDeserialized(spans);

                        lock (this)
                        {
                            // we only need to lock when replacing the span collection,
                            // not when reading it because it is immutable
                            Spans = Spans.AddRange(spans.SelectMany(trace => trace));
                            RequestHeaders = RequestHeaders.Add(new NameValueCollection(ctx.Request.Headers));
                        }
                    }

                    // NOTE: HttpStreamRequest doesn't support Transfer-Encoding: Chunked
                    // (Setting content-length avoids that)

                    ctx.Response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes("{}");
                    ctx.Response.ContentLength64 = buffer.LongLength;
                    ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    ctx.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // listener was stopped,
                    // ignore to let the loop end and the method return
                }
                catch (ObjectDisposedException)
                {
                    // the response has been already disposed.
                }
                catch (InvalidOperationException)
                {
                    // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                    // for now ignore, and we'll see if this introduces downstream issues
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    // we don't care about any exception when listener is stopped
                }
            }
        }

        [MessagePackObject]
        [DebuggerDisplay("TraceId={TraceId}, SpanId={SpanId}, Service={Service}, Name={Name}, Resource={Resource}")]
        public class Span
        {
            [Key("trace_id")]
            public ulong TraceId { get; set; }

            [Key("span_id")]
            public ulong SpanId { get; set; }

            [Key("name")]
            public string Name { get; set; }

            [Key("resource")]
            public string Resource { get; set; }

            [Key("service")]
            public string Service { get; set; }

            [Key("type")]
            public string Type { get; set; }

            [Key("start")]
            public long Start { get; set; }

            [Key("duration")]
            public long Duration { get; set; }

            [Key("parent_id")]
            public ulong? ParentId { get; set; }

            [Key("error")]
            public byte Error { get; set; }

            [Key("meta")]
            public Dictionary<string, string> Tags { get; set; }

            [Key("metrics")]
            public Dictionary<string, double> Metrics { get; set; }

            public Span WithTag(string key, string value)
            {
                Tags[key] = value;
                return this;
            }

            public Span WithMetric(string key, double value)
            {
                Metrics[key] = value;
                return this;
            }

            public override string ToString()
            {
                return $"TraceId={TraceId}, SpanId={SpanId}, Service={Service}, Name={Name}, Resource={Resource}, Type={Type}";
            }
        }
    }
}
