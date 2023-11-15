// <copyright file="SpanModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent.MessagePack;

/// <summary>
/// Contains a reference to a <see cref="Span"/> and additional context needed for serialization.
/// </summary>
internal readonly struct SpanModel
{
    public readonly Span Span;

    public readonly TraceChunkModel TraceChunk;

    /// <remarks>
    /// The "local root span" is the root span of the local trace. If there is no upstream service,
    /// this is also the root span of the entire distributed trace and it's parent span id is null or zero.
    /// If there is an upstream service, then the parent span id will be greater than zero.
    /// </remarks>
    public readonly bool IsLocalRoot;

    /// <remarks>
    /// By "chunk orphan" we mean that this span's parent is not found in the same chunk.
    /// The trace agent chooses one of these are the "chunk root", but the choice is not deterministic.
    /// </remarks>
    public readonly bool IsChunkOrphan;

    public readonly bool IsFirstSpanInChunk;

    public SpanModel(
        Span span,
        in TraceChunkModel traceChunk,
        bool isLocalRoot,
        bool isChunkOrphan,
        bool isFirstSpanInChunk)
    {
        Span = span;
        TraceChunk = traceChunk;
        IsLocalRoot = isLocalRoot;
        IsChunkOrphan = isChunkOrphan;
        IsFirstSpanInChunk = isFirstSpanInChunk;
    }
}
