// <copyright file="DataStreamsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Manages all the data streams monitoring behaviour
/// </summary>
internal sealed class DataStreamsManager
{
    /// <summary>
    /// Maximum number of distinct keys stored in a single per-type edge-tag cache.
    /// When the limit is reached, new keys are computed on the fly without caching to
    /// prevent unbounded memory growth caused by high-cardinality identifiers.
    /// </summary>
    internal const int MaxEdgeTagCacheSize = 1000;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsManager>();
    private static readonly AsyncLocal<PathwayContext?> LastConsumePathway = new(); // saves the context on consume checkpointing only
    private readonly object _nodeHashUpdateLock = new();
    private readonly ConcurrentDictionary<string, RateLimiter> _schemaRateLimiters = new();
    private readonly IDiscoveryService _discoveryService;
    private readonly DataStreamsExtractorRegistry _registry;
    private readonly IDisposable _updateSubscription;
    private readonly bool _isLegacyDsmHeadersEnabled;
    private readonly bool _isInDefaultState;
    // Keyed by string[] identity (reference equality) — safe because EdgeTagCache holds strong
    // references to the cached arrays (bounded by MaxEdgeTagCacheSize).
    private readonly ConcurrentDictionary<string[], NodeHashCacheEntry> _nodeHashCache =
        new(NodeHashCacheKeyComparer.Instance);

    private long _nodeHashBase; // note that this actually represents a `ulong` that we have done an unsafe cast for
    private MutableSettings _previousMutableSettings;
    private string? _previousContainerTagsHash;
    private bool _isEnabled;
    private IDataStreamsWriter? _writer;

    public DataStreamsManager(
        TracerSettings tracerSettings,
        IDataStreamsWriter? writer,
        IDiscoveryService discoveryService)
    {
        _isEnabled = writer is not null;
        _isLegacyDsmHeadersEnabled = tracerSettings.IsDataStreamsLegacyHeadersEnabled;
        _writer = writer;
        _discoveryService = discoveryService;
        _isInDefaultState = tracerSettings.IsDataStreamsMonitoringInDefaultState;
        _registry = new DataStreamsExtractorRegistry(tracerSettings.DataStreamsTransactionExtractors);

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Data Streams extractors loaded: {Value}", _registry.AsJson());
        }

        _previousMutableSettings = tracerSettings.Manager.InitialMutableSettings;
        // even though the value will probably get updated by a callback when subscriptions happen just after,
        // we still need to initialize it to a value from initial settings in case no callback fire
        UpdateNodeHash(_previousMutableSettings, containerTagsHash: null);
        // subscribing to changes calls the callback immediately if a value is present
        discoveryService.SubscribeToChanges(UpdateHashWithContainerTags);
        _updateSubscription = tracerSettings.Manager.SubscribeToChanges(UpdateHashWithNewSettings);
    }

    public bool IsEnabled
    {
        get => Volatile.Read(ref _isEnabled);
    }

    public bool IsInDefaultState => _isInDefaultState;

    public bool IsTransactionTrackingEnabled => !_isInDefaultState && IsEnabled;

    /// <summary> Callback for AgentConfiguration updates </summary>
    private void UpdateHashWithContainerTags(AgentConfiguration conf)
    {
        lock (_nodeHashUpdateLock)
        {
            if (conf.ContainerTagsHash == _previousContainerTagsHash)
            {
                return;
            }

            UpdateNodeHash(_previousMutableSettings, conf.ContainerTagsHash);
            _previousContainerTagsHash = conf.ContainerTagsHash;
        }
    }

    /// <summary> Callback for MutableSettings updates </summary>
    private void UpdateHashWithNewSettings(TracerSettings.SettingsManager.SettingChanges updates)
    {
        if (updates.UpdatedMutable is { } updated)
        {
            lock (_nodeHashUpdateLock)
            {
                UpdateNodeHash(updated, _previousContainerTagsHash);
                _previousMutableSettings = updated;
            }
        }
    }

    private void UpdateNodeHash(MutableSettings settings, string? containerTagsHash)
    {
        // We don't yet support primary tag in .NET yet
        var value = HashHelper.CalculateNodeHashBase(settings.DefaultServiceName, settings.Environment, primaryTag: null, settings.ProcessTags?.SerializedTags, containerTagsHash);
        // Working around the fact we can't do Interlocked.Exchange with the struct
        // and also that we can't do Interlocked.Exchange with a ulong in < .NET 5
        Interlocked.Exchange(
            ref _nodeHashBase,
            unchecked((long)value.Value)); // reinterpret as a long
    }

    public static DataStreamsManager Create(
        TracerSettings settings,
        ProfilerSettings profilerSettings,
        IDiscoveryService discoveryService)
    {
        var writer = settings.IsDataStreamsMonitoringEnabled
                         ? DataStreamsWriter.Create(settings, profilerSettings, discoveryService)
                         : null;

        return new DataStreamsManager(settings, writer, discoveryService);
    }

    public async Task DisposeAsync()
    {
        _updateSubscription.Dispose();
        _discoveryService.RemoveSubscription(UpdateHashWithContainerTags);
        Volatile.Write(ref _isEnabled, false);
        var writer = Interlocked.Exchange(ref _writer, null);

        if (writer is null)
        {
            return;
        }

        await writer.DisposeAsync().ConfigureAwait(false);
    }

    public async Task FlushAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        var writer = Volatile.Read(ref _writer);
        if (writer is null)
        {
            return;
        }

        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Trys to extract a <see cref="PathwayContext"/>, from the provided <paramref name="headers"/>
    /// If data streams is disabled, or no pathway is present, returns null.
    /// </summary>
    public PathwayContext? ExtractPathwayContext<TCarrier>(TCarrier headers)
        where TCarrier : IBinaryHeadersCollection
        => IsEnabled ? DataStreamsContextPropagator.Instance.Extract(headers) : null;

    public List<DataStreamsTransactionExtractor>? GetExtractorsByType(DataStreamsTransactionExtractor.ExtractorType extractorType)
    {
        return _registry.GetExtractorsByType(extractorType);
    }

    /// <summary>
    /// Injects a <see cref="PathwayContext"/> into headers
    /// </summary>
    /// <param name="context">The pathway context to inject</param>
    /// <param name="headers">The header collection to inject the headers into</param>
    public void InjectPathwayContext<TCarrier>(PathwayContext? context, TCarrier headers)
        where TCarrier : IBinaryHeadersCollection
    {
        if (!IsEnabled || context is null)
        {
            return;
        }

        DataStreamsContextPropagator.Instance.Inject(context.Value, headers, _isLegacyDsmHeadersEnabled);
    }

    public void TrackTransaction(string transactionId, string checkpointName)
    {
        if (!IsTransactionTrackingEnabled)
        {
            return;
        }

        var writer = Volatile.Read(ref _writer);
        writer?.AddTransaction(new DataStreamsTransactionInfo(
                                   transactionId,
                                   DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(),
                                   checkpointName));
    }

    public void TrackTransaction(byte[] transactionIdBytes, string checkpointName)
    {
        if (!IsTransactionTrackingEnabled)
        {
            return;
        }

        var writer = Volatile.Read(ref _writer);
        writer?.AddTransaction(new DataStreamsTransactionInfo(
                                   transactionIdBytes,
                                   DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(),
                                   checkpointName));
    }

    public void TrackBacklog(string tags, long value)
    {
        if (!IsEnabled)
        {
            return;
        }

        var writer = Volatile.Read(ref _writer);
        var point = new BacklogPoint(tags, value, DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());
        writer?.AddBacklog(point);
    }

    /// <summary>
    /// Trys to extract a <see cref="PathwayContext"/>, from the provided <paramref name="headers"/>
    /// If data streams is disabled, or no pathway is present, returns null.
    /// </summary>
    public PathwayContext? ExtractPathwayContextAsBase64String<TCarrier>(TCarrier headers)
        where TCarrier : IHeadersCollection
        => IsEnabled ? DataStreamsContextPropagator.Instance.ExtractAsBase64String(headers) : null;

    /// <summary>
    /// Injects a <see cref="PathwayContext"/> into headers
    /// </summary>
    /// <param name="context">The pathway context to inject</param>
    /// <param name="headers">The header collection to inject the headers into</param>
    public void InjectPathwayContextAsBase64String<TCarrier>(PathwayContext? context, TCarrier headers)
        where TCarrier : IHeadersCollection
    {
        if (!IsEnabled)
        {
            return;
        }

        if (context is not null)
        {
            DataStreamsContextPropagator.Instance.InjectAsBase64String(context.Value, headers);
            return;
        }

        // This shouldn't happen normally, as you should call SetCheckpoint before calling InjectPathwayContext
        // But if data streams was disabled, you call SetCheckpoint, and then data streams is enabled
        // you will hit this code path
        Log.Debug("Attempted to inject null pathway context");
    }

    /// <summary>
    /// Sets a checkpoint using the provided <see cref="PathwayContext"/>
    /// NOTE: <paramref name="edgeTags"/> must be in correct sort order
    /// </summary>
    /// <param name="parentPathway">The pathway from upstream, if known</param>
    /// <param name="checkpointKind">Is this a Produce or Consume operation?</param>
    /// <param name="edgeTags">Edge tags to set for the new pathway. MUST be sorted in alphabetical order</param>
    /// <param name="payloadSizeBytes">Payload size in bytes</param>
    /// <param name="timeInQueueMs">Edge start time extracted from the message metadata. Used only if this is start of the pathway</param>
    /// <returns>If disabled, returns <c>null</c>. Otherwise returns a new <see cref="PathwayContext"/></returns>
    public PathwayContext? SetCheckpoint(
        in PathwayContext? parentPathway,
        CheckpointKind checkpointKind,
        string[] edgeTags,
        long payloadSizeBytes,
        long timeInQueueMs)
    {
        if (!IsEnabled)
        {
            return null;
        }

        try
        {
            var previousContext = parentPathway;
            if (previousContext == null && checkpointKind == CheckpointKind.Produce)
            {
                // We only enter here on produce: when we consume, the only thing that matters is the parent we'd have read from the inbound message, not what happened before.
                // We want to use the context from the previous consume (but we'll give priority to the parent passed in param if set).
                previousContext = LastConsumePathway.Value;
            }

            var nowNs = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
            // We should use timeInQueue to offset the edge / pathway start if this is a beginning of a pathway
            // This allows tracking edge / pathway latency for pipelines starting with a queue (no producer instrumented upstream)
            // by relying on the message timestamp.
            // ReSharper disable once ArrangeRedundantParentheses
            var edgeStartNs = previousContext == null && timeInQueueMs > 0 ? nowNs - (timeInQueueMs * 1_000_000) : nowNs;
            var pathwayStartNs = previousContext?.PathwayStart ?? edgeStartNs;

            // Don't blame me, blame the fact we can't do Volatile.Read with a ulong in .NET FX...
            var nodeHashBase = new NodeHashBase(unchecked((ulong)Volatile.Read(ref _nodeHashBase)));
            var cacheEntry = _nodeHashCache.GetOrAdd(edgeTags, static _ => new NodeHashCacheEntry());
            NodeHash nodeHash;

            // Fast lock-free path: snapshot is an immutable object published via a volatile field.
            // If the base still matches we avoid taking any lock on the hot path.
            if (!cacheEntry.TryGetNodeHash(nodeHashBase, out nodeHash))
            {
                lock (cacheEntry)
                {
                    // Double-check under lock in case another thread raced to update
                    if (!cacheEntry.TryGetNodeHash(nodeHashBase, out nodeHash))
                    {
                        nodeHash = HashHelper.CalculateNodeHash(nodeHashBase, edgeTags);
                        cacheEntry.Store(nodeHashBase, nodeHash);
                    }
                }
            }

            var parentHash = previousContext?.Hash ?? default;
            var pathwayHash = HashHelper.CalculatePathwayHash(nodeHash, parentHash);

            var writer = Volatile.Read(ref _writer);
            writer?.Add(
                new StatsPoint(
                    edgeTags: edgeTags,
                    hash: pathwayHash,
                    parentHash: parentHash,
                    timestampNs: nowNs,
                    pathwayLatencyNs: nowNs - pathwayStartNs,
                    edgeLatencyNs: nowNs - (previousContext?.EdgeStart ?? edgeStartNs),
                    payloadSizeBytes));

            var pathway = new PathwayContext(
                hash: pathwayHash,
                pathwayStartNs: pathwayStartNs,
                edgeStartNs: edgeStartNs);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "SetCheckpoint with {PathwayHash}, {PathwayStart}, {EdgeStart}",
                    pathway.Hash,
                    pathway.PathwayStart,
                    pathway.EdgeStart);
            }

            // overwrite the previous checkpoint, so it can be used in the future if needed
            if (checkpointKind == CheckpointKind.Consume)
            {
                LastConsumePathway.Value = pathway;
            }

            return pathway;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting a data streams checkpoint. Disabling data streams monitoring");
            // Set this to false out of an abundance of caution.
            // We will look at being less conservative in the future
            // if we see intermittent errors for some reason.
            Volatile.Write(ref _isEnabled, false);
            return null;
        }
    }

    /// <summary>
    /// Returns a cached edge-tag array for the given key, creating and caching it on first use.
    /// On cache hits, zero heap allocations occur. The factory is only invoked on the first call
    /// per unique key, making this safe to use on high-throughput hot paths.
    /// Once the cache reaches <see cref="MaxEdgeTagCacheSize"/> entries the result is computed
    /// fresh each time (no caching) to bound memory usage for high-cardinality key spaces.
    /// </summary>
    /// <typeparam name="TKey">A value type (struct) used as the cache key — no boxing.</typeparam>
    /// <param name="key">The cache key derived from the caller's natural identifiers.</param>
    /// <param name="factory">A static factory that builds the edge-tag array from the key on cache miss.</param>
    public string[] GetOrCreateEdgeTags<TKey>(TKey key, Func<TKey, string[]> factory)
        where TKey : IEquatable<TKey>
        => TagCache<TKey, string[]>.GetOrCreate(key, factory, MaxEdgeTagCacheSize);

    /// <summary>
    /// Returns a cached backlog tag string for the given key, creating and caching it on first use.
    /// On cache hits, zero heap allocations occur. The factory is only invoked on the first call
    /// per unique key, making this safe to use on high-throughput hot paths.
    /// Once the cache reaches <see cref="MaxEdgeTagCacheSize"/> entries the result is computed
    /// fresh each time (no caching) to bound memory usage for high-cardinality key spaces.
    /// </summary>
    /// <typeparam name="TKey">A value type (struct) used as the cache key — no boxing.</typeparam>
    /// <param name="key">The cache key derived from the caller's natural identifiers.</param>
    /// <param name="factory">A static factory that builds the backlog tag string from the key on cache miss.</param>
    public string GetOrCreateBacklogTags<TKey>(TKey key, Func<TKey, string> factory)
        where TKey : IEquatable<TKey>
        => TagCache<TKey, string>.GetOrCreate(key, factory, MaxEdgeTagCacheSize);

    /// <summary>
    /// Make sure we only extract the schema (a costly operation) on select occasions
    /// </summary>
    public bool ShouldExtractSchema(Span span, string operation, out int weight)
    {
        var limiter = _schemaRateLimiters.GetOrAdd(operation, _ => new RateLimiter());
        if (limiter.PeekDecision())
        {
            // we only want to "consume" a decision to extract the schema for a span that we are going to keep
            // && we don't want to make the sampling decision if we know we have no chance of getting selected by the rate limiter
            var spanSamplingDecision = span.Context.GetOrMakeSamplingDecision();
            if (spanSamplingDecision != null && SamplingPriorityValues.IsKeep(spanSamplingDecision.Value))
            {
                return limiter.GetDecision(out weight);
            }
        }

        weight = 0;
        return false;
    }

    private static class TagCache<TKey, TValue>
        where TKey : IEquatable<TKey>
    {
        private static readonly ConcurrentDictionary<TKey, TValue> Cache = new();
        private static int _count;

        internal static TValue GetOrCreate(TKey key, Func<TKey, TValue> factory, int maxSize)
        {
            if (Cache.TryGetValue(key, out var existing))
            {
                return existing;
            }

            if (_count >= maxSize)
            {
                return factory(key);
            }

            var result = Cache.GetOrAdd(key, factory);
            Interlocked.Increment(ref _count);
            return result;
        }
    }

    /// <summary>
    /// Reference-equality comparer for string[] keys in <see cref="_nodeHashCache"/>.
    /// Two string[] objects are considered equal only when they are the same instance,
    /// which is always true for the cached arrays held by <see cref="TagCache{TKey, TValue}"/>.
    /// </summary>
    private sealed class NodeHashCacheKeyComparer : IEqualityComparer<string[]>
    {
        internal static readonly NodeHashCacheKeyComparer Instance = new();

        public bool Equals(string[]? x, string[]? y) => ReferenceEquals(x, y);

        public int GetHashCode(string[] obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Memoized NodeHash associated with a specific edge-tag array instance and nodeHashBase value.
    /// The volatile <see cref="_snapshot"/> field enables a lock-free fast path: callers read the
    /// snapshot without a lock, and only acquire the lock when the base has changed or is missing.
    /// </summary>
    private sealed class NodeHashCacheEntry
    {
        // Immutable snapshot published via volatile write; null until first computation.
        private volatile NodeHashSnapshot? _snapshot;

        /// <summary>
        /// Tries to return the cached <see cref="NodeHash"/> for <paramref name="nodeHashBase"/>
        /// without acquiring any lock (lock-free read via volatile field).
        /// </summary>
        public bool TryGetNodeHash(NodeHashBase nodeHashBase, out NodeHash nodeHash)
        {
            var snap = _snapshot; // volatile read — acts as a load-acquire barrier
            if (snap is not null && snap.Base == nodeHashBase.Value)
            {
                nodeHash = snap.Hash;
                return true;
            }

            nodeHash = default;
            return false;
        }

        /// <summary>
        /// Stores a newly-computed <see cref="NodeHash"/>. Must be called under a lock held by the caller.
        /// The volatile write ensures the snapshot is visible to all threads before the lock is released.
        /// </summary>
        public void Store(NodeHashBase nodeHashBase, NodeHash nodeHash)
        {
            _snapshot = new NodeHashSnapshot(nodeHashBase.Value, nodeHash); // volatile write
        }

        /// <summary>Immutable payload published atomically via the volatile <see cref="_snapshot"/> field.</summary>
        private sealed class NodeHashSnapshot
        {
            private readonly ulong _base;
            private readonly NodeHash _hash;

            internal NodeHashSnapshot(ulong @base, NodeHash hash)
            {
                _base = @base;
                _hash = hash;
            }

            internal ulong Base => _base;

            internal NodeHash Hash => _hash;
        }
    }
}
