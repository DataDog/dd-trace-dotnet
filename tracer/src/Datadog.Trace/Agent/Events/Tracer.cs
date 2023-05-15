using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Events.Writers;

#nullable enable

namespace Datadog.Trace.Agent.Events;

public sealed class Tracer
{
    private static readonly string RuntimeId = Guid.NewGuid().ToString();

    private static readonly string Env = Environment.GetEnvironmentVariable("DD_ENV") ?? "";

    private readonly AsyncLocal<Span> _activeSpan = new();
    private readonly ArrayBufferWriter<SpanEvent> _events = new(); // CommunityToolkit.HighPerformance.Buffers.ArrayPoolBufferWriter<T>
    private readonly ConcurrentDictionary<Span, Span> _openSpans = new();
    private readonly ISpanEventWriter _writer;

    public Tracer(ISpanEventWriter writer)
    {
        _writer = writer;
    }

    public Span StartSpan(
        string name,
        string type,
        string service,
        string resource)
    {
        ulong traceId;
        ulong spanId = (ulong)Random.Shared.NextInt64();
        ulong parentId;
        Span parent = _activeSpan.Value;

        if (parent == Span.None)
        {
            traceId = (ulong)Random.Shared.NextInt64();
            parentId = 0;
        }
        else
        {
            traceId = parent.TraceId;
            parentId = parent.SpanId;
        }

        var span = new Span(this, traceId, spanId, parentId);
        PushSpan(span, parent);

        KeyValuePair<string, string>[] meta;
        KeyValuePair<string, double>[] metrics;

        if (parentId == 0)
        {
            meta = new KeyValuePair<string, string>[]
                   {
                       new("language", "dotnet"),
                       new("runtime-id", RuntimeId),
                       new("env", Env)
                   };

            metrics = new KeyValuePair<string, double>[]
                      {
                          new("_dd.tracer_kr", 0),
                          new("_dd.agent_psr", 1),
                          new("_sampling_priority_v1", 1),
                          new("_dd.top_level", 1),
                          new("process_id", Environment.ProcessId),
                      };
        }
        else
        {
            meta = new KeyValuePair<string, string>[]
                   {
                       new("language", "dotnet"),
                       new("env", Env)
                   };

            metrics = Array.Empty<KeyValuePair<string, double>>();
        }

        var spanEvent = new StartSpanEvent(
            traceId,
            spanId,
            parentId,
            DateTimeOffset.UtcNow,
            service,
            name,
            resource,
            type,
            meta,
            metrics);

        lock (_events)
        {
            var events = _events.GetSpan(1);
            events[0] = spanEvent;
            _events.Advance(1);
        }

        return span;
    }

    internal void FinishSpan(Span span)
    {
        PopSpan(span);

        var spanEvent = new FinishSpanEvent(
            span.TraceId,
            span.SpanId,
            DateTimeOffset.UtcNow,
            ReadOnlyMemory<KeyValuePair<string, string>>.Empty,
            ReadOnlyMemory<KeyValuePair<string, double>>.Empty
        );

        lock (_events)
        {
            var events = _events.GetSpan(1);
            events[0] = spanEvent;
            _events.Advance(1);
        }
    }

    internal void AddTags(Span span, ReadOnlyMemory<KeyValuePair<string, string>> tags)
    {
        var spanEvent = new AddTagsSpanEvent(
            span.TraceId,
            span.SpanId,
            tags,
            ReadOnlyMemory<KeyValuePair<string, double>>.Empty
        );

        lock (_events)
        {
            var events = _events.GetSpan(1);
            events[0] = spanEvent;
            _events.Advance(1);
        }
    }

    internal void AddTags(Span span, ReadOnlyMemory<KeyValuePair<string, double>> tags)
    {
        var spanEvent = new AddTagsSpanEvent(
            span.TraceId,
            span.SpanId,
            ReadOnlyMemory<KeyValuePair<string, string>>.Empty,
            tags
        );

        lock (_events)
        {
            var events = _events.GetSpan(1);
            events[0] = spanEvent;
            _events.Advance(1);
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        SpanEvent[] rentedArray;
        Memory<SpanEvent> memory;

        lock (_events)
        {
            int eventCount = _events.WrittenCount;
            rentedArray = ArrayPool<SpanEvent>.Shared.Rent(eventCount);
            memory = rentedArray.AsMemory(0, eventCount);
            _events.WrittenMemory.CopyTo(memory);
            _events.Clear();
        }

        await _writer.WriteAsync(memory, cancellationToken).ConfigureAwait(false);

        Array.Clear(rentedArray);
        ArrayPool<SpanEvent>.Shared.Return(rentedArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushSpan(Span span, Span parent)
    {
        _activeSpan.Value = span;
        _openSpans.TryAdd(span, parent);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopSpan(Span span)
    {
        if (_openSpans.TryRemove(span, out var parent))
        {
            // if the active span is finished, set its parent as the new active span.
            // if it didn't have a parent, then there is no active span now.
            if (_activeSpan.Value == span)
            {
                _activeSpan.Value = parent;
            }
        }
    }
}
