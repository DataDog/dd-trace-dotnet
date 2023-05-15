using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.Vendors.MessagePack;

#nullable enable

namespace Datadog.Trace.Agent.Events.Serializers;

internal ref struct MessagePackWriterHelper
{
    private MessagePackWriter _writer;
    private readonly StringCache _stringCache;

    public MessagePackWriterHelper(in MessagePackWriter writer, StringCache stringCache)
    {
        _writer = writer;
        _stringCache = stringCache;
    }

    public void Write(StartSpanEvent e)
    {
        // 2 top-level items: event type and additional fields
        _writer.WriteArrayHeader(2);

        // write event type
        Write((byte)SpanEventType.StartSpan);

        // start array for additional fields
        _writer.WriteArrayHeader(10);

        // start time
        Write(e.Timestamp);

        Write(e.TraceId);
        Write(e.SpanId);
        Write(e.ParentId);
        Write(e.Service);
        Write(e.Name);
        Write(e.Resource);
        Write(e.Meta);
        Write(e.Metrics);
        Write(e.Type);
    }

    public void Write(FinishSpanEvent e)
    {
        // 2 top-level items: event type and additional fields
        _writer.WriteArrayHeader(2);

        // write event type
        Write((byte)SpanEventType.FinishSpan);

        // start array for additional fields
        _writer.WriteArrayHeader(5);

        // end time
        Write(e.Timestamp);

        Write(e.TraceId);
        Write(e.SpanId);
        Write(e.Meta);
        Write(e.Metrics);
    }

    public void Write(AddTagsSpanEvent e)
    {
        // 2 top-level items: event type and additional fields
        _writer.WriteArrayHeader(2);

        // write event type
        Write((byte)SpanEventType.AddSpanTags);

        // start array for additional fields
        _writer.WriteArrayHeader(4);

        Write(0); // duration, not used yet
        Write(e.TraceId);
        Write(e.SpanId);
        Write(e.Meta);
        Write(e.Metrics);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ReadOnlyMemory<KeyValuePair<string, string>> tags)
    {
        if (tags.Length == 0)
        {
            _writer.WriteMapHeader(0);
            return;
        }

        int count = 0;
        var tagsSpan = tags.Span;

        foreach (var tag in tagsSpan)
        {
            if (!string.IsNullOrEmpty(tag.Value))
            {
                count++;
            }
        }

        _writer.WriteMapHeader(count);

        foreach ((string key, string value) in tagsSpan)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Write(key);
                Write(value);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ReadOnlyMemory<KeyValuePair<string, double>> tags)
    {
        if (tags.Length == 0)
        {
            _writer.WriteMapHeader(0);
            return;
        }

        _writer.WriteMapHeader(tags.Length);

        foreach ((string key, double value) in tags.Span)
        {
            Write(key);
            Write(value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(DateTimeOffset value)
    {
        const long nanoSecondsPerTick = 1_000_000 / TimeSpan.TicksPerMillisecond;
        const long unixEpochInTicks = 621355968000000000; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks

        long nanoseconds = (value.Ticks - unixEpochInTicks) * nanoSecondsPerTick;
        Write(nanoseconds);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(string? value)
    {
        int stringIndex = _stringCache.TryAdd(value ?? "");
        _writer.Write(stringIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ulong value)
    {
        _writer.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(long value)
    {
        _writer.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(byte value)
    {
        _writer.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(double value)
    {
        _writer.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteArrayHeader(int count)
    {
        _writer.WriteArrayHeader(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteMapHeader(int count)
    {
        _writer.WriteMapHeader(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        _writer.Flush();
    }
}
