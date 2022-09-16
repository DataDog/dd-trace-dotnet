// <copyright file="SpanIdLookup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Agent.MessagePack;

internal readonly struct SpanIdLookup
{
    // for small trace chunks, use the ArraySegment<Span> copy directly, no heap allocations
    private readonly ArraySegment<Span> _spans = default;

    // for large trace chunks, fall back to HashSet<ulong>
    private readonly HashSet<ulong> _hashSet = default;

    public SpanIdLookup(in ArraySegment<Span> spans)
    {
        if (spans.Count == 0)
        {
            // Contains() will always return false
            return;
        }

        _spans = spans;

        // for large trace chunks, fall back to HashSet<ulong>
        if (spans.Count > 50)
        {
#if NET472_OR_GREATER || NETCOREAPP2_0_OR_GREATER
            _hashSet = new HashSet<ulong>(spans.Count);
#else // NETFX < 4.7.2 || NETSTANDARD < 2.1
            _hashSet = new HashSet<ulong>();
#endif

            for (var i = 0; i < spans.Count; i++)
            {
                _hashSet.Add(spans.Array![i + spans.Offset].SpanId);
            }
        }
    }

    public bool Contains(ulong value, int startIndex = 0)
    {
        if (_spans.Count == 0)
        {
            return false;
        }

        // The local root span (if present) is the usually last span in the chunk
        // and it is the parent of other most span.
        // This or _spans.Array![_spans.Offset] would also be a shortcut for,
        // trace chunks with single spans, be we don't use SpanIdLookup at all for those.
        if (value == _spans.Array![_spans.Offset + _spans.Count - 1].SpanId)
        {
            return true;
        }

        // if we created a HashSet, use it
        if (_hashSet != null)
        {
            return _hashSet?.Contains(value) ?? false;
        }

        // If we didn't create a HashSet, iterate over the span array.
        // A span's parent usually closes after its child,
        // so start at the specified index then loop back to the beginning if needed.

        // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
        for (var i = startIndex; i < _spans.Count; i++)
        {
            if (value == _spans.Array![i + _spans.Offset].SpanId)
            {
                return true;
            }
        }

        for (var i = 0; i < startIndex; i++)
        {
            if (value == _spans.Array![i + _spans.Offset].SpanId)
            {
                return true;
            }
        }

        return false;
    }
}
