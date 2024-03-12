// <copyright file="MockTracerAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using HttpMultipartParser;
// use nuget MessagePack to deserialize
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// A mock agent that can be used to test the tracer.
    /// </summary>
    public abstract partial class MockTracerAgent : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        protected MockTracerAgent(bool telemetryEnabled, TestTransports transport)
        {
            TelemetryEnabled = telemetryEnabled;
            TransportType = transport;
        }

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        public event EventHandler<EventArgs<IList<IList<MockSpan>>>> RequestDeserialized;

        public event EventHandler<EventArgs<MockClientStatsPayload>> StatsDeserialized;

        public event EventHandler<EventArgs<string>> MetricsReceived;

        public event EventHandler<EventArgs<EvpProxyPayload>> EventPlatformProxyPayloadReceived;

        public string ListenerInfo { get; protected set; }

        public TestTransports TransportType { get; }

        public string Version { get; set; }

        public bool TelemetryEnabled { get; }

        public Dictionary<MockTracerResponseType, MockTracerResponse> CustomResponses { get; } = new();

        /// <summary>
        /// Gets the filters used to filter out spans we don't want to look at for a test.
        /// </summary>
        public List<Func<MockSpan, bool>> SpanFilters { get; } = new();

        public ConcurrentBag<Exception> Exceptions { get; private set; } = new ConcurrentBag<Exception>();

        public IImmutableList<MockSpan> Spans { get; private set; } = ImmutableList<MockSpan>.Empty;

        public IImmutableList<MockClientStatsPayload> Stats { get; private set; } = ImmutableList<MockClientStatsPayload>.Empty;

        public IImmutableList<MockDataStreamsPayload> DataStreams { get; private set; } = ImmutableList<MockDataStreamsPayload>.Empty;

        public IImmutableList<NameValueCollection> TraceRequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public IImmutableList<(Dictionary<string, string> Headers, MultipartFormDataParser Form)> TracerFlareRequests { get; private set; } = ImmutableList<(Dictionary<string, string> Headers, MultipartFormDataParser Form)>.Empty;

        public IImmutableList<string> Snapshots { get; private set; } = ImmutableList<string>.Empty;

        public IImmutableList<string> ProbesStatuses { get; private set; } = ImmutableList<string>.Empty;

        public abstract ConcurrentQueue<string> StatsdRequests { get; }

        public abstract ConcurrentQueue<Exception> StatsdExceptions { get; }

        /// <summary>
        /// Gets the wrapped <see cref="TelemetryData"/> requests received by the telemetry endpoint
        /// </summary>
        public ConcurrentStack<object> Telemetry { get; } = new();

        public ITestOutputHelper Output { get; set; }

        public AgentConfiguration Configuration { get; set; }

        public IImmutableList<NameValueCollection> TelemetryRequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public IImmutableList<NameValueCollection> DataStreamsRequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        public ConcurrentQueue<string> RemoteConfigRequests { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to skip deserialization of traces.
        /// </summary>
        public bool ShouldDeserializeTraces { get; set; } = true;

        public void ClearSnapshots()
        {
            Snapshots = Snapshots.Clear();
        }

        public void ClearProbeStatuses()
        {
            ProbesStatuses = ProbesStatuses.Clear();
        }

        public virtual void Dispose()
        {
            _cancellationTokenSource.Cancel();
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

        protected virtual void OnStatsDeserialized(MockClientStatsPayload stats)
        {
            StatsDeserialized?.Invoke(this, new EventArgs<MockClientStatsPayload>(stats));
        }

        protected virtual void OnMetricsReceived(string stats)
        {
            MetricsReceived?.Invoke(this, new EventArgs<string>(stats));
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
            ProbesStatuses = probeStatuses.Values.ToImmutableArray();
            Snapshots = Snapshots.AddRange(snapshots);
        }

        public class EvpProxyPayload
        {
            public EvpProxyPayload(string pathAndQuery, NameValueCollection headers, string bodyInJson)
            {
                PathAndQuery = pathAndQuery;
                Headers = headers;
                BodyInJson = bodyInJson;
                Response = null;
            }

            public string PathAndQuery { get; }

            public NameValueCollection Headers { get; }

            public string BodyInJson { get; }

            public MockTracerResponse Response { get; set; }
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
    }
}
