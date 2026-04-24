// <copyright file="StatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.TraceSamplers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Processors;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent
{
    internal sealed class StatsAggregator : IStatsAggregator
    {
        private const int BufferCount = 2;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<StatsAggregator>();
        private static readonly List<byte[]> EmptyPeerTags = [];
        private static readonly byte[] PeerTagSeparator = [0];
        private static readonly byte[] BaseServiceUtf8Prefix = EncodingHelpers.Utf8NoBom.GetBytes(Tags.BaseService + ":");

        private readonly StatsBuffer[] _buffers;

        private readonly IApi _api;
        private readonly bool _isOtlp;
        private readonly NormalizerTraceProcessor _normalizerProcessor;
        private readonly ObfuscatorTraceProcessor _obfuscatorProcessor;

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
        private List<PeerTagKey> _peerTagKeys = [];

        internal StatsAggregator(IApi api, TracerSettings settings, IDiscoveryService discoveryService, bool isOtlp)
        {
            _api = api;
            _isOtlp = isOtlp;
            _processExit = new TaskCompletionSource<bool>();
            _bucketDuration = TimeSpan.FromSeconds(settings.StatsComputationInterval);
            _buffers = new StatsBuffer[BufferCount];
            _normalizerProcessor = new NormalizerTraceProcessor();
            _obfuscatorProcessor = new ObfuscatorTraceProcessor();

            _prioritySampler = new PrioritySampler();
            _errorSampler = new ErrorSampler();
            _rareSampler = new RareSampler(settings, this);
            _analyticsEventSampler = new AnalyticsEventsSampler();

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

        public TraceKeepState ProcessTrace(ref SpanCollection spans)
        {
            // Follow the same processing steps as the Go tracer
            spans = NormalizeTrace(in spans);
            if (ShouldFilterTrace(in spans))
            {
                return TraceKeepState.Rejected;
            }

            spans = ObfuscateTrace(in spans);
            if (!ShouldKeepTrace(in spans))
            {
                return TraceKeepState.AggregateOnly;
            }

            return TraceKeepState.AggregateAndExport; // keep
        }

        [TestingAndPrivateOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SpanCollection NormalizeTrace(in SpanCollection trace)
        {
            try
            {
                return _normalizerProcessor.Process(in trace);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error executing normalizer trace processor");
                return trace;
            }
        }

        [TestingAndPrivateOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ShouldFilterTrace(in SpanCollection trace)
        {
            var filter = Volatile.Read(ref _traceFilter);
            if (filter is null)
            {
                return false;
            }

            // Find the local root span, searching from the last span, as that's where it normally is
            var traceContext = TraceContext.GetTraceContext(in trace);
            var localRoot = traceContext?.RootSpan;

            if (localRoot is not null && trace.ContainsSpanId(localRoot.SpanId, trace.Count - 1))
            {
                // localRoot is in the trace chunk, so we can apply the filter directly
                return !filter.ShouldKeepTrace(localRoot);
            }

            // local root isn't in the trace chunk (can happen with partial flushing)
            // or we don't have a local root (I don't know when that happens!)
            return false;
        }

        [TestingAndPrivateOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ShouldKeepTrace(in SpanCollection trace)
        {
            // For OTLP, align with the OpenTelemetry SDK behavior to export a trace based
            // solely on its sampling decision.
            if (_isOtlp)
            {
                return _prioritySampler.Sample(in trace);
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

        [TestingAndPrivateOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SpanCollection ObfuscateTrace(in SpanCollection trace)
        {
            // Only obfuscate resources when the tracer has negotiated obfuscation responsibility.
            // The tracer currently only supports obfuscation version 1 (SQL, Cassandra, Redis).
            if (Volatile.Read(ref _tracerObfuscationVersion) == 1)
            {
                try
                {
                    return _obfuscatorProcessor.Process(in trace);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing obfuscator trace processor");
                }
            }

            return trace;
        }

        public StatsAggregationKey BuildKey(Span span)
            => BuildKey(span, Volatile.Read(ref _peerTagKeys), out _);

        /// <summary>
        /// Computes a <see cref="StatsAggregationKey"/> for the given span, including the peer tags hash.
        /// The <paramref name="peerTagResults"/> carries context to <see cref="GetEncodedPeerTags"/>
        /// so the cold path can skip re-deriving spanKind/baseService and pre-allocate the result list.
        /// </summary>
        [TestingAndPrivateOnly]
        internal StatsAggregationKey BuildKey(Span span, List<PeerTagKey> peerTagKeys, out PeerTagResults peerTagResults)
        {
            var rawHttpStatusCode = span.GetTag(Tags.HttpStatusCode);

            if (rawHttpStatusCode == null || !int.TryParse(rawHttpStatusCode, out var httpStatusCode))
            {
                httpStatusCode = 0;
            }

            // Check gRPC status code tags in priority order per CSS v1.2.0 spec.
            // Stored as string to match the Go agent's wire format (GRPCStatusCode is a string field).
            // This preserves the distinction between "0" (gRPC OK) and "" (no gRPC status).
            var grpcStatusCode = span.GetTag("rpc.grpc.status_code")
                              ?? span.GetTag("grpc.code")
                              ?? span.GetTag("rpc.grpc.status.code")
                              ?? span.GetTag(Tags.GrpcStatusCode)
                              ?? string.Empty;

            // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/aggregation.go
            var spanKind = (span.Tags is InstrumentationTags t ? t.SpanKind : span.GetTag(Tags.SpanKind)) ?? string.Empty;
            var isTraceRoot = span.Context.ParentId is null or 0;
            var httpMethod = span.GetTag(Tags.HttpMethod) ?? string.Empty;
            var httpEndpoint = span.GetTag(Tags.HttpRoute) ?? string.Empty;

            // Normalize service source to match trace serialization behavior:
            // clear the source when service name equals the default, unless it's
            // a configuration-driven override (opt.*).
            var serviceSource = span.Context.ServiceNameSource;
            var serviceNameEqualsDefault = string.Equals(span.ServiceName, span.Context.TraceContext?.Tracer?.DefaultServiceName, StringComparison.OrdinalIgnoreCase);
            if (serviceNameEqualsDefault && serviceSource?.StartsWith("opt.", StringComparison.Ordinal) != true)
            {
                serviceSource = string.Empty;
            }

            // Based on https://github.com/DataDog/datadog-agent/blob/ce22e11ee71e55be717b9d9a3f8f3d7721a9c6d7/pkg/trace/stats/span_concentrator.go#L53-L99
            // Peer tags are extracted for client/server/producer/consumer spans.
            // If the span kind is missing or internal, and we have a "base service" tag `_dd.base_service`
            // then we only aggregate based on the `_dd.base_service`.
            // This computes only the hash; see GetEncodedPeerTags() for the cold-path encoding.
            ulong peerTagsHash;
            if ((string.IsNullOrEmpty(spanKind) || spanKind is SpanKinds.Internal) && span.GetTag(Tags.BaseService) is { Length: > 0 } baseService)
            {
                peerTagsHash = HashTag(BaseServiceUtf8Prefix, baseService, FnvHash64.Version.V1A);
                peerTagResults = new PeerTagResults { BaseService = baseService };
            }
            else if (spanKind is SpanKinds.Client or SpanKinds.Server or SpanKinds.Producer or SpanKinds.Consumer)
            {
                // Hash should be generated as TAGNAME:TAGVALUE, in sorted order (peerTagKeys is pre-sorted).
                ulong? previousHash = null;
                var peerTagCount = 0;
                foreach (var peerTag in peerTagKeys)
                {
                    var tagValue = span.GetTag(peerTag.Name);
                    if (string.IsNullOrEmpty(tagValue))
                    {
                        continue;
                    }

                    tagValue = IpAddressObfuscationUtil.QuantizePeerIpAddresses(tagValue);

                    if (previousHash.HasValue)
                    {
                        // add the separator between tags
                        previousHash = FnvHash64.GenerateHash(PeerTagSeparator, FnvHash64.Version.V1A, previousHash.Value);
                    }

                    previousHash = HashTag(peerTag.Utf8Prefix, tagValue, FnvHash64.Version.V1A, previousHash);
                    peerTagCount++;
                }

                peerTagResults = new PeerTagResults { PeerTagCount = peerTagCount };
                peerTagsHash = previousHash ?? 0;
            }
            else
            {
                peerTagsHash = 0;
                peerTagResults = default;
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
                span.Context.Origin?.StartsWith("synthetics") == true,
                spanKind,
                _isOtlp ? span.Error : false,
                _isOtlp ? span.IsTopLevel : false,
                isTraceRoot,
                httpMethod,
                httpEndpoint,
                grpcStatusCode,
                serviceSource,
                peerTagsHash);
        }

        /// <summary>
        /// Hashes "keyPrefix + tagValue" using FNV-64.
        /// The <paramref name="keyPrefix"/> is a pre-encoded UTF-8 byte array (e.g. "tagKey:") and is
        /// hashed directly. Only the <paramref name="tagValue"/> needs UTF-8 encoding at call time.
        /// </summary>
        [SkipLocalsInit]
        private static ulong HashTag(byte[] keyPrefix, string tagValue, FnvHash64.Version version, ulong? initialHash = null)
        {
            // Hash the pre-encoded key prefix (e.g. "peer.service:") directly — no encoding needed
            var hash = initialHash is { } h
                ? FnvHash64.GenerateHash(keyPrefix, version, h)
                : FnvHash64.GenerateHash(keyPrefix, version);

            // Now encode and hash just the tag value
            var maxByteCount = EncodingHelpers.Utf8NoBom.GetMaxByteCount(tagValue.Length);
            const int maxStackLimit = 256;

            if (maxByteCount <= maxStackLimit)
            {
                Span<byte> buffer = stackalloc byte[maxStackLimit];
                int written;
#if NETCOREAPP
                written = EncodingHelpers.Utf8NoBom.GetBytes(tagValue, buffer);
#else
                unsafe
                {
                    var tagValueSpan = tagValue.AsSpan();
                    fixed (char* tagValuePointer = tagValueSpan)
                    {
                        fixed (byte* bufferPointer = buffer)
                        {
                            written = EncodingHelpers.Utf8NoBom.GetBytes(tagValuePointer, tagValueSpan.Length, bufferPointer, buffer.Length);
                        }
                    }
                }
#endif
                return FnvHash64.GenerateHash(buffer.Slice(0, written), version, hash);
            }

            var rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                var written = EncodingHelpers.Utf8NoBom.GetBytes(tagValue, charIndex: 0, charCount: tagValue.Length, rented, byteIndex: 0);
                return FnvHash64.GenerateHash(rented.AsSpan().Slice(0, written), version, hash);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Encodes the peer tags for a span into a <see cref="List{T}"/> of UTF-8 byte arrays.
        /// Called only on the cold path (new bucket creation).
        /// Uses <paramref name="results"/> from <see cref="BuildKey(Span, List{PeerTagKey}, out PeerTagResults)"/>
        /// to skip re-deriving spanKind/baseService and to pre-allocate the result list.
        /// </summary>
        internal static List<byte[]> GetEncodedPeerTags(Span span, List<PeerTagKey> peerTagKeys, in PeerTagResults results)
        {
            if (results.BaseService is not null)
            {
                return [EncodingHelpers.Utf8NoBom.GetBytes($"{Tags.BaseService}:{results.BaseService}")];
            }

            if (results.PeerTagCount == 0)
            {
                return EmptyPeerTags;
            }

            var result = new List<byte[]>(results.PeerTagCount);
            foreach (var peerTag in peerTagKeys)
            {
                var tagValue = span.GetTag(peerTag.Name);
                if (string.IsNullOrEmpty(tagValue))
                {
                    continue;
                }

                tagValue = IpAddressObfuscationUtil.QuantizePeerIpAddresses(tagValue);
                result.Add(EncodingHelpers.Utf8NoBom.GetBytes($"{peerTag.Name}:{tagValue}"));
            }

            return result;
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

                if (buffer.HasHits() && CanComputeStats == true)
                {
                    await _api.SendStatsAsync(buffer, _bucketDuration.ToNanoseconds(), Volatile.Read(ref _tracerObfuscationVersion)).ConfigureAwait(false);
                }

                // Always reset the buffer so Start is re-aligned and stale keys are pruned,
                // even when no hits were recorded this interval.
                buffer.Reset();
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
            var isSpanKindEligible = spanKind is SpanKinds.Client or SpanKinds.Server or SpanKinds.Consumer or SpanKinds.Producer;

            if (!_isOtlp // If we are using OTLP, we include both top-level and non-top-level spans
                && (!(span.IsTopLevel || isSpanKindEligible || span.GetMetric(Tags.Measured) == 1.0)
                 || span.GetMetric(Tags.PartialSnapshot) > 0))
            {
                return;
            }

            var buffer = CurrentBuffer;
            var peerTagKeys = Volatile.Read(ref _peerTagKeys);
            var key = BuildKey(span, peerTagKeys, out var peerTagResults);

            if (!buffer.Buckets.TryGetValue(key, out var bucket))
            {
                // Cold path: encode the peer tags for storage in the new bucket
                bucket = new StatsBucket(key, GetEncodedPeerTags(span, peerTagKeys, in peerTagResults));
                buffer.Buckets.Add(key, bucket);
            }

            var weight = GetWeight(span);
            bucket.Hits += weight;

            if (span.IsTopLevel)
            {
                bucket.TopLevelHits += weight;
            }

            var duration = span.Duration.ToNanoseconds();

            // Duration is weighted by sampling rate, matching the Go agent behavior:
            // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/stats/statsraw.go
            bucket.Duration += duration * weight;

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

        private void HandleConfigUpdate(AgentConfiguration config)
        {
            CanComputeStats = !string.IsNullOrWhiteSpace(config.StatsEndpoint)
                           && config.ClientDropP0s;

            if (CanComputeStats.Value)
            {
                Log.Debug("Stats computation enabled.");
            }
            else
            {
                Log.Warning("Stats computation disabled because the detected agent does not support this feature.");
                // early return, because there's no point doing all the extra work if stats isn't enabled anyway
                return;
            }

            if (config.PeerTags is { Count: > 0 })
            {
                // Sort, deduplicate, and pre-compute the UTF-8 key prefixes so that
                // BuildKey can hash without per-call string encoding.
                var precomputed = new List<PeerTagKey>(config.PeerTags.Count);
                foreach (var tag in config.PeerTags.Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x))
                {
                    precomputed.Add(new PeerTagKey(tag));
                }

                Interlocked.Exchange(ref _peerTagKeys, precomputed);
            }
            else
            {
                Interlocked.Exchange(ref _peerTagKeys, []);
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
        }

        internal readonly struct PeerTagKey(string name)
        {
            public readonly string Name = name;
            public readonly byte[] Utf8Prefix = EncodingHelpers.Utf8NoBom.GetBytes(name + ":");
        }

        internal readonly struct PeerTagResults
        {
            public int PeerTagCount { get; init; }

            public string BaseService { get; init; }
        }
    }
}
