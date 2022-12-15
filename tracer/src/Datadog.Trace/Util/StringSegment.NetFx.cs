// <copyright file="StringSegment.NetFx.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Datadog.Trace.Util;

[DebuggerDisplay("{ToString(),raw}")]
internal readonly ref partial struct StringSegment
{
    private readonly string? _string;

    private readonly int _start;

    public readonly int Length;

    public StringSegment()
        : this(string.Empty, 0, 0)
    {
    }

    public StringSegment(string? s)
        : this(s, 0, s?.Length ?? 0)
    {
    }

    public StringSegment(string? s, int start)
        : this(s, 0, s?.Length ?? 0 - start)
    {
    }

    public StringSegment(string? s, int start, int length)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _string = s ?? string.Empty;

        if (start + length > _string.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start), "Specified argument was out of the range of valid values.");
        }

        _start = start;
        Length = length;
    }

    public bool IsEmpty => _string!.Length == 0;

    public char this[int i] => (_string ?? string.Empty)[i];

    public static implicit operator StringSegment(string? value)
    {
        return new StringSegment(value);
    }

    public static bool operator ==(StringSegment left, StringSegment right)
    {
        return Equals(left, right, StringComparison.Ordinal);
    }

    public static bool operator !=(StringSegment left, StringSegment right)
    {
        return !Equals(left, right, StringComparison.Ordinal);
    }

    public static StringSegment operator +(StringSegment left, StringSegment right)
    {
        var sb = StringBuilderCache.Acquire(left.Length + right.Length);

        left.AppendTo(sb);
        right.AppendTo(sb);

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    public static int Compare(StringSegment left, StringSegment right, StringComparison comparison)
    {
        if (left._string == null)
        {
            // both are null (0), or only left is null (-1)
            return right._string == null ? 0 : -1;
        }

        if (right._string == null)
        {
            // only right is null
            return 1;
        }

        var minLength = Math.Min(left.Length, right.Length);
        var compare = string.Compare(left._string, left._start, right._string, right._start, minLength, comparison);

        if (compare == 0)
        {
            if (left.Length > right.Length)
            {
                // equal up to the shortest length, but left is longer
                return 1;
            }

            if (left.Length < right.Length)
            {
                // equal up to the shortest length, but right is longer
                return -1;
            }
        }

        return compare;
    }

    public static bool Equals(StringSegment left, StringSegment right, StringComparison comparison)
    {
        if (left.Length != right.Length)
        {
            // different lengths
            return false;
        }

        if (left._start == right._start && ReferenceEquals(left._string, right._string))
        {
            // same length (above), same start index, and same underlying string reference
            // (including both strings being null)
            return true;
        }

        if (left._string == null || right._string == null)
        {
            // we already checked if are null above,
            // check if only one of them is null
            return false;
        }

        return string.Compare(left._string, left._start, right._string, right._start, left.Length, comparison) == 0;
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
        return false;
    }

    public override int GetHashCode()
    {
        // NOTE: allocates new string
        return ToString().GetHashCode();
    }

    public override string ToString()
    {
        if (_string == null)
        {
            return string.Empty;
        }

        if (_start == 0 && Length == _string.Length)
        {
            return _string;
        }

        // NOTE: allocates new string
        return _string.Substring(_start, Length);
    }

    public StringSegment Slice(int start)
    {
        return new StringSegment(_string, _start + start);
    }

    public StringSegment Slice(int start, int length)
    {
        return new StringSegment(_string, _start + start, length);
    }

    public StringSegment Trim()
    {
        if (_string == null)
        {
            return string.Empty;
        }

        var start = 0;

        for (; start < _string.Length; start++)
        {
            if (!char.IsWhiteSpace(_string[start]))
            {
                break;
            }
        }

        var end = _string.Length - 1;

        for (; end > start; end--)
        {
            if (!char.IsWhiteSpace(_string[end]))
            {
                break;
            }
        }

        var length = end - start + 1;
        return new StringSegment(_string, start, length);
    }

    public unsafe void AppendTo(StringBuilder sb)
    {
        if (_string == null)
        {
            return;
        }

        if (_start == 0 && Length == _string.Length)
        {
            sb.Append(_string);
        }
        else
        {
            fixed (char* ptr = _string)
            {
                sb.Append(ptr + _start, Length);
            }
        }
    }

    public bool StartsWith(StringSegment value, StringComparison comparison)
    {
        if (value._string == null || value.Length == 0)
        {
            return true;
        }

        if (_string == null || Length < value.Length)
        {
            return false;
        }

        return string.Compare(_string, _start, value._string, value._start, value.Length, comparison) == 0;
    }

    public int IndexOf(char value)
    {
        return _string?.IndexOf(value) ?? -1;
    }

    public int IndexOf(string value, StringComparison comparison)
    {
        return _string?.IndexOf(value, comparison) ?? -1;
    }

    public Enumerator GetEnumerator() => new(this);
}

#endif
