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
using MessagePack; // use nuget MessagePack to deserialize

namespace Datadog.Trace.TestHelpers
{
    public class MockTracerAgent : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly UdpClient _udpClient;
        private readonly Thread _tracesListenerThread;
        private readonly Thread _statsdThread;
        private readonly CancellationTokenSource _cancellationTokenSource;

#if NETCOREAPP
        private readonly UnixDomainSocketEndPoint _tracesPath;
        private readonly Socket _udsTracesServer;
        // private readonly UnixDomainSocketEndPoint _statsEndpoint;
        // private readonly Socket _udsStatsServer;

        public MockTracerAgent(string traceUdsName, string statsUdsName = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            ListenerInfo = $"Traces at {traceUdsName}";

            if (statsUdsName != null)
            {
                ListenerInfo += $", Stats at {statsUdsName}";
                // _statsEndpoint = new UnixDomainSocketEndPoint(statsUdsName);
                // _udsStatsServer = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                // _udsStatsServer.Bind(_statsEndpoint);
                // _statsdThread = new Thread(HandleUdsStats) { IsBackground = true };
                // _statsdThread.Start();
            }

            _tracesPath = new UnixDomainSocketEndPoint(traceUdsName);
            _udsTracesServer = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            _udsTracesServer.Bind(_tracesPath);
            _udsTracesServer.Listen(5);

            _tracesListenerThread = new Thread(HandleUdsTraces) { IsBackground = true };
            _tracesListenerThread.Start();
        }
#endif

        public MockTracerAgent(int port = 8126, int retries = 5, bool useStatsd = false)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var listeners = new List<string>();

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

                listeners.Add($"Stats at port {StatsdPort}");
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

                    listeners.Add($"Traces at port {Port}");
                    _tracesListenerThread = new Thread(HandleHttpRequests);
                    _tracesListenerThread.Start();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port = TcpPortProvider.GetOpenPort();
                    retries--;
                }
                finally
                {
                    ListenerInfo = string.Join(", ", listeners);
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        public event EventHandler<EventArgs<IList<IList<MockSpan>>>> RequestDeserialized;

        public string ListenerInfo { get; }

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

        public string TracesUdsPath { get; }

        public string StatsUdsPath { get; }

        /// <summary>
        /// Gets the filters used to filter out spans we don't want to look at for a test.
        /// </summary>
        public List<Func<MockSpan, bool>> SpanFilters { get; } = new();

        public IImmutableList<MockSpan> Spans { get; private set; } = ImmutableList<MockSpan>.Empty;

        public IImmutableList<NameValueCollection> RequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public ConcurrentQueue<string> StatsdRequests { get; } = new();

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
        public IImmutableList<MockSpan> WaitForSpans(
            int count,
            int timeoutInMilliseconds = 20000,
            string operationName = null,
            DateTimeOffset? minDateTime = null,
            bool returnAllOperations = false)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            var minimumOffset = (minDateTime ?? DateTimeOffset.MinValue).ToUnixTimeNanoseconds();

            IImmutableList<MockSpan> relevantSpans = ImmutableList<MockSpan>.Empty;

            while (DateTime.Now < deadline)
            {
                relevantSpans =
                    Spans
                       .Where(s => SpanFilters.All(shouldReturn => shouldReturn(s)) && s.Start > minimumOffset)
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
#if NETCOREAPP
            _udsTracesServer?.Dispose();
            // _udsStatsServer?.Dispose();
#endif
        }

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            RequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(context));
        }

        protected virtual void OnRequestDeserialized(IList<IList<MockSpan>> traces)
        {
            RequestDeserialized?.Invoke(this, new EventArgs<IList<IList<MockSpan>>>(traces));
        }

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

#if NETCOREAPP
        private void HandleUdsStats()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var bytesReceived = default(Span<byte>);
                    using (var handler = _udsTracesServer.Accept())
                    {
                        using (var ns = new NetworkStream(handler))
                        {
                            ns.Read(bytesReceived);
                        }
                    }

                    StatsdRequests.Enqueue(Encoding.UTF8.GetString(bytesReceived));
                }
                catch (Exception) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void HandleUdsTraces()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var bytesReceived = default(Span<byte>);
                    string request;
                    using (var handler = _udsTracesServer.Accept())
                    {
                        using (var ns = new NetworkStream(handler))
                        {
                            var bytes = ns.Read(bytesReceived);
                            request = Encoding.ASCII.GetString(bytesReceived);
                            using (var writer = new StreamWriter(ns))
                            {
                                var responseBody = Encoding.UTF8.GetBytes("{}");
                                var contentLength64 = responseBody.LongLength;

                                var response = $"HTTP/1.1 200 OK";
                                response += Environment.NewLine;
                                response += $" Date: {DateTime.UtcNow.ToString("ddd, dd MMM yyyy H:mm::ss K")}";
                                response += Environment.NewLine;
                                response += $"Connection: Keep-Alive";
                                response += Environment.NewLine;
                                response += $"Server: Apache";
                                response += Environment.NewLine;
                                response += $"Content-Type: application/json";
                                response += Environment.NewLine;
                                response += $"Content-Length: {contentLength64}";
                                response += Environment.NewLine;
                                response += Environment.NewLine;
                                response += Encoding.ASCII.GetString(responseBody);

                                var responseBytes = Encoding.UTF8.GetBytes(response);

                                writer.Write(responseBytes);
                                writer.Flush();
                            }
                        }
                    }

                    var shouldDeserializeTraces = ShouldDeserializeTraces && !string.IsNullOrEmpty(request);
                    if (shouldDeserializeTraces)
                    {
                        var parts = request.Split($"{Environment.NewLine}{Environment.NewLine}");
                        var requestHeaderText = parts[0];
                        var requestHeaders = new NameValueCollection();
                        foreach (var line in requestHeaderText.Split(Environment.NewLine))
                        {
                            var lineParts = line.Split(":");
                            requestHeaders.Add(lineParts[0].Trim(), lineParts[1].Trim());
                        }

                        var requestBody = parts[1];
                        var bodyBytes = Encoding.ASCII.GetBytes(requestBody);
                        var spans = MessagePackSerializer.Deserialize<IList<IList<Span>>>(bodyBytes);
                        OnRequestDeserialized(spans);

                        lock (this)
                        {
                            // we only need to lock when replacing the span collection,
                            // not when reading it because it is immutable
                            Spans = Spans.AddRange(spans.SelectMany(trace => trace));
                            RequestHeaders = RequestHeaders.Add(requestHeaders);
                        }
                    }
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
            }
        }
#endif

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    OnRequestReceived(ctx);
                    if (ShouldDeserializeTraces)
                    {
                        var spans = MessagePackSerializer.Deserialize<IList<IList<MockSpan>>>(ctx.Request.InputStream);
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
    }
}
