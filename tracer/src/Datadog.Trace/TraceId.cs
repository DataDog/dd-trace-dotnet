// <copyright file="TraceId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.Contracts;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace;

internal readonly record struct TraceId(ulong Upper, ulong Lower) : IComparable<TraceId>, IComparable
{
    public const int Size = sizeof(ulong) * 2;

    public static readonly TraceId Zero = new(0, 0);

    public readonly ulong Upper = Upper;
    public readonly ulong Lower = Lower;

    public static explicit operator TraceId(ulong lower) => new(0, lower);

    public static explicit operator TraceId(long lower) => new(0, (ulong)lower);

    public static explicit operator TraceId(uint lower) => new(0, lower);

    public static explicit operator TraceId(int lower) => new(0, (ulong)lower);

    public static bool operator <(TraceId left, TraceId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(TraceId left, TraceId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(TraceId left, TraceId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(TraceId left, TraceId right)
    {
        return left.CompareTo(right) >= 0;
    }

    [Pure]
    public bool Equals(TraceId value) => Lower == value.Lower && Upper == value.Upper;

    [Pure]
    public int CompareTo(TraceId other)
    {
        var upperComparison = Upper.CompareTo(other.Upper);

        if (upperComparison != 0)
        {
            return upperComparison;
        }

        return Lower.CompareTo(other.Lower);
    }

    [Pure]
    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return 1;
        }

        if (obj is TraceId other)
        {
            return CompareTo(other);
        }

        ThrowHelper.ThrowArgumentException($"Object must be of type {nameof(TraceId)}", nameof(obj));
        return 0; // unreachable code
    }

    [Pure]
    public override int GetHashCode() => HashCode.Combine(Lower, Upper);

    [Pure]
    public override string ToString()
    {
        return this == Zero ?
                   "00000000000000000000000000000000" :
                   HexString.ToHexString(this, pad16To32: true, lowerCase: true);
    }
}
