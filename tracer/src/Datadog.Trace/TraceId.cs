// <copyright file="TraceId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace;

internal readonly struct TraceId : IEquatable<TraceId>
{
    public readonly ulong Lower;
    public readonly ulong Upper;

    /// <summary>Initializes a new instance of the <see cref="TraceId" /> struct.</summary>
    /// <param name="upper">The upper 64-bits of the 128-bit value.</param>
    /// <param name="lower">The lower 64-bits of the 128-bit value.</param>
    public TraceId(ulong upper, ulong lower)
    {
        Upper = upper;
        Lower = lower;
    }

    public static implicit operator TraceId(ulong lower) => new(0, lower);

    public static implicit operator TraceId(int lower) => new(0, (ulong)lower);

    public static bool operator ==(TraceId left, TraceId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TraceId left, TraceId right)
    {
        return !left.Equals(right);
    }

    [Pure]
    public bool Equals(TraceId value) => Lower == value.Lower && Upper == value.Upper;

    [Pure]
    public override bool Equals([NotNullWhen(true)] object? value) => value is TraceId other && Equals(other);

    [Pure]
    public override int GetHashCode() => HashCode.Combine(Lower, Upper);

    [Pure]
    public override string ToString()
    {
        return $"{HexString.ToHexString(Upper)}{HexString.ToHexString(Lower)}";
    }
}
