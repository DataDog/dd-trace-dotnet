// <copyright file="StringSegment.NetCore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Datadog.Trace.Util;

[DebuggerDisplay("{ToString(),raw}")]
internal readonly ref partial struct StringSegment
{
    private readonly ReadOnlySpan<char> _span;
    private readonly int _start;

    public StringSegment(ReadOnlySpan<char> s)
    {
        _span = s;
        _start = 0;
    }

    public StringSegment(ReadOnlySpan<char> s, int start)
    {
        _span = s.Slice(start);
        _start = start;
    }

    public StringSegment(ReadOnlySpan<char> s, int start, int length)
    {
        _span = s.Slice(start, length);
        _start = start;
    }

    public int Length => _span.Length;

    public char this[int i] => _span[i];

    public char this[Index index] => _span[index];

    public StringSegment this[Range range] => _span[range];

    public static implicit operator StringSegment(string? value)
    {
        return new StringSegment(value.AsSpan());
    }

    public static implicit operator StringSegment(ReadOnlySpan<char> value)
    {
        return new StringSegment(value);
    }

    public static implicit operator ReadOnlySpan<char>(StringSegment value)
    {
        return value._span;
    }

    public static bool operator ==(StringSegment left, StringSegment right)
    {
        return left._span.Equals(right._span, StringComparison.Ordinal);
    }

    public static bool operator !=(StringSegment left, StringSegment right)
    {
        return !left._span.Equals(right._span, StringComparison.Ordinal);
    }

    public static StringSegment operator +(StringSegment left, StringSegment right)
    {
        var span = string.Concat(left._span, right._span).AsSpan();
        return new StringSegment(span);
    }

    public static int Compare(StringSegment a, StringSegment b, StringComparison comparison)
    {
        return a._span.CompareTo(b._span, comparison);
    }

    public static bool Equals(StringSegment a, StringSegment b, StringComparison comparison)
    {
        return a._span.Equals(b._span, comparison);
    }

    public bool Equals(StringSegment value, StringComparison comparison)
    {
        return Equals(this, value, comparison);
    }

    [DoesNotReturn]
    public override bool Equals(object? obj)
    {
        ThrowHelper.ThrowNotSupportedException("StringSegment.Equals(object) is not supported.");

        // unreachable code
        return default;
    }

    [DoesNotReturn]
    public override int GetHashCode()
    {
        ThrowHelper.ThrowNotSupportedException("StringSegment.GetHashCode(object) is not supported.");

        // unreachable code
        return default;
    }

    public override string ToString()
    {
        // NOTE: allocates new string
        return _span.ToString();
    }

    public ReadOnlySpan<char> AsSpan() => _span;

    public StringSegment Slice(int start)
    {
        return new StringSegment(_span.Slice(start));
    }

    public StringSegment Slice(int start, int length)
    {
        return new StringSegment(_span.Slice(start, length));
    }

    public StringSegment Trim()
    {
        int start = 0;

        for (; start < _span.Length; start++)
        {
            if (!char.IsWhiteSpace(_span[start]))
            {
                break;
            }
        }

        int end = _span.Length - 1;

        for (; end > start; end--)
        {
            if (!char.IsWhiteSpace(_span[end]))
            {
                break;
            }
        }

        int length = end - start + 1;
        return _span.Slice(start, length);
    }

    public void AppendTo(StringBuilder sb)
    {
        sb.Append(_span);
    }

    public bool StartsWith(StringSegment value, StringComparison comparison)
    {
        return _span.StartsWith(value._span, comparison);
    }

    public int IndexOf(char value)
    {
        return _span.IndexOf(value);
    }

    public int IndexOf(string value, StringComparison comparison)
    {
        return _span.IndexOf(value, comparison);
    }

    public Enumerator GetEnumerator() => new(this);
}

#endif
