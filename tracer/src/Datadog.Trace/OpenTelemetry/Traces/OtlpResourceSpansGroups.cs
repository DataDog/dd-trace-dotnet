// <copyright file="OtlpResourceSpansGroups.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.OpenTelemetry.Traces;

/// <summary>
/// Tracks the <c>ResourceSpans</c> that an OTLP trace serializer opened in the current payload, in write order.
/// Chunks that produce the same resource attributes (see <see cref="OtlpResourceIdentity"/>) share one
/// <c>ResourceSpans</c> (the common case). A chunk that doesn't, e.g. a trace created before a remote configuration
/// change of the global tags, opens a new <c>ResourceSpans</c> in the same payload instead of being written under
/// another trace's resource.
/// </summary>
/// <remarks>
/// Offsets are absolute positions in the caller's destination buffer (<c>SpanBuffer._buffer</c>), not in the
/// temporary serialization buffer. <c>SpanBuffer.TryWrite</c> copies the temporary buffer into the destination at
/// the <c>spanBufferOffset</c> passed to <c>SerializeSpans</c>, unless it rejects the chunk after serializing it.
/// Committed chunks always write at least one byte, so a group that starts at or after the current write position
/// was opened by a chunk that was never committed, or belongs to a previous payload.
/// </remarks>
internal sealed class OtlpResourceSpansGroups
{
    private readonly List<Group> _groups = [];

    public int Count => _groups.Count;

    public Group this[int index] => _groups[index];

    /// <summary>
    /// Prepares to write <paramref name="traceChunk"/> at <paramref name="spanBufferOffset"/>. Returns the resource
    /// of a new <c>ResourceSpans</c> to open, or <c>null</c> when the chunk shares the current one.
    /// </summary>
    public OtlpResourceIdentity? GetNewResource(in TraceChunkModel traceChunk, OtlpResourceGlobalTags? globalTags, int spanBufferOffset)
    {
        RemoveUncommitted(spanBufferOffset);

        if (_groups.Count > 0 && _groups[_groups.Count - 1].Resource.Matches(in traceChunk, globalTags))
        {
            return null;
        }

        return new OtlpResourceIdentity(in traceChunk, globalTags);
    }

    /// <summary>
    /// Records a group. Only call once the chunk that opened it serialized successfully.
    /// </summary>
    public void Add(Group group) => _groups.Add(group);

    /// <summary>
    /// Gets the end offset of the group at <paramref name="index"/>: where the next group starts,
    /// or <paramref name="payloadEnd"/> for the last group.
    /// </summary>
    public int GetEndOffset(int index, int payloadEnd)
        => index + 1 < _groups.Count ? _groups[index + 1].StartOffset : payloadEnd;

    /// <summary>
    /// Forgets the groups that start at or after <paramref name="committedOffset"/>, which were opened by chunks
    /// that never made it into the destination buffer (or by a previous payload when it is the header size).
    /// </summary>
    public void RemoveUncommitted(int committedOffset)
    {
        var count = _groups.Count;
        while (count > 0 && _groups[count - 1].StartOffset >= committedOffset)
        {
            count--;
        }

        if (count < _groups.Count)
        {
            _groups.RemoveRange(count, _groups.Count - count);
        }
    }

    public void Clear() => _groups.Clear();

    internal sealed class Group
    {
        public Group(int startOffset, OtlpResourceIdentity resource)
            : this(startOffset, resourceSpansLengthPos: -1, scopeSpansLengthPos: -1, resource)
        {
        }

        public Group(int startOffset, int resourceSpansLengthPos, int scopeSpansLengthPos, OtlpResourceIdentity resource)
        {
            StartOffset = startOffset;
            ResourceSpansLengthPos = resourceSpansLengthPos;
            ScopeSpansLengthPos = scopeSpansLengthPos;
            Resource = resource;
        }

        /// <summary>Gets the offset of the first byte of the group (the <c>spanBufferOffset</c> of the chunk that opened it).</summary>
        public int StartOffset { get; }

        /// <summary>Gets the offset of the ResourceSpans length placeholder (protobuf only, -1 otherwise).</summary>
        public int ResourceSpansLengthPos { get; }

        /// <summary>Gets the offset of the ScopeSpans length placeholder (protobuf only, -1 otherwise).</summary>
        public int ScopeSpansLengthPos { get; }

        /// <summary>Gets the resource the group was opened with.</summary>
        public OtlpResourceIdentity Resource { get; }
    }
}
