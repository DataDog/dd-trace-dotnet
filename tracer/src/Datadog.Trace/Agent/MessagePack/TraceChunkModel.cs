// <copyright file="TraceChunkModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Contains one of more spans (a trace chunk) that belong to the same trace
/// and additional context needed for serialization.
/// </summary>
internal readonly struct TraceChunkModel
{
    // for small trace chunks, use the ArraySegment<Span> copy directly, no heap allocations
    private readonly ArraySegment<Span> _spans;

    // for large trace chunks, use a HashSet<ulong> instead of iterating the array.
    // there are 3 possible states:
    //   _hashSet == null, we know in advance that we won't use it (small traces)
    //   _hashSet.IsValueCreated == false, we may need it (large traces), but we haven't used it yet (and it may never be used)
    //   _hashSet.IsValueCreated == true, we needed the HashSet so we initialized it
    private readonly Lazy<HashSet<ulong>>? _hashSet;

    public readonly string? DefaultServiceName = null;

    public readonly int? SamplingPriority = null;

    public readonly string? Environment = null;

    public readonly string? ServiceVersion = null;

    public readonly string? GitRepositoryUrl = null;

    public readonly string? GitCommitSha = null;

    public readonly string? Origin = null;

    public readonly TraceTagCollection? Tags = null;

    public readonly ulong? LocalRootSpanId = null;

    public readonly bool ContainsLocalRootSpan = false;

    public readonly bool HasUpstreamService = false;

    public readonly bool IsRunningInAzureAppService = false;

    public readonly ImmutableAzureAppServiceSettings? AzureAppServiceSettings = null;

    public readonly bool IsApmEnabled = true;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TraceChunkModel"/> struct.
    /// </summary>
    /// <param name="spans">The spans that will be within this <see cref="TraceChunkModel"/>.</param>
    /// <param name="samplingPriority">Optional sampling priority to override the <see cref="TraceContext"/> sampling priority.</param>
    public TraceChunkModel(in ArraySegment<Span> spans, int? samplingPriority = null)
        : this(spans, TraceContext.GetTraceContext(spans), samplingPriority)
    {
        // since all we have is an array of spans, use the trace context from the first span
        // to get the other values we need (sampling priority, origin, trace tags, etc) for now.
        // the idea is that as we refactor further, we can pass more than just the spans,
        // and these values can come directly from the trace context.
    }

    // used only to chain constructors
    private TraceChunkModel(in ArraySegment<Span> spans, TraceContext? traceContext, int? samplingPriority)
        : this(spans, traceContext?.RootSpan)
    {
        // sampling decision override takes precedence over TraceContext.SamplingPriority
        SamplingPriority = samplingPriority;

        if (traceContext is not null)
        {
            // only use TraceContext.SamplingPriority if there was  no override value
            SamplingPriority ??= traceContext.SamplingPriority;

            Environment = traceContext.Environment;
            ServiceVersion = traceContext.ServiceVersion;
            Origin = traceContext.Origin;
            Tags = traceContext.Tags;

            if (traceContext.Tracer is { } tracer)
            {
                DefaultServiceName = tracer.DefaultServiceName;

                if (tracer.Settings is { } settings)
                {
                    IsRunningInAzureAppService = settings.IsRunningInAzureAppService;
                    AzureAppServiceSettings = settings.AzureAppServiceMetadata ?? null;
                    IsApmEnabled = !settings.AppsecStandaloneEnabledInternal;
                }

                if (tracer.GitMetadataTagsProvider?.TryExtractGitMetadata(out var gitMetadata) == true &&
                    gitMetadata != GitMetadata.Empty)
                {
                    GitRepositoryUrl = gitMetadata.RepositoryUrl;
                    GitCommitSha = gitMetadata.CommitSha;
                }
            }
        }
    }

    // used in tests
    internal TraceChunkModel(in ArraySegment<Span> spans, Span? localRootSpan)
    {
        _spans = spans;

        // don't create the Lazy<T> instance if we know we won't use it (small traces)
        _hashSet = spans.Count > 50 ? new Lazy<HashSet<ulong>>(LazyThreadSafetyMode.None) : null;

        if (localRootSpan is not null)
        {
            var localRootSpanId = localRootSpan.SpanId;
            LocalRootSpanId = localRootSpanId;

            // the local root span is almost always at the end of the chunk, so start searching at the last index.
            // skip the HashSet to avoid initializing it yet, always iterate the array of spans.
            ContainsLocalRootSpan = IndexOf(localRootSpanId, spans.Count - 1) >= 0;

            HasUpstreamService = localRootSpan.Context.ParentIdInternal is not (null or 0);
        }
    }

    public int SpanCount => _spans.Count;

    // used in tests
    internal bool HashSetInitialized => _hashSet?.IsValueCreated == true && _hashSet.Value.Count > 0;

    public SpanModel GetSpanModel(int spanIndex)
    {
        if (spanIndex >= _spans.Count)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(spanIndex));
        }

        var span = _spans.Array![_spans.Offset + spanIndex];
        var parentId = span.Context.ParentIdInternal ?? 0;
        bool isLocalRoot = parentId is 0 || span.SpanId == LocalRootSpanId;
        bool isFirstSpan = spanIndex == 0;

        // calling Contains() can be expensive, so try to short-circuit if possible
        bool isChunkOrphan;

        if (isLocalRoot)
        {
            // if this the local root span, it is also a chunk orphan
            // (we won't find a parent in this trace chunk, by definition)
            isChunkOrphan = true;
        }
        else
        {
            // if this span's parent is the local root span, we will already know if its parent
            // is present or not because we already looked for the local root span
            var parentIsLocalRoot = LocalRootSpanId == parentId;

            if (ContainsLocalRootSpan && (!HasUpstreamService || parentIsLocalRoot))
            {
                // if this trace chunk contains the _distributed_ root span (parentId == 0), don't bother
                // marking any other spans as orphans (even if they are!) because the trace agent will always pick parentId == 0 first
                // (common case if partial flushing is disabled and there is no upstream service)
                // ...OR...
                // if the span's parent is the local root span, and we already know it is present in this trace chunk
                // (common case because most local traces only have 2 tiers of spans and
                // child spans are direct descendents of the local root, i.e. no grandchildren)
                isChunkOrphan = false;
            }
            else if (!ContainsLocalRootSpan && parentIsLocalRoot)
            {
                // if the span's parent is the local root span, and we already know it is _not_ present in this trace chunk
                isChunkOrphan = true;
            }
            else
            {
                // call Contains() as last resort. note we already checked span.Context.ParentId for null above.
                isChunkOrphan = !Contains(parentId, spanIndex);
            }
        }

        return new SpanModel(
            span,
            this,
            isLocalRoot: isLocalRoot,
            isChunkOrphan: isChunkOrphan,
            isFirstSpanInChunk: isFirstSpan);
    }

    /// <summary>
    /// Searches for the specified spanId.
    /// </summary>
    private bool Contains(ulong spanId, int startIndex)
    {
        // for larger trace chunks, use a HashSet instead of iterating the array, but only initialize it on first use.
        // even for large traces, sometimes we can get away without ever using the HashSet.
        if (_hashSet is not null)
        {
            var hashSet = _hashSet.Value;

            if (hashSet.Count == 0)
            {
                for (var i = 0; i < _spans.Count; i++)
                {
                    hashSet.Add(_spans.Array![_spans.Offset + i].SpanId);
                }
            }

            return hashSet.Contains(spanId);
        }

        return IndexOf(spanId, startIndex) >= 0;
    }

    /// <summary>
    /// Searches for the specified spanId by iteration the array of spans.
    /// </summary>
    private int IndexOf(ulong spanId, int startIndex)
    {
        // wrap around the end of the array
        if (startIndex >= _spans.Count)
        {
            startIndex = 0;
        }

        // iterate over the span array starting at the specified index + 1
        for (var i = startIndex; i < _spans.Count; i++)
        {
            if (spanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return i;
            }
        }

        // if not found above, wrap around to the beginning to search the rest of the array
        for (var i = 0; i < startIndex; i++)
        {
            if (spanId == _spans.Array![_spans.Offset + i].SpanId)
            {
                return i;
            }
        }

        return -1;
    }
}
