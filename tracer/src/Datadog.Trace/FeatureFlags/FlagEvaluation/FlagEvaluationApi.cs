// <copyright file="FlagEvaluationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// EVP flag evaluation writer — two-tier aggregation (full → degraded → drop-counted) with
/// a comparable canonical-context key and periodic flush to evp_proxy/v2/api/v2/flagevaluation.
///
/// Design: a comparable canonical-context key (sorted, type-tagged, length-delimited — NOT a hash
/// digest); context pruned to 256 fields / 256 chars; events captured by a FinallyAsync hook; gated
/// by the DD_FLAGGING_EVALUATION_COUNTS_ENABLED killswitch; NullValueHandling.Ignore per tier so the
/// degraded tier omits optional fields (schema-conformant).
/// </summary>
internal sealed class FlagEvaluationApi : IDisposable
{
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FlagEvaluationApi));

    /// <summary>EVP proxy path for flagevaluation events.</summary>
    public const string FlagEvaluationPath = "evp_proxy/v2/api/v2/flagevaluation";

    internal const int EvalScaleTargetFlags = 2_500;
    internal const int EvalScaleFullBucketsPerFlag = 50;
    internal const int EvalScaleUsersPerFlag = 1_000;
    internal const int EvalScalePerFlagHeadroomMultiplier = 10;
    internal const int EvalScaleDegradedBucketsPerFlag = 10;
    internal const int EvalScaleFullBucketTarget = EvalScaleTargetFlags * EvalScaleFullBucketsPerFlag;
    internal const int EvalScalePerFlagBucketTarget = EvalScalePerFlagHeadroomMultiplier * EvalScaleUsersPerFlag;
    internal const int EvalScaleDegradedBucketTarget = EvalScaleTargetFlags * EvalScaleDegradedBucketsPerFlag;
    internal const int GlobalCap = 131_072;
    internal const int PerFlagCap = EvalScalePerFlagBucketTarget;
    internal const int DegradedCap = 32_768;

    /// <summary>
    /// Bounds the async hand-off queue between the (hot-path) Enqueue call and the background
    /// aggregation worker. On overflow Enqueue drops the event and increments an observable counter
    /// rather than aggregating on — or blocking — the evaluation thread.
    /// </summary>
    private const int EventQueueCapacity = 16_384;

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        // Optional-field omission per tier: the degraded tier leaves context/targeting_key null so
        // they are omitted from the JSON (absent, not null) and stay schema-conformant.
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        }
    };

    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _drainInterval = TimeSpan.FromMilliseconds(100);

    private readonly FlagEvaluationAggregator _aggregator = new(
        globalCap: GlobalCap,
        perFlagCap: PerFlagCap,
        degradedCap: DegradedCap);

    // Async hand-off: Enqueue (hot path) does a cheap bounded ConcurrentQueue offer with an already
    // pruned event snapshot; the background send loop drains it into the aggregator.
    private readonly ConcurrentQueue<FlagEvalEvent> _queue = new();
    private int _queueCount;
    private long _droppedBackpressure;
    private Task? _sendLoopTask;

    private IApiRequestFactory _apiRequestFactory;
    private string _service;
    private string _env;
    private string _version;
    private int _started;

    internal FlagEvaluationApi(TracerSettings tracerSettings)
    {
        UpdateApi(tracerSettings.Manager.InitialExporterSettings);
        UpdateContext(tracerSettings.Manager.InitialMutableSettings);

        tracerSettings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                UpdateApi(exporter);
            }

            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateContext(mutable);
            }
        });

        [MemberNotNull(nameof(_apiRequestFactory))]
        void UpdateApi(ExporterSettings exporterSettings)
        {
            Log.Debug("FlagEvaluationApi::UpdateApi -> Applying settings");
            var apiRequestFactory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: "FeatureFlags evaluation",
                tcpTimeout: TimeSpan.FromSeconds(5),
                httpHeaderHelper: EventPlatformHeaderHelper.Instance);
            Interlocked.Exchange(ref _apiRequestFactory!, apiRequestFactory);
        }

        [MemberNotNull(nameof(_service), nameof(_env), nameof(_version))]
        void UpdateContext(MutableSettings settings)
        {
            Log.Debug("FlagEvaluationApi::UpdateContext -> Applying settings");
            Interlocked.Exchange(ref _service!, settings.DefaultServiceName);
            Interlocked.Exchange(ref _env!, settings.Environment ?? string.Empty);
            Interlocked.Exchange(ref _version!, settings.ServiceVersion ?? string.Empty);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvaluationApi"/> class with an injected
    /// request factory and context. Test-only seam: lets a unit test drive <see cref="Enqueue"/>
    /// and <see cref="FlushAsync()"/> against a captured transport without a live agent.
    /// </summary>
    internal FlagEvaluationApi(IApiRequestFactory apiRequestFactory, string service, string env, string version)
    {
        _apiRequestFactory = apiRequestFactory;
        _service = service;
        _env = env;
        _version = version;
    }

    /// <summary>Gets the hand-off queue capacity. Test-only accessor for the backpressure bound.</summary>
    internal static int QueueCapacity => EventQueueCapacity;

    /// <summary>Gets the number of events currently buffered in the hand-off queue. Test-only.</summary>
    internal int PendingQueueCount => Volatile.Read(ref _queueCount);

    /// <summary>Gets the running backpressure drop count (not reset). Test-only observability check.</summary>
    internal long DroppedBackpressureCount => Interlocked.Read(ref _droppedBackpressure);

    /// <summary>
    /// Non-blocking enqueue of one evaluation event. This is the ONLY method called from the
    /// hot-path FlagEvalEVPHook.FinallyAsync: it does a cheap bounded ConcurrentQueue offer and
    /// nothing else. Canonical-key/aggregation run later on the background send loop
    /// (see <see cref="DrainQueueIntoAggregator"/>), off the evaluation thread. When the queue
    /// is at capacity the event is dropped and counted (observable, never blocks the evaluation).
    /// </summary>
    public void Enqueue(FlagEvalEvent ev)
    {
        // Bounded offer: check capacity with a single Interlocked op, then enqueue. A small
        // transient overshoot under races is acceptable — the cap exists to bound memory, not to be
        // exact — and the count is corrected as the worker drains.
        if (Volatile.Read(ref _queueCount) >= EventQueueCapacity)
        {
            Interlocked.Increment(ref _droppedBackpressure);
            return;
        }

        Interlocked.Increment(ref _queueCount);
        _queue.Enqueue(ev);
        TryToStartSendLoopIfNotStarted();
    }

    /// <summary>
    /// Bounded enqueue WITHOUT starting the background send loop. Test-only seam: lets a unit test
    /// stage events through the same cheap-offer + backpressure path Enqueue uses, then drive
    /// <see cref="FlushAsync()"/> / <see cref="RunSendLoopForTestAsync"/> deterministically.
    /// </summary>
    internal void EnqueueForTest(FlagEvalEvent ev)
    {
        if (Volatile.Read(ref _queueCount) >= EventQueueCapacity)
        {
            Interlocked.Increment(ref _droppedBackpressure);
            return;
        }

        Interlocked.Increment(ref _queueCount);
        _queue.Enqueue(ev);
    }

    /// <summary>
    /// Drains the hand-off queue into the aggregator. Runs only on the background send loop, so all
    /// aggregation cost (prune + canonical key + map insert under the aggregator lock) stays off the
    /// evaluation hot path. Returns the observed backpressure drop count for this drain (reset).
    /// </summary>
    internal long DrainQueueIntoAggregator()
    {
        while (_queue.TryDequeue(out var ev))
        {
            Interlocked.Decrement(ref _queueCount);
            _aggregator.Add(ev);
        }

        return Interlocked.Exchange(ref _droppedBackpressure, 0);
    }

    /// <summary>
    /// Runs the background send loop once and then performs the shutdown drain, returning when the
    /// loop has fully exited. Test-only seam for exercising the shutdown-flush path deterministically.
    /// </summary>
    internal Task RunSendLoopForTestAsync()
    {
        _processExit.TrySetResult(true);
        return SendLoopAsync();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _processExit.TrySetResult(true);
        try
        {
            _sendLoopTask?.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FeatureFlags FlagEvaluation send loop failed during shutdown");
        }
    }

    /// <summary>
    /// Builds the EVP payload from the aggregator's current state. Returns null when there is
    /// nothing to send (both maps empty). Drains the aggregator.
    /// Exposed as internal for unit testing; production callers use Enqueue + the send loop.
    /// </summary>
    internal static FlagEvaluationsRequest? BuildPayload(
        FlagEvaluationAggregator aggregator,
        string service,
        string env,
        string version)
    {
        DrainResult result = aggregator.Drain();
        Dictionary<FullKey, EvaluationEntry> full = result.Full;
        Dictionary<DegradedKey, EvaluationEntry> degraded = result.Degraded;
        long dropped = result.Dropped;
        long degradedRows = CountEvaluations(degraded.Values);

        RecordFlagEvaluationRowsDropped(MetricTags.FlagEvaluationReason.DegradedCap, dropped);
        RecordFlagEvaluationRowsDegraded(MetricTags.FlagEvaluationReason.CardinalityCap, degradedRows);

        if (dropped > 0)
        {
            Log.Warning<long>("FlagEvaluationApi: degraded aggregation tier full — dropped {Dropped} evaluation(s); raise degradedCap (best-effort telemetry)", dropped);
        }

        if (full.Count == 0 && degraded.Count == 0)
        {
            return null;
        }

        long flushTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<FlagEvaluationEvent>(full.Count + degraded.Count);

        // Full tier: all fields present including targeting_key and context.
        foreach (KeyValuePair<FullKey, EvaluationEntry> pair in full)
        {
            FullKey key = pair.Key;
            EvaluationEntry entry = pair.Value;

            var ev = new FlagEvaluationEvent
            {
                Timestamp = flushTimeMs,
                Flag = new FlagEvalFlag { Key = key.FlagKey },
                FirstEvaluation = entry.FirstEvaluationMs,
                LastEvaluation = entry.LastEvaluationMs,
                EvaluationCount = entry.Count,
                RuntimeDefaultUsed = entry.RuntimeDefault ? (bool?)true : null,
                TargetingKey = StringUtil.IsNullOrEmpty(key.TargetingKey) ? null : key.TargetingKey,
                Variant = StringUtil.IsNullOrEmpty(key.Variant) ? null : new FlagEvalVariant { Key = key.Variant },
                Allocation = StringUtil.IsNullOrEmpty(key.AllocationKey) ? null : new FlagEvalAllocation { Key = key.AllocationKey },
                Error = StringUtil.IsNullOrEmpty(key.ErrorMessage) ? null : new FlagEvalError { Message = key.ErrorMessage },
                Context = entry.ContextAttrs is { Count: > 0 } ? new FlagEvalEventContext { Evaluation = entry.ContextAttrs } : null,
            };
            events.Add(ev);
        }

        // Degraded tier: no targeting_key, no context (left null → omitted by NullValueHandling.Ignore).
        foreach (KeyValuePair<DegradedKey, EvaluationEntry> pair in degraded)
        {
            DegradedKey key = pair.Key;
            EvaluationEntry entry = pair.Value;

            var ev = new FlagEvaluationEvent
            {
                Timestamp = flushTimeMs,
                Flag = new FlagEvalFlag { Key = key.FlagKey },
                FirstEvaluation = entry.FirstEvaluationMs,
                LastEvaluation = entry.LastEvaluationMs,
                EvaluationCount = entry.Count,
                RuntimeDefaultUsed = entry.RuntimeDefault ? (bool?)true : null,
                Variant = StringUtil.IsNullOrEmpty(key.Variant) ? null : new FlagEvalVariant { Key = key.Variant },
                Allocation = StringUtil.IsNullOrEmpty(key.AllocationKey) ? null : new FlagEvalAllocation { Key = key.AllocationKey },
                Error = StringUtil.IsNullOrEmpty(key.ErrorMessage) ? null : new FlagEvalError { Message = key.ErrorMessage },
                // TargetingKey = null (omitted), Context = null (omitted) by NullValueHandling.Ignore
            };
            events.Add(ev);
        }

        return new FlagEvaluationsRequest
        {
            Context = new FlagEvalDDContext
            {
                Service = service,
                Env = StringUtil.IsNullOrEmpty(env) ? null : env,
                Version = StringUtil.IsNullOrEmpty(version) ? null : version,
            },
            FlagEvaluations = events,
        };
    }

    /// <summary>
    /// Serializes a request with the EXACT production serializer settings (snake_case inner fields,
    /// NullValueHandling.Ignore, camelCase batch key). Test-only seam for asserting the wire format
    /// against the flageval-worker contract without duplicating the serializer configuration.
    /// </summary>
    internal static string SerializeForTest(FlagEvaluationsRequest request) =>
        Util.Json.JsonHelper.SerializeObject(request, SerializerSettings);

    internal static List<byte[]> BuildPayloadBytesForTest(FlagEvaluationsRequest request, int payloadSizeLimit) =>
        BuildPayloadBytes(request, payloadSizeLimit).Payloads;

    internal static PayloadBuildResult BuildPayloadBytesWithStatsForTest(FlagEvaluationsRequest request, int payloadSizeLimit) =>
        BuildPayloadBytes(request, payloadSizeLimit);

    internal static FlagEvaluationsRequest DeserializeForTest(string json) =>
        Util.Json.JsonHelper.DeserializeObject<FlagEvaluationsRequest>(json, SerializerSettings)!;

    private void TryToStartSendLoopIfNotStarted()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        _sendLoopTask = Task.Run(SendLoopAsync);
        _ = _sendLoopTask.ContinueWith(
            t => { Log.Error(t.Exception, "FeatureFlags FlagEvaluation send loop failed"); },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task SendLoopAsync()
    {
        Log.Debug("FlagEvaluationApi::SendLoopAsync -> Enter");
        var nextSend = DateTimeOffset.UtcNow + _sendInterval;
        while (!_processExit.Task.IsCompleted)
        {
            ReportBackpressureDrops(DrainQueueIntoAggregator());

            var now = DateTimeOffset.UtcNow;
            if (now >= nextSend)
            {
                await FlushAsync().ConfigureAwait(false);
                nextSend = now + _sendInterval;
            }

            try
            {
                var delay = nextSend - now;
                if (delay > _drainInterval)
                {
                    delay = _drainInterval;
                }

                await Task.WhenAny(_processExit.Task, Task.Delay(delay)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutting down — ignore.
            }
        }

        // Final drain on shutdown: flush whatever accumulated in the aggregator since the last
        // interval so a process exit does not silently lose the last window of evaluations.
        await FlushAsync().ConfigureAwait(false);

        Log.Debug("FlagEvaluationApi::SendLoopAsync -> Exit");
    }

    /// <summary>
    /// Drains the aggregator and, if there is anything to send, posts one EVP batch to the agent.
    /// Returns true when a batch was built (and a send attempted), false when the aggregator was empty.
    /// Exposed as internal so the shutdown-drain behavior can be exercised in unit tests.
    /// </summary>
    internal Task<bool> FlushAsync() => FlushAsync(EventPlatformProxyConstants.PayloadSizeLimitBytes);

    internal async Task<bool> FlushAsync(int payloadSizeLimit)
    {
        // Drain the hot-path hand-off queue into the aggregator first (off the evaluation thread),
        // then surface any backpressure drops so an undersized queue is observable.
        ReportBackpressureDrops(DrainQueueIntoAggregator());

        try
        {
            var apiRequestFactory = _apiRequestFactory;
            var uri = apiRequestFactory.GetEndpoint(FlagEvaluationPath);
            var payload = BuildPayload(_aggregator, _service, _env, _version);
            if (payload is null)
            {
                return false;
            }

            var payloadResult = BuildPayloadBytes(payload, payloadSizeLimit);
            RecordPayloadBuildResult(payloadResult);

            foreach (var payloadBytes in payloadResult.Payloads)
            {
                var request = apiRequestFactory.Create(uri);
                var compressedPayload = GZip(payloadBytes);
                using var response = await request.PostAsync(new ArraySegment<byte>(compressedPayload), MimeTypes.Json, "gzip").ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while sending Feature Flags evaluation events to the agent");
            return true;
        }
    }

    private static void ReportBackpressureDrops(long droppedBackpressure)
    {
        RecordFlagEvaluationRowsDropped(MetricTags.FlagEvaluationReason.QueueOverflow, droppedBackpressure);

        if (droppedBackpressure > 0)
        {
            Log.Warning<long>("FlagEvaluationApi: evaluation queue full — dropped {Dropped} evaluation(s) under backpressure (best-effort telemetry)", droppedBackpressure);
        }
    }

    private static PayloadBuildResult BuildPayloadBytes(FlagEvaluationsRequest request, int payloadSizeLimit)
    {
        var contextJson = Util.Json.JsonHelper.SerializeObject(request.Context, SerializerSettings);
        var payloadPrefix = $"{{\"context\":{contextJson},\"flagEvaluations\":[";
        const string PayloadSuffix = "]}";
        var basePayloadSize = EncodingHelpers.Utf8NoBom.GetByteCount(payloadPrefix) + EncodingHelpers.Utf8NoBom.GetByteCount(PayloadSuffix);
        var payloads = new List<byte[]>();
        var batch = new List<EncodedEvent>();
        var batchSize = basePayloadSize;
        long droppedOversized = 0;
        long degradedOversized = 0;

        foreach (var ev in request.FlagEvaluations)
        {
            var encodedEvent = EncodeEvent(ev);
            if (!SingleEventFits(basePayloadSize, encodedEvent.SizeBytes, payloadSizeLimit))
            {
                var degraded = DegradeForPayloadLimit(ev);
                if (degraded is null)
                {
                    droppedOversized += ev.EvaluationCount;
                    continue;
                }

                encodedEvent = EncodeEvent(degraded);
                if (!SingleEventFits(basePayloadSize, encodedEvent.SizeBytes, payloadSizeLimit))
                {
                    droppedOversized += ev.EvaluationCount;
                    continue;
                }

                degradedOversized += ev.EvaluationCount;
            }

            var separatorSize = batch.Count > 0 ? 1 : 0;
            if (batchSize + separatorSize + encodedEvent.SizeBytes > payloadSizeLimit && batch.Count > 0)
            {
                payloads.Add(BuildPayloadBytes(payloadPrefix, PayloadSuffix, batch));
                batch.Clear();
                batchSize = basePayloadSize;
            }

            separatorSize = batch.Count > 0 ? 1 : 0;
            batchSize += separatorSize + encodedEvent.SizeBytes;
            batch.Add(encodedEvent);
        }

        if (batch.Count > 0)
        {
            payloads.Add(BuildPayloadBytes(payloadPrefix, PayloadSuffix, batch));
        }

        if (droppedOversized > 0)
        {
            Log.Warning<long>("FlagEvaluationApi: dropped {Dropped} oversized flag evaluation event(s) after payload-limit degradation (best-effort telemetry)", droppedOversized);
        }

        var splitPayloads = payloads.Count > 1 ? payloads.Count - 1 : 0;
        return new PayloadBuildResult(payloads, droppedOversized, degradedOversized, splitPayloads);
    }

    private static long CountEvaluations(IEnumerable<EvaluationEntry> entries)
    {
        long total = 0;
        foreach (var entry in entries)
        {
            total += entry.Count;
        }

        return total;
    }

    private static void RecordPayloadBuildResult(PayloadBuildResult result)
    {
        RecordFlagEvaluationRowsDropped(MetricTags.FlagEvaluationReason.PayloadLimit, result.DroppedPayloadLimit);
        RecordFlagEvaluationRowsDegraded(MetricTags.FlagEvaluationReason.PayloadLimit, result.DegradedPayloadLimit);
        RecordTelemetryCount(result.SplitPayloadCount, TelemetryFactory.Metrics.RecordCountFlagEvaluationPayloadSplits);
    }

    private static void RecordFlagEvaluationRowsDropped(MetricTags.FlagEvaluationReason reason, long value)
    {
        RecordTelemetryCount(value, increment => TelemetryFactory.Metrics.RecordCountFlagEvaluationRowsDropped(reason, increment));
    }

    private static void RecordFlagEvaluationRowsDegraded(MetricTags.FlagEvaluationReason reason, long value)
    {
        RecordTelemetryCount(value, increment => TelemetryFactory.Metrics.RecordCountFlagEvaluationRowsDegraded(reason, increment));
    }

    private static void RecordTelemetryCount(long value, Action<int> record)
    {
        if (value <= 0)
        {
            return;
        }

        while (value > int.MaxValue)
        {
            record(int.MaxValue);
            value -= int.MaxValue;
        }

        record((int)value);
    }

    private static EncodedEvent EncodeEvent(FlagEvaluationEvent ev)
    {
        var json = Util.Json.JsonHelper.SerializeObject(ev, SerializerSettings);
        return new EncodedEvent(json, EncodingHelpers.Utf8NoBom.GetByteCount(json));
    }

    private static bool SingleEventFits(int basePayloadSize, int eventSize, int payloadSizeLimit) =>
        basePayloadSize + eventSize <= payloadSizeLimit;

    private static byte[] BuildPayloadBytes(string payloadPrefix, string payloadSuffix, List<EncodedEvent> events)
    {
        var builder = new StringBuilder(payloadPrefix);
        for (int i = 0; i < events.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(events[i].Json);
        }

        builder.Append(payloadSuffix);
        return EncodingHelpers.Utf8NoBom.GetBytes(builder.ToString());
    }

    private static FlagEvaluationEvent? DegradeForPayloadLimit(FlagEvaluationEvent ev)
    {
        if (ev.TargetingKey is null && ev.Context is null)
        {
            return null;
        }

        return new FlagEvaluationEvent
        {
            Timestamp = ev.Timestamp,
            Flag = ev.Flag,
            FirstEvaluation = ev.FirstEvaluation,
            LastEvaluation = ev.LastEvaluation,
            EvaluationCount = ev.EvaluationCount,
            RuntimeDefaultUsed = ev.RuntimeDefaultUsed,
            Variant = ev.Variant,
            Allocation = ev.Allocation,
            Error = ev.Error,
            TargetingKey = null,
            Context = null,
        };
    }

    private static byte[] GZip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private readonly struct EncodedEvent
    {
        public EncodedEvent(string json, int sizeBytes)
        {
            Json = json;
            SizeBytes = sizeBytes;
        }

        public string Json { get; }

        public int SizeBytes { get; }
    }

    internal readonly struct PayloadBuildResult
    {
        public PayloadBuildResult(List<byte[]> payloads, long droppedPayloadLimit, long degradedPayloadLimit, int splitPayloadCount)
        {
            Payloads = payloads;
            DroppedPayloadLimit = droppedPayloadLimit;
            DegradedPayloadLimit = degradedPayloadLimit;
            SplitPayloadCount = splitPayloadCount;
        }

        public List<byte[]> Payloads { get; }

        public long DroppedPayloadLimit { get; }

        public long DegradedPayloadLimit { get; }

        public int SplitPayloadCount { get; }
    }
}
