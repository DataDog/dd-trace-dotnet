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
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using MessagePack; // use nuget MessagePack to deserialize
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public abstract class MockTracerAgent : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private AgentBehaviour behaviour = AgentBehaviour.Normal;

        protected MockTracerAgent(bool telemetryEnabled, TestTransports transport)
        {
            TelemetryEnabled = telemetryEnabled;
            TransportType = transport;
        }

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        public event EventHandler<EventArgs<IList<IList<MockSpan>>>> RequestDeserialized;

        public event EventHandler<EventArgs<MockClientStatsPayload>> StatsDeserialized;

        public event EventHandler<EventArgs<string>> MetricsReceived;

        public string ListenerInfo { get; protected set; }

        public TestTransports TransportType { get; }

        public string Version { get; set; }

        public bool TelemetryEnabled { get; }

        public string RcmResponse { get; set; }

        /// <summary>
        /// Gets the filters used to filter out spans we don't want to look at for a test.
        /// </summary>
        public List<Func<MockSpan, bool>> SpanFilters { get; } = new();

        public ConcurrentBag<Exception> Exceptions { get; private set; } = new ConcurrentBag<Exception>();

        public IImmutableList<MockSpan> Spans { get; private set; } = ImmutableList<MockSpan>.Empty;

        public IImmutableList<MockClientStatsPayload> Stats { get; private set; } = ImmutableList<MockClientStatsPayload>.Empty;

        public IImmutableList<MockDataStreamsPayload> DataStreams { get; private set; } = ImmutableList<MockDataStreamsPayload>.Empty;

        public IImmutableList<NameValueCollection> TraceRequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public List<string> Snapshots { get; private set; } = new();

        public List<string> ProbesStatuses { get; private set; } = new();

        public ConcurrentQueue<string> StatsdRequests { get; } = new();

        /// <summary>
        /// Gets the <see cref="Datadog.Trace.Telemetry.TelemetryData"/> requests received by the telemetry endpoint
        /// </summary>
        public ConcurrentStack<object> Telemetry { get; } = new();

        public ITestOutputHelper Output { get; set; }

        public AgentConfiguration Configuration { get; set; }

        public IImmutableList<NameValueCollection> TelemetryRequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public ConcurrentQueue<string> RemoteConfigRequests { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to skip deserialization of traces.
        /// </summary>
        public bool ShouldDeserializeTraces { get; set; } = true;

        public static TcpUdpAgent Create(ITestOutputHelper output, int port = 8126, int retries = 5, bool useStatsd = false, bool doNotBindPorts = false, int? requestedStatsDPort = null, bool useTelemetry = false, AgentConfiguration agentConfiguration = null)
            => new TcpUdpAgent(port, retries, useStatsd, doNotBindPorts, requestedStatsDPort, useTelemetry) { Output = output, Configuration = agentConfiguration ?? new() };

#if NETCOREAPP3_1_OR_GREATER
        public static UdsAgent Create(ITestOutputHelper output, UnixDomainSocketConfig config, AgentConfiguration agentConfiguration = null) => new UdsAgent(config) { Output = output, Configuration = agentConfiguration ?? new() };
#endif

        public static NamedPipeAgent Create(ITestOutputHelper output, WindowsPipesConfig config, AgentConfiguration agentConfiguration = null) => new NamedPipeAgent(config) { Output = output, Configuration = agentConfiguration ?? new() };

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
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);
            var minimumOffset = (minDateTime ?? DateTimeOffset.MinValue).ToUnixTimeNanoseconds();

            IImmutableList<MockSpan> relevantSpans = ImmutableList<MockSpan>.Empty;

            while (DateTime.UtcNow < deadline)
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

            foreach (var headers in TraceRequestHeaders)
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

                // Ensure only one Content-Type is specified and that it is msgpack
                AssertHeader(
                    headers,
                    "Content-Type",
                    header =>
                    {
                        if (!header.Equals("application/msgpack"))
                        {
                            return false;
                        }

                        return true;
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

        /// <summary>
        /// Wait for the telemetry condition to be satisfied.
        /// Note that the first telemetry that satisfies the condition is returned
        /// To retrieve all telemetry received, use <see cref="Telemetry"/>
        /// </summary>
        /// <param name="hasExpectedValues">A predicate for the current telemetry.
        /// The object passed to the func will be a <see cref="TelemetryData"/> instance</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <param name="sleepTime">The time between checks</param>
        /// <returns>The telemetry that satisfied <paramref name="hasExpectedValues"/></returns>
        public object WaitForLatestTelemetry(
            Func<object, bool> hasExpectedValues,
            int timeoutInMilliseconds = 5000,
            int sleepTime = 200)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            object latest = default;
            while (DateTime.UtcNow < deadline)
            {
                if (Telemetry.TryPeek(out latest) && hasExpectedValues(latest))
                {
                    break;
                }

                Thread.Sleep(sleepTime);
            }

            return latest;
        }

        public IImmutableList<MockClientStatsPayload> WaitForStats(
            int count,
            int timeoutInMilliseconds = 20000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            IImmutableList<MockClientStatsPayload> stats = ImmutableList<MockClientStatsPayload>.Empty;

            while (DateTime.UtcNow < deadline)
            {
                stats = Stats;

                if (stats.Count >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            return stats;
        }

        public IImmutableList<MockDataStreamsPayload> WaitForDataStreams(
            int count,
            int timeoutInMilliseconds = 20000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            IImmutableList<MockDataStreamsPayload> stats = ImmutableList<MockDataStreamsPayload>.Empty;

            while (DateTime.UtcNow < deadline)
            {
                stats = DataStreams;

                if (stats.Count >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            return stats;
        }

        /// <summary>
        /// Wait for the given number of probe snapshots to appear.
        /// </summary>
        /// <param name="snapshotCount">The expected number of probe snapshots when more than one snapshot is expected (e.g. multiple line probes in method).</param>
        /// <param name="timeout">The timeout</param>
        /// <returns>The list of probe snapshots.</returns>
        public async Task<string[]> WaitForSnapshots(int snapshotCount, TimeSpan? timeout = null)
        {
            using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));

            var isFound = false;
            while (!isFound && !cancellationSource.IsCancellationRequested)
            {
                isFound = Snapshots.Count == snapshotCount;

                if (!isFound)
                {
                    await Task.Delay(100);
                }
            }

            if (!isFound)
            {
                throw new InvalidOperationException($"Snapshot count not found. Expected {snapshotCount}, actual {Snapshots.Count}");
            }

            return Snapshots.ToArray();
        }

        public async Task<bool> WaitForNoSnapshots(int timeoutInMilliseconds = 10000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            while (DateTime.Now < deadline)
            {
                if (Snapshots.Any())
                {
                    return false;
                }

                await Task.Delay(100);
            }

            return !Snapshots.Any();
        }

        public void ClearSnapshots()
        {
            Snapshots.Clear();
        }

        /// <summary>
        /// Wait for the given number of probe statuses to appear.
        /// </summary>
        /// <param name="statusCount">The expected number of probe statuses when more than one status is expected (e.g. multiple line probes in method).</param>
        /// <param name="timeout">The timeout</param>
        /// <returns>The list of probe statuses.</returns>
        public async Task<string[]> WaitForProbesStatuses(int statusCount, TimeSpan? timeout = null)
        {
            using var cancellationSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));

            var isFound = false;
            while (!isFound && !cancellationSource.IsCancellationRequested)
            {
                isFound = ProbesStatuses.Count == statusCount;

                if (!isFound)
                {
                    await Task.Delay(100);
                }
            }

            if (!isFound)
            {
                throw new InvalidOperationException($"Probes Status count not found. Expected {statusCount}, actual {ProbesStatuses.Count}");
            }

            return ProbesStatuses.ToArray();
        }

        public void ClearProbeStatuses()
        {
            ProbesStatuses.Clear();
        }

        public virtual void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        public void SetBehaviour(AgentBehaviour behaviour) => this.behaviour = behaviour;

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

        protected virtual void OnStatsDeserialized(MockClientStatsPayload stats)
        {
            StatsDeserialized?.Invoke(this, new EventArgs<MockClientStatsPayload>(stats));
        }

        protected virtual void OnMetricsReceived(string stats)
        {
            MetricsReceived?.Invoke(this, new EventArgs<string>(stats));
        }

        private protected MockTracerResponse HandleHttpRequest(MockHttpParser.MockHttpRequest request)
        {
            string response;
            int statusCode;
            bool sendResponse;
            var isTraceCommand = false;

            if (TelemetryEnabled && request.PathAndQuery.StartsWith("/" + TelemetryConstants.AgentTelemetryEndpoint))
            {
                HandlePotentialTelemetryData(request);
                response = "{}";
            }
            else if (request.PathAndQuery.EndsWith("/info"))
            {
                response = JsonConvert.SerializeObject(Configuration);
            }
            else if (request.PathAndQuery.StartsWith("/debugger/v1/input"))
            {
                HandlePotentialDebuggerData(request);
                response = "{}";
            }
            else if (request.PathAndQuery.StartsWith("/v0.6/stats"))
            {
                HandlePotentialStatsData(request);
                response = "{}";
            }
            else if (request.PathAndQuery.StartsWith("/v0.7/config"))
            {
                HandlePotentialRemoteConfig(request);
                response = RcmResponse ?? "{}";
            }
            else if (request.PathAndQuery.StartsWith("/v0.1/pipeline_stats"))
            {
                HandlePotentialDataStreams(request);
                response = "{}";
            }
            else
            {
                HandlePotentialTraces(request);
                response = "{}";
                isTraceCommand = true;
            }

            if (isTraceCommand)
            {
                statusCode = 200;
                sendResponse = true;
            }
            else
            {
                if (behaviour == AgentBehaviour.WrongAnswer)
                {
                    response = "WRONG_ANSWER";
                }

                sendResponse = behaviour != AgentBehaviour.NoAnswer;
                statusCode = behaviour == AgentBehaviour.Return500 ? 500 : (behaviour == AgentBehaviour.Return404 ? 404 : 200);
            }

            return new MockTracerResponse()
            {
                Response = response,
                SendResponse = sendResponse,
                StatusCode = statusCode
            };
        }

        private void HandlePotentialTraces(MockHttpParser.MockHttpRequest request)
        {
            if (ShouldDeserializeTraces && request.ContentLength >= 1)
            {
                try
                {
                    var body = ReadStreamBody(request);

                    var spans = MessagePackSerializer.Deserialize<IList<IList<MockSpan>>>(body);
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

                        TraceRequestHeaders = TraceRequestHeaders.Add(headerCollection);
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
            }
        }

        private void HandlePotentialTelemetryData(MockHttpParser.MockHttpRequest request)
        {
            if (request.ContentLength >= 1)
            {
                try
                {
                    var body = ReadStreamBody(request);
                    using var stream = new MemoryStream(body);

                    var telemetry = MockTelemetryAgent<TelemetryData>.DeserializeResponse(stream);
                    Telemetry.Push(telemetry);

                    lock (this)
                    {
                        var headerCollection = new NameValueCollection();
                        foreach (var header in request.Headers)
                        {
                            headerCollection.Add(header.Name, header.Value);
                        }

                        TelemetryRequestHeaders = TelemetryRequestHeaders.Add(headerCollection);
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
            }
        }

        private void HandlePotentialDebuggerData(MockHttpParser.MockHttpRequest request)
        {
            if (request.ContentLength >= 1)
            {
                try
                {
                    var body = ReadStreamBody(request);
                    using var stream = new MemoryStream(body);
                    using var streamReader = new StreamReader(stream);
                    var batch = streamReader.ReadToEnd();
                    ReceiveDebuggerBatch(batch);
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
            }
        }

        private void HandlePotentialStatsData(MockHttpParser.MockHttpRequest request)
        {
            if (ShouldDeserializeTraces && request.ContentLength >= 1)
            {
                try
                {
                    var body = ReadStreamBody(request);

                    var statsPayload = MessagePackSerializer.Deserialize<MockClientStatsPayload>(body);
                    OnStatsDeserialized(statsPayload);

                    lock (this)
                    {
                        Stats = Stats.Add(statsPayload);
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
            }
        }

        private void HandlePotentialRemoteConfig(MockHttpParser.MockHttpRequest request)
        {
            if (request.ContentLength >= 1)
            {
                try
                {
                    var body = ReadStreamBody(request);
                    var rc = Encoding.UTF8.GetString(body);
                    RemoteConfigRequests.Enqueue(rc);
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
            }
        }

        private void HandlePotentialDataStreams(MockHttpParser.MockHttpRequest request)
        {
            if (ShouldDeserializeTraces && request.ContentLength >= 1)
            {
                try
                {
                    var body = ReadStreamBody(request);

                    var dataStreamsPayload = MessagePackSerializer.Deserialize<MockDataStreamsPayload>(body);

                    lock (this)
                    {
                        DataStreams = DataStreams.Add(dataStreamsPayload);
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.ToLowerInvariant();

                    if (message.Contains("beyond the end of the stream"))
                    {
                        // Accept call is likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        return;
                    }

                    throw;
                }
            }
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

        private string GetStatusString(int status) => status switch
        {
            200 => "200 OK",
            404 => "404 Not Found",
            500 => "500 Internal Server Error",
            _ => status.ToString(),
        };

        private byte[] GetResponseBytes(string body, int status)
        {
            if (string.IsNullOrEmpty(body))
            {
                // Our DatadogHttpClient can't cope if the response doesn't have a body.
                // Which isn't great.
                throw new ArgumentException("Response body must not be null or empty", nameof(body));
            }

            var sb = new StringBuilder();
            sb
               .Append("HTTP/1.1 ")
               .Append(GetStatusString(status))
               .Append(DatadogHttpValues.CrLf)
               .Append("Date: ")
               .Append(DateTime.UtcNow.ToString("ddd, dd MMM yyyy H:mm::ss K"))
               .Append(DatadogHttpValues.CrLf)
               .Append("Connection: Close")
               .Append(DatadogHttpValues.CrLf)
               .Append("Server: dd-mock-agent");

            if (Version != null)
            {
                sb
                   .Append(DatadogHttpValues.CrLf)
                   .Append("Datadog-Agent-Version: ")
                   .Append(Version);
            }

            var responseBody = Encoding.UTF8.GetBytes(body);
            var contentLength64 = responseBody.LongLength;
            sb
               .Append(DatadogHttpValues.CrLf)
               .Append("Content-Type: application/json")
               .Append(DatadogHttpValues.CrLf)
               .Append("Content-Length: ")
               .Append(contentLength64)
               .Append(DatadogHttpValues.CrLf)
               .Append(DatadogHttpValues.CrLf)
               .Append(Encoding.ASCII.GetString(responseBody));

            var responseBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return responseBytes;
        }

        private byte[] ReadStreamBody(MockHttpParser.MockHttpRequest request)
        {
            if (request.ContentLength is null)
            {
                return new byte[0];
            }

            var i = 0;
            var body = new byte[request.ContentLength.Value];

            while (request.Body.Stream.CanRead && i < request.ContentLength)
            {
                var nextByte = request.Body.Stream.ReadByte();

                if (nextByte == -1)
                {
                    break;
                }

                body[i] = (byte)nextByte;
                i++;
            }

            if (i < request.ContentLength)
            {
                throw new Exception($"Less bytes were sent than we counted. {i} read versus {request.ContentLength} expected.");
            }

            return body;
        }

        private void ReceiveDebuggerBatch(string batch)
        {
            var arr = JArray.Parse(batch);

            var probeStatuses = new Dictionary<string, string>();
            var snapshots = new List<string>();

            foreach (var token in arr)
            {
                var stringifiedToken = token.ToString();
                var id = token["debugger"]?["diagnostics"]?["probeId"]?.ToString();
                if (id != null)
                {
                    probeStatuses[id] = stringifiedToken;
                }
                else
                {
                    snapshots.Add(stringifiedToken);
                }
            }

            // We override the previous Probes Statuses as the debugger-agent is always emitting complete set of probes statuses, so we can
            // solely rely on that.
            ProbesStatuses = probeStatuses.Values.ToList();
            Snapshots.AddRange(snapshots);
        }

        public class AgentConfiguration
        {
            [JsonProperty("endpoints")]
            public string[] Endpoints { get; set; } = DiscoveryService.AllSupportedEndpoints.Select(s => s.StartsWith("/") ? s : "/" + s).ToArray();

            [JsonProperty("client_drop_p0s")]
            public bool ClientDropP0s { get; set; } = true;

            [JsonProperty("version")]
            public string AgentVersion { get; set; }
        }

        public class TcpUdpAgent : MockTracerAgent
        {
            private readonly HttpListener _listener;
            private readonly UdpClient _udpClient;
            private readonly Task _tracesListenerTask;
            private readonly Task _statsdTask;

            public TcpUdpAgent(int port, int retries, bool useStatsd, bool doNotBindPorts, int? requestedStatsDPort, bool useTelemetry)
                : base(useTelemetry, TestTransports.Tcp)
            {
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

                    _statsdTask = Task.Run(HandleStatsdRequests);

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
                        _tracesListenerTask = Task.Run(HandleHttpRequests);

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

            /// <summary>
            /// Gets the TCP port that this Agent is listening on.
            /// Can be different from the request port if listening on that port fails.
            /// </summary>
            public int Port { get; }

            /// <summary>
            /// Gets the UDP port for statsd
            /// </summary>
            public int StatsdPort { get; }

            public override void Dispose()
            {
                base.Dispose();
                _listener?.Close();
                _udpClient?.Close();
            }

            private void HandleHttpRequests()
            {
                while (_listener.IsListening)
                {
                    try
                    {
                        var ctx = _listener.GetContext();
                        try
                        {
                            OnRequestReceived(ctx);

                            if (Version != null)
                            {
                                ctx.Response.AddHeader("Datadog-Agent-Version", Version);
                            }

                            var mockTracerResponse = HandleHttpRequest(MockHttpParser.MockHttpRequest.Create(ctx.Request));

                            if (mockTracerResponse.SendResponse)
                            {
                                var buffer = Encoding.UTF8.GetBytes(mockTracerResponse.Response);

                                if (ctx.Response.StatusCode == 200)
                                {
                                    ctx.Response.StatusCode = mockTracerResponse.StatusCode;
                                }

                                ctx.Response.ContentType = "application/json";
                                ctx.Response.ContentLength64 = buffer.LongLength;
                                ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                                ctx.Response.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Output?.WriteLine("[HandleHttpRequests]Error processing web request" + ex);
                            ctx.Response.Close();
                        }
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
        }

        public class NamedPipeAgent : MockTracerAgent
        {
            private readonly PipeServer _statsPipeServer;
            private readonly PipeServer _tracesPipeServer;
            private readonly Task _statsdTask;
            private readonly Task _tracesListenerTask;

            public NamedPipeAgent(WindowsPipesConfig config)
                : base(config.UseTelemetry, TestTransports.WindowsNamedPipe)
            {
                ListenerInfo = $"Traces at {config.Traces}";

                if (config.Metrics != null && config.UseDogstatsD)
                {
                    if (File.Exists(config.Metrics))
                    {
                        File.Delete(config.Metrics);
                    }

                    StatsWindowsPipeName = config.Metrics;
                    ListenerInfo += $", Stats at {config.Metrics}";

                    _statsPipeServer = new PipeServer(
                        config.Metrics,
                        PipeDirection.In, // we don't send responses to stats requests
                        _cancellationTokenSource,
                        (stream, ct) => HandleNamedPipeStats(stream, ct),
                        ex => Exceptions.Add(ex),
                        x => Output?.WriteLine(x));

                    _statsdTask = Task.Run(_statsPipeServer.Start);
                }

                if (File.Exists(config.Traces))
                {
                    File.Delete(config.Traces);
                }

                TracesWindowsPipeName = config.Traces;

                _tracesPipeServer = new PipeServer(
                    config.Traces,
                    PipeDirection.InOut,
                    _cancellationTokenSource,
                    (stream, ct) => HandleNamedPipeTraces(stream, ct),
                    ex => Exceptions.Add(ex),
                    x => Output?.WriteLine(x));

                _tracesListenerTask = Task.Run(_tracesPipeServer.Start);
            }

            public string TracesWindowsPipeName { get; }

            public string StatsWindowsPipeName { get; }

            public override void Dispose()
            {
                base.Dispose();
                _statsPipeServer?.Dispose();
                _tracesPipeServer?.Dispose();
            }

            private async Task HandleNamedPipeStats(NamedPipeServerStream namedPipeServerStream, CancellationToken cancellationToken)
            {
                // A somewhat large, arbitrary amount, but Runtime metrics sends a lot
                // Will throw if we exceed that but YOLO
                var bytesReceived = new byte[0x10_000];
                var byteCount = 0;
                int bytesRead;
                do
                {
                    bytesRead = await namedPipeServerStream.ReadAsync(bytesReceived, byteCount, count: 500, cancellationToken);
                    byteCount += bytesRead;
                }
                while (bytesRead > 0);

                var stats = Encoding.UTF8.GetString(bytesReceived, 0, byteCount);
                OnMetricsReceived(stats);
                StatsdRequests.Enqueue(stats);
            }

            private async Task HandleNamedPipeTraces(NamedPipeServerStream namedPipeServerStream, CancellationToken cancellationToken)
            {
                var request = await MockHttpParser.ReadRequest(namedPipeServerStream);
                var mockTracerResponse = HandleHttpRequest(request);

                if (mockTracerResponse.SendResponse)
                {
                    var responseBytes = GetResponseBytes(body: mockTracerResponse.Response, status: mockTracerResponse.StatusCode);
                    await namedPipeServerStream.WriteAsync(responseBytes, offset: 0, count: responseBytes.Length);
                }
            }

            internal class PipeServer : IDisposable
            {
                private const int ConcurrentInstanceCount = 5;
                private readonly CancellationTokenSource _cancellationTokenSource;
                private readonly string _pipeName;
                private readonly PipeDirection _pipeDirection;
                private readonly Func<NamedPipeServerStream, CancellationToken, Task> _handleReadFunc;
                private readonly Action<Exception> _handleExceptionFunc;
                private readonly ConcurrentBag<Task> _tasks = new();
                private readonly Action<string> _log;
                private int _instanceCount = 0;

                public PipeServer(
                    string pipeName,
                    PipeDirection pipeDirection,
                    CancellationTokenSource tokenSource,
                    Func<NamedPipeServerStream, CancellationToken, Task> handleReadFunc,
                    Action<Exception> handleExceptionFunc,
                    Action<string> log)
                {
                    _cancellationTokenSource = tokenSource;
                    _pipeDirection = pipeDirection;
                    _pipeName = pipeName;
                    _handleReadFunc = handleReadFunc;
                    _handleExceptionFunc = handleExceptionFunc;
                    _log = log;
                }

                public Task Start()
                {
                    for (var i = 0; i < ConcurrentInstanceCount; i++)
                    {
                        _log("Starting PipeServer " + _pipeName);
                        using var mutex = new ManualResetEventSlim();
                        var startPipe = StartNamedPipeServer(mutex);
                        _tasks.Add(startPipe);
                        mutex.Wait(5_000);
                    }

                    return Task.CompletedTask;
                }

                public void Dispose()
                {
                    _log("Waiting for PipeServer Disposal " + _pipeName);
                    Task.WaitAll(_tasks.ToArray(), TimeSpan.FromSeconds(10));
                }

                private async Task StartNamedPipeServer(ManualResetEventSlim mutex)
                {
                    var instance = $" ({_pipeName}:{Interlocked.Increment(ref _instanceCount)})";
                    try
                    {
                        _log("Starting NamedPipeServerStream instance " + instance);
                        using var statsServerStream = new NamedPipeServerStream(
                            _pipeName,
                            _pipeDirection, // we don't send responses to stats requests
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                        _log("Starting wait for connection " + instance);
                        var connectTask = statsServerStream.WaitForConnectionAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                        mutex.Set();

                        _log("Awaiting connection " + instance);
                        await connectTask;

                        _log("Connection accepted, starting new server" + instance);

                        // start a new Named pipe server to handle additional connections
                        // Yes, this is madness, but apparently the way it's supposed to be done
                        using var m = new ManualResetEventSlim();
                        _tasks.Add(Task.Run(() => StartNamedPipeServer(m)));
                        // Wait for the next instance to start listening before we handle this one
                        m.Wait(5_000);

                        _log("Executing read for " + instance);

                        await _handleReadFunc(statsServerStream, _cancellationTokenSource.Token);
                    }
                    catch (Exception) when (_cancellationTokenSource.IsCancellationRequested)
                    {
                        _log("Execution canceled " + instance);
                    }
                    catch (IOException ex) when (ex.Message.Contains("The pipe is being closed"))
                    {
                        // Likely interrupted by a dispose
                        // Swallow the exception and let the test finish
                        _log("Pipe closed " + instance);
                    }
                    catch (Exception ex)
                    {
                        _handleExceptionFunc(ex);

                        // unexpected exception, so start another listener
                        _log("Unexpected exception " + instance + " " + ex.ToString());
                        using var m = new ManualResetEventSlim();
                        _tasks.Add(Task.Run(() => StartNamedPipeServer(m)));
                        m.Wait(5_000);
                    }
                }
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        public class UdsAgent : MockTracerAgent
        {
            private readonly UnixDomainSocketEndPoint _tracesEndpoint;
            private readonly Socket _udsTracesSocket;
            private readonly UnixDomainSocketEndPoint _statsEndpoint;
            private readonly Socket _udsStatsSocket;
            private readonly Task _tracesListenerTask;
            private readonly Task _statsdTask;

            public UdsAgent(UnixDomainSocketConfig config)
                : base(config.UseTelemetry, TestTransports.Uds)
            {
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
                    _statsdTask = Task.Run(HandleUdsStats);
                }

                _tracesEndpoint = new UnixDomainSocketEndPoint(config.Traces);

                if (File.Exists(config.Traces))
                {
                    File.Delete(config.Traces);
                }

                TracesUdsPath = config.Traces;
                _udsTracesSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                _udsTracesSocket.Bind(_tracesEndpoint);
                _udsTracesSocket.Listen(1000);
                _tracesListenerTask = Task.Run(HandleUdsTraces);
            }

            public string TracesUdsPath { get; }

            public string StatsUdsPath { get; }

            public override void Dispose()
            {
                base.Dispose();
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

            private async Task HandleUdsTraces()
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        using var handler = await _udsTracesSocket.AcceptAsync();
                        using var stream = new NetworkStream(handler);

                        var request = await MockHttpParser.ReadRequest(stream);
                        var mockTracerResponse = HandleHttpRequest(request);

                        if (mockTracerResponse.SendResponse)
                        {
                            await stream.WriteAsync(GetResponseBytes(body: mockTracerResponse.Response, status: mockTracerResponse.StatusCode));
                            handler.Shutdown(SocketShutdown.Both);
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
                    catch (Exception ex)
                    {
                        Output?.WriteLine("[HandleUdsTraces]Error processing uds request: " + ex);
                    }
                }
            }
        }
#endif
    }
}
