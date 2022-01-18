// <copyright file="MockTracerAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;
using MessagePack; // use nuget MessagePack to deserialize
using Xunit.Abstractions;

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
        private readonly UnixDomainSocketEndPoint _tracesEndpoint;
        private readonly Socket _udsTracesSocket;
        private readonly UnixDomainSocketEndPoint _statsEndpoint;
        private readonly Socket _udsStatsSocket;

        public MockTracerAgent(UnixDomainSocketConfig config)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            ListenerInfo = $"Traces at {config.Traces}";

            if (config.Metrics != null && config.UseDogstatsD)
            {
                if (File.Exists(config.Metrics))
                {
                    File.Delete(config.Metrics);
                }

                StatsUdsPath = config.Metrics;
                ListenerInfo += $", Stats at {config.Metrics}";
                _statsEndpoint = new UnixDomainSocketEndPoint(config.Metrics);

                _udsStatsSocket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);

                _udsStatsSocket.Bind(_statsEndpoint);
                // NOTE: Connectionless protocols don't use Listen()
                _statsdThread = new Thread(HandleUdsStats) { IsBackground = true };
                _statsdThread.Start();
            }

            _tracesEndpoint = new UnixDomainSocketEndPoint(config.Traces);

            if (File.Exists(config.Traces))
            {
                File.Delete(config.Traces);
            }

            TracesUdsPath = config.Traces;
            _udsTracesSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            _udsTracesSocket.Bind(_tracesEndpoint);
            _udsTracesSocket.Listen(1);
            _tracesListenerThread = new Thread(HandleUdsTraces) { IsBackground = true };
            _tracesListenerThread.Start();
        }
#endif

        public MockTracerAgent(WindowsPipesConfig config)
        {
            throw new NotImplementedException("Windows named pipes are not yet implemented in the MockTracerAgent");
        }

        public MockTracerAgent(int port = 8126, int retries = 5, bool useStatsd = false, bool doNotBindPorts = false, int? requestedStatsDPort = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            if (doNotBindPorts)
            {
                // This is for any tests that want to use a specific port but never actually bind
                Port = port;
                return;
            }

            var listeners = new List<string>();

            if (useStatsd)
            {
                if (requestedStatsDPort != null)
                {
                    // This port is explicit, allow failure if not available
                    StatsdPort = requestedStatsDPort.Value;
                    _udpClient = new UdpClient(requestedStatsDPort.Value);
                }
                else
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

                        StatsdPort = basePort + retriesLeft;
                        break;
                    }
                }

                _statsdThread = new Thread(HandleStatsdRequests) { IsBackground = true };
                _statsdThread.Start();

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

                var containerHostname = EnvironmentHelpers.GetEnvironmentVariable("CONTAINER_HOSTNAME");
                if (containerHostname != null)
                {
                    listener.Prefixes.Add($"{containerHostname}:{port}/");
                }

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

        public event EventHandler<EventArgs<string>> MetricsReceived;

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

        public string TracesWindowsPipeName { get; }

        public string StatsWindowsPipeName { get; }

        /// <summary>
        /// Gets the filters used to filter out spans we don't want to look at for a test.
        /// </summary>
        public List<Func<MockSpan, bool>> SpanFilters { get; } = new();

        public ConcurrentBag<Exception> Exceptions { get; private set; } = new ConcurrentBag<Exception>();

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
            if (_udsTracesSocket != null)
            {
                IgnoreException(() => _udsTracesSocket.Shutdown(SocketShutdown.Both));
                IgnoreException(() => _udsTracesSocket.Close());
                IgnoreException(() => _udsTracesSocket.Dispose());
                IgnoreException(() => File.Delete(TracesUdsPath));
            }

            if (_udsStatsSocket != null)
            {
                // In versions before net6, dispose doesn't shutdown this socket type for some reason
                IgnoreException(() => _udsStatsSocket.Shutdown(SocketShutdown.Both));
                IgnoreException(() => _udsStatsSocket.Close());
                IgnoreException(() => _udsStatsSocket.Dispose());
                IgnoreException(() => File.Delete(StatsUdsPath));
            }
#endif
        }

        protected void IgnoreException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Exceptions.Add(ex);
            }
        }

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            RequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(context));
        }

        protected virtual void OnRequestDeserialized(IList<IList<MockSpan>> traces)
        {
            RequestDeserialized?.Invoke(this, new EventArgs<IList<IList<MockSpan>>>(traces));
        }

        protected virtual void OnMetricsReceived(string stats)
        {
            MetricsReceived?.Invoke(this, new EventArgs<string>(stats));
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
                    var stats = Encoding.UTF8.GetString(buffer);
                    OnMetricsReceived(stats);
                    StatsdRequests.Enqueue(stats);
                }
                catch (Exception) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Exceptions.Add(ex);
                }
            }
        }

#if NETCOREAPP
        private byte[] GetResponseBytes()
        {
            var responseBody = Encoding.UTF8.GetBytes("{}");
            var contentLength64 = responseBody.LongLength;

            var response = $"HTTP/1.1 200 OK";
            response += DatadogHttpValues.CrLf;
            response += $" Date: {DateTime.UtcNow.ToString("ddd, dd MMM yyyy H:mm::ss K")}";
            response += DatadogHttpValues.CrLf;
            response += $"Connection: Keep-Alive";
            response += DatadogHttpValues.CrLf;
            response += $"Server: dd-mock-agent";
            response += DatadogHttpValues.CrLf;
            response += $"Content-Type: application/json";
            response += DatadogHttpValues.CrLf;
            response += $"Content-Length: {contentLength64}";
            response += DatadogHttpValues.CrLf;
            response += DatadogHttpValues.CrLf;
            response += Encoding.ASCII.GetString(responseBody);

            var responseBytes = Encoding.UTF8.GetBytes(response);
            return responseBytes;
        }

        private void HandlePotentialTraces(MockHttpParser.MockHttpRequest request)
        {
            if (ShouldDeserializeTraces && request.ContentLength > 1)
            {
                byte[] body = null;
                IList<IList<MockSpan>> spans = null;

                try
                {
                    var i = 0;
                    body = new byte[request.ContentLength];
                    var moreBytesThanContentLength = false;
                    while (request.Body.Stream.CanRead)
                    {
                        var nextByte = request.Body.Stream.ReadByte();

                        if (nextByte == -1)
                        {
                            break;
                        }
                        else if (i < request.ContentLength)
                        {
                            body[i] = (byte)nextByte;
                        }
                        else
                        {
                            moreBytesThanContentLength = true;
                        }

                        i++;
                    }

                    if (moreBytesThanContentLength)
                    {
                        throw new Exception($"More bytes were sent than we counted. {i} read versus {request.ContentLength} expected.");
                    }
                    else if (i < request.ContentLength)
                    {
                        throw new Exception($"Less bytes were sent than we counted. {i} read versus {request.ContentLength} expected.");
                    }

                    spans = MessagePackSerializer.Deserialize<IList<IList<MockSpan>>>(body);
                    OnRequestDeserialized(spans);

                    lock (this)
                    {
                        // we only need to lock when replacing the span collection,
                        // not when reading it because it is immutable
                        Spans = Spans.AddRange(spans.SelectMany(trace => trace));

                        var headerCollection = new NameValueCollection();
                        foreach (var header in request.Headers)
                        {
                            headerCollection.Add(header.Name, header.Value);
                        }

                        RequestHeaders = RequestHeaders.Add(headerCollection);
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                    }

                    throw;
                }
            }
        }

        private void HandleUdsStats()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var bytesReceived = new byte[0x1000];
                    // Connectionless protocol doesn't need Accept, Receive will block until we get something
                    var byteCount = _udsStatsSocket.Receive(bytesReceived);
                    var stats = Encoding.UTF8.GetString(bytesReceived, 0, byteCount);
                    OnMetricsReceived(stats);
                    StatsdRequests.Enqueue(stats);
                }
                catch (Exception) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Exceptions.Add(ex);
                }
            }
        }

        private void HandleUdsTraces()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    using (var handler = _udsTracesSocket.Accept())
                    {
                        handler.Send(GetResponseBytes());
                        var stream = new NetworkStream(handler);
                        var requestTask = MockHttpParser.ReadRequest(stream);
                        requestTask.Wait();
                        var request = requestTask.Result;
                        HandlePotentialTraces(request);
                    }
                }
                catch (SocketException ex)
                {
                    var message = ex.Message.ToLowerInvariant();
                    if (message.Contains("interrupted"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    if (message.Contains("broken") || message.Contains("forcibly closed") || message.Contains("invalid argument"))
                    {
                        // The application was likely shut down
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
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
