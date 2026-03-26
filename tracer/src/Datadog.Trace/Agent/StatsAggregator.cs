// <copyright file="StatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.TraceSamplers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent
{
    internal sealed class StatsAggregator : IStatsAggregator
    {
        private const int BufferCount = 2;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<StatsAggregator>();
        private static readonly List<byte[]> EmptyPeerTags = [];
        private static readonly byte[] PeerTagSeparator = [0];

        private readonly StatsBuffer[] _buffers;

        private readonly IApi _api;
        private readonly bool _isOtlp;
        private readonly ITraceProcessor[] _traceProcessors;
        private readonly ITraceProcessor _obfuscatorProcessor;

        private readonly TaskCompletionSource<bool> _processExit;

        private readonly TimeSpan _bucketDuration;

        private readonly Task _flushTask;

        private readonly IDiscoveryService _discoveryService;

        private readonly PrioritySampler _prioritySampler;
        private readonly ErrorSampler _errorSampler;
        private readonly RareSampler _rareSampler;
        private readonly AnalyticsEventsSampler _analyticsEventSampler;
        private readonly IDisposable _settingSubscription;

        private int _currentBuffer;

        private int _tracerObfuscationVersion;
        private TraceFilter _traceFilter;
        private List<string> _peerTagKeys = [];
        // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/span_concentrator.go#L210-L213
        private List<string> _spanKindsStatsComputed =
        [
            SpanKinds.Client,
            SpanKinds.Server,
            SpanKinds.Producer,
            SpanKinds.Consumer,
        ];

        private string _defaultServiceName;

        internal StatsAggregator(IApi api, TracerSettings settings, IDiscoveryService discoveryService, bool isOtlp)
        {
            _api = api;
            _isOtlp = isOtlp;
            _processExit = new TaskCompletionSource<bool>();
            _bucketDuration = TimeSpan.FromSeconds(settings.StatsComputationInterval);
            _buffers = new StatsBuffer[BufferCount];
            _traceProcessors = new ITraceProcessor[]
            {
                new Processors.NormalizerTraceProcessor(),
            };

            _obfuscatorProcessor = new Processors.ObfuscatorTraceProcessor();

            _prioritySampler = new PrioritySampler();
            _errorSampler = new ErrorSampler();
            _rareSampler = new RareSampler(settings, this);
            _analyticsEventSampler = new AnalyticsEventsSampler();
            _defaultServiceName = settings.Manager.InitialMutableSettings.DefaultServiceName;

            // Create with the initial mutable settings, but be aware that this could change later
            var header = new ClientStatsPayload(settings.Manager.InitialMutableSettings)
            {
                HostName = HostMetadata.Instance.Hostname,
            };

            _settingSubscription = settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } mutable)
                {
                    header.UpdateDetails(mutable);
                    Interlocked.Exchange(ref _defaultServiceName, mutable.DefaultServiceName);
                }
            });

            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i] = new(header);
            }

            _flushTask = Task.Run(Flush);
            _flushTask.ContinueWith(t => Log.Error(t.Exception, "Error in StatsAggregator"), TaskContinuationOptions.OnlyOnFaulted);

            if (_isOtlp)
            {
                CanComputeStats = true;
            }
            else
            {
                _discoveryService = discoveryService;
                discoveryService.SubscribeToChanges(HandleConfigUpdate);
            }
        }

        /// <summary>
        /// Gets the current buffer.
        /// StatsBuffer is not thread-safe, this property is not intended to be used outside of the class,
        /// except for tests.
        /// </summary>
        internal StatsBuffer CurrentBuffer => _buffers[_currentBuffer];

        public bool? CanComputeStats { get; private set; }

        public static IStatsAggregator Create(IApi api, TracerSettings settings, IDiscoveryService discoveryService, bool isOtlp)
        {
            return isOtlp || settings.StatsComputationEnabled ? new StatsAggregator(api, settings, discoveryService, isOtlp) : new NullStatsAggregator();
        }

        public Task DisposeAsync()
        {
            _discoveryService?.RemoveSubscription(HandleConfigUpdate);
            _processExit.TrySetResult(true);
            _settingSubscription.Dispose();
            return _flushTask;
        }

        [TestingOnly]
        public void Add(params Span[] spans)
        {
            AddRange(new(spans, spans.Length));
        }

        public void AddRange(in SpanCollection spans)
        {
            // Trace-level filters from the agent must be applied before stats computation.
            // Rejected traces should not contribute to stats.
            if (IsTraceFiltered(in spans))
            {
                return;
            }

            // Contention around this lock is expected to be very small:
            // AddRange is called from the serialization thread, and concurrent serialization
            // of traces is a rare corner-case (happening only during shutdown).
            // The Flush thread only acquires the lock long enough to swap the metrics buffer.
            lock (_buffers)
            {
                foreach (var span in spans)
                {
                    AddToBuffer(span);
                }
            }
        }

        public bool ShouldKeepTrace(in SpanCollection trace)
        {
            // For OTLP, align with the OpenTelemetry SDK behavior to export a trace based
            // solely on its sampling decision.
            if (_isOtlp)
            {
                return _prioritySampler.Sample(in trace);
            }

            // Trace-level filters from the agent must reject traces before sampling.
            if (IsTraceFiltered(in trace))
            {
                return false;
            }

            // Note: The RareSampler must be run before all other samplers so that
            // the first rare span in the trace chunk (if any) is marked with "_dd.rare".
            // The sampling decision is only used if no other samplers choose to keep the trace chunk.
            bool rareSpanFound = _rareSampler.Sample(in trace);

            return rareSpanFound
                || _prioritySampler.Sample(in trace)
                || _errorSampler.Sample(in trace)
                || _analyticsEventSampler.Sample(in trace);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanCollection ProcessTrace(in SpanCollection trace)
        {
            var spans = trace;
            foreach (var processor in _traceProcessors)
            {
                try
                {
                    spans = processor.Process(in spans);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing trace processor {TraceProcessorType}", processor?.GetType());
                }
            }

            // Only obfuscate resources when the tracer has negotiated obfuscation responsibility.
            // The tracer currently only supports obfuscation version 1 (SQL, Cassandra, Redis).
            if (Volatile.Read(ref _tracerObfuscationVersion) == 1)
            {
                try
                {
                    spans = _obfuscatorProcessor.Process(in spans);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing obfuscator trace processor");
                }
            }

            return spans;
        }

        public StatsAggregationKey BuildKey(Span span, out List<byte[]> utf8PeerTags)
            => BuildKey(span, _peerTagKeys, out utf8PeerTags);

        internal StatsAggregationKey BuildKey(Span span, List<string> peerTagKeys, out List<byte[]> utf8PeerTags)
        {
            var rawHttpStatusCode = span.GetTag(Tags.HttpStatusCode);

            if (rawHttpStatusCode == null || !int.TryParse(rawHttpStatusCode, out var httpStatusCode))
            {
                httpStatusCode = 0;
            }

            // Check gRPC status code tags in priority order per CSS v1.3.0 spec
            var rawGrpcStatusCode = span.GetTag("rpc.grpc.status_code")
                                 ?? span.GetTag("grpc.code")
                                 ?? span.GetTag("rpc.grpc.status.code")
                                 ?? span.GetTag(Tags.GrpcStatusCode);
            if (rawGrpcStatusCode is null || !int.TryParse(rawGrpcStatusCode, out var grpcStatusCode))
            {
                grpcStatusCode = 0;
            }

            // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/aggregation.go
            var spanKind = (span.Tags is InstrumentationTags t ? t.SpanKind : span.GetTag(Tags.SpanKind)) ?? string.Empty;
            var isTraceRoot = span.Context.ParentId is null or 0;
            var httpMethod = span.GetTag(Tags.HttpMethod) ?? string.Empty;
            var httpEndpoint = span.GetTag(Tags.HttpRoute) ?? string.Empty;
            var serviceName = span.ServiceName;

            // Normalize service source to match trace serialization behavior:
            // clear the source when service name equals the default, unless it's
            // a configuration-driven override (opt.*).
            var serviceNameSource = span.Context.ServiceNameSource;
            var serviceNameEqualsDefault = string.Equals(span.ServiceName, span.Context.TraceContext?.Tracer?.DefaultServiceName, StringComparison.OrdinalIgnoreCase);
            if (serviceNameEqualsDefault && serviceNameSource?.StartsWith("opt.", StringComparison.Ordinal) != true)
            {
                serviceNameSource = null;
            }

            var serviceNameIsDefault = span.ServiceName == Volatile.Read(ref _defaultServiceName);

            // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/span_concentrator.go#L53-L99
            // Peer tags are extracted for client/producer/consumer spans
            // If the span kind is missing or internal, and we have a "base service" tag `_dd.base_service`
            // then we only aggregate based on the `_dd.base_service`
            // TODO: work out how to optimize the peer tags allocations, to avoid all the extra utf8 allocations
            // - on .NET Core these are only necessary for the first instance of the key, but this makes for a tricky
            // chicken and egg - we need to convert everything to utf-8, so that we can get the hash, so that we
            // know whether we need the tags as byte[] or not..
            ulong peerTagsHash;
            if (!serviceNameIsDefault && (string.IsNullOrEmpty(spanKind) || spanKind is SpanKinds.Internal))
            {
                utf8PeerTags = [EncodingHelpers.Utf8NoBom.GetBytes($"{Tags.BaseService}:{serviceName}")];
                peerTagsHash = FnvHash64.GenerateHash(utf8PeerTags[0], FnvHash64.Version.V1A);
            }
            else if (spanKind is SpanKinds.Client or SpanKinds.Server or SpanKinds.Producer or SpanKinds.Consumer)
            {
                // Hash should be generated as TAGNAME:TAGVALUE, and should be in sorted order (we sort ahead of time)
                // peerTagKeys should already be in sorted order
                // We serialize to the utf-8 bytes because we need to serialize them during sending anyway
                // TODO: Verify we get the same results as the go code
                utf8PeerTags = EmptyPeerTags;
                peerTagsHash = 0;
                foreach (var tagKey in peerTagKeys)
                {
                    var tagValue = span.GetTag(tagKey);
                    if (string.IsNullOrEmpty(tagValue))
                    {
                        continue;
                    }

                    tagValue = IpAddressObfuscationUtil.QuantizePeerIpAddresses(tagValue);

                    if (ReferenceEquals(utf8PeerTags, EmptyPeerTags))
                    {
                        // We're not setting the capacity here, because there's
                        // a _lot_ of potential peer tags, and _most_ of them won't apply
                        utf8PeerTags = new();
                    }
                    else
                    {
                        // add the separator
                        peerTagsHash = FnvHash64.GenerateHash(PeerTagSeparator, FnvHash64.Version.V1A, peerTagsHash);
                    }

                    var bytes = EncodingHelpers.Utf8NoBom.GetBytes($"{tagKey}:{tagValue}");
                    peerTagsHash = FnvHash64.GenerateHash(bytes, FnvHash64.Version.V1A, peerTagsHash);
                    utf8PeerTags.Add(bytes);
                }
            }
            else
            {
                peerTagsHash = 0;
                utf8PeerTags = EmptyPeerTags;
            }

            // When submitting trace metrics over OTLP, we must create inidividual timeseries
            // timeseries for each unique set of attributes, including the Error and IsTopLevel attributes.
            // As a result, we must create distinct Aggregation keys (and consequently, unique stats) by these attributes.
            // Outside of OTLP, we make no distinction between these attributes for histograms, so we can set a constant 'false' value for each.
            return new StatsAggregationKey(
                span.ResourceName,
                span.ServiceName,
                span.OperationName,
                span.Type,
                httpStatusCode,
                span.Context.Origin == "synthetics",
                _isOtlp ? span.Error : false,
                _isOtlp ? span.IsTopLevel : false,
                spanKind,
                isTraceRoot,
                httpMethod,
                httpEndpoint,
                grpcStatusCode,
                serviceSource,
                peerTagsHash);
        }

        internal async Task Flush()
        {
            // Use a do/while loop to still flush once if _processExit is already completed (this makes testing easier)
            do
            {
                if (CanComputeStats == false)
                {
                    // TODO: When we implement the feature to continuously poll the Agent Configuration,
                    // we may want to stay in this loop instead of returning
                    return;
                }

                await Task.WhenAny(_processExit.Task, Task.Delay(_bucketDuration)).ConfigureAwait(false);

                var buffer = CurrentBuffer;

                lock (_buffers)
                {
                    _currentBuffer = (_currentBuffer + 1) % BufferCount;
                }

                TelemetryFactory.Metrics.RecordGaugeStatsBuckets(buffer.Buckets.Count);

                if (buffer.Buckets.Count > 0)
                {
                    // Push the metrics
                    if (CanComputeStats == true)
                    {
                        await _api.SendStatsAsync(buffer, _bucketDuration.ToNanoseconds(), Volatile.Read(ref _tracerObfuscationVersion)).ConfigureAwait(false);
                    }

                    buffer.Reset();
                }
            }
            while (!_processExit.Task.IsCompleted);
        }

        /// <summary>
        /// Converts a nanosec timestamp into a float nanosecond timestamp truncated to a fixed precision.
        /// Span timestamps must have maximum precision, but we can reduce precision of timestamps for
        /// aggregated stats points to achieve more efficient data representation.
        /// </summary>
        /// <param name="ns">Timestamp to convert</param>
        /// <returns>Timestamp with truncated precision</returns>
        private static double ConvertTimestamp(long ns)
        {
            // 10 bits precision (any value will be +/- 1/1024)
            const long roundMask = 1 << 10;

            int shift = 0;

            while (ns > roundMask)
            {
                ns >>= 1;
                shift++;
            }

            return ns << shift;
        }

        // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/weight.go
        private static double GetWeight(Span span)
        {
            var rate = span.Context.TraceContext.AppliedSamplingRate;
            return (rate is > 0) ? 1.0 / rate.Value : 1.0;
        }

        private void AddToBuffer(Span span)
        {
            // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/span_concentrator.go#L210-L217
            var spanKind = (span.Tags is InstrumentationTags t ? t.SpanKind : span.GetTag(Tags.SpanKind));
            var isSpanKindEligible = !string.IsNullOrEmpty(spanKind) && _spanKindsStatsComputed.Contains(spanKind);

            if (!_isOtlp // If we are using OTLP, we include both top-level and non-top-level spans
                && (!(span.IsTopLevel || isSpanKindEligible || span.GetMetric(Tags.Measured) == 1.0)
                 || span.GetMetric(Tags.PartialSnapshot) > 0))
            {
                return;
            }

            var key = BuildKey(span, out var peerTags);

            var buffer = CurrentBuffer;

            if (!buffer.Buckets.TryGetValue(key, out var bucket))
            {
                bucket = new StatsBucket(key, peerTags);
                buffer.Buckets.Add(key, bucket);
            }

            var weight = GetWeight(span);
            bucket.Hits += weight;

            if (span.IsTopLevel)
            {
                bucket.TopLevelHits += weight;
            }

            var duration = span.Duration.ToNanoseconds();

            bucket.Duration += duration;

            // If we are using OTLP, the errors are tracked as a separate aggregation entirely (different AggregationKey)
            // As a result, if using OTLP we always add to the OkSummary sketch.
            if (span.Error && !_isOtlp)
            {
                bucket.Errors += weight;
                bucket.ErrorSummary.Add(ConvertTimestamp(duration));
            }
            else
            {
                bucket.OkSummary.Add(ConvertTimestamp(duration));
            }
        }

        private bool IsTraceFiltered(in SpanCollection spans)
        {
            var filter = Volatile.Read(ref _traceFilter);
            if (filter is null)
            {
                return false;
            }

            // Find the root span (ParentId == null or 0) and apply the filter
            foreach (var span in spans)
            {
                if (span.Context.ParentId is null or 0)
                {
                    return !filter.ShouldKeepTrace(span);
                }
            }

            return false;
        }

        private void HandleConfigUpdate(AgentConfiguration config)
        {
            CanComputeStats = !string.IsNullOrWhiteSpace(config.StatsEndpoint) && config.ClientDropP0s == true;

            if (config.SpanKindsStatsComputed is not null)
            {
                Interlocked.Exchange(ref _spanKindsStatsComputed, config.SpanKindsStatsComputed);
            }

            if (config.PeerTags is not null)
            {
                Interlocked.Exchange(ref _peerTagKeys, config.PeerTags);
            }

            // Update trace filter from agent configuration
            if (config.TraceFilterConfig.HasFilters)
            {
                Volatile.Write(ref _traceFilter, new TraceFilter(config.TraceFilterConfig));
            }
            else
            {
                Volatile.Write(ref _traceFilter, null);
            }

            // Tracer obfuscation version is 1. If the agent's version is > 0 and <= ours, the tracer obfuscates.
            const int tracerObfuscationVersion = 1;
            var agentVersion = config.ObfuscationVersion;
            Volatile.Write(ref _tracerObfuscationVersion, agentVersion > 0 && agentVersion <= tracerObfuscationVersion ? tracerObfuscationVersion : 0);

            if (CanComputeStats.Value)
            {
                Log.Debug("Stats computation has been enabled.");
            }
            else
            {
                Log.Warning("Stats computation disabled because the detected agent does not support this feature.");
            }
        }
    }
}
