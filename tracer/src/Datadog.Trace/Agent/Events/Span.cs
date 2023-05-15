using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#nullable enable

namespace Datadog.Trace.Agent.Events;

public readonly struct Span : IDisposable, IEquatable<Span>
{
    public static readonly Span None = default;

    private readonly Tracer _tracer;

    public ulong TraceId { get; }

    public ulong SpanId { get; }

    public ulong ParentId { get; }

    internal Span(Tracer tracer, ulong traceId, ulong spanId, ulong parentId)
    {
        _tracer = tracer;
        TraceId = traceId;
        SpanId = spanId;
        ParentId = parentId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddTags(ReadOnlyMemory<KeyValuePair<string, string>> tags)
    {
        _tracer.AddTags(this, tags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddTags(ReadOnlyMemory<KeyValuePair<string, double>> tags)
    {
        _tracer.AddTags(this, tags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _tracer.FinishSpan(this);
    }

    public bool Equals(Span other)
    {
        return TraceId == other.TraceId && SpanId == other.SpanId;
    }

    public override bool Equals(object? obj)
    {
        return obj is Span other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TraceId, SpanId);
    }

    public static bool operator ==(Span left, Span right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Span left, Span right)
    {
        return !left.Equals(right);
    }
}
