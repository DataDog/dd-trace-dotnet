// <copyright file="TraceId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.Contracts;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace;

internal readonly record struct TraceId(ulong Upper, ulong Lower)
{
    public const int Size = sizeof(ulong) * 2;

    public readonly ulong Lower = Lower;
    public readonly ulong Upper = Upper;

    public static implicit operator TraceId(ulong lower) => new(0, lower);

    public static implicit operator TraceId(int lower) => new(0, (ulong)lower);

    [Pure]
    public bool Equals(TraceId value) => Lower == value.Lower && Upper == value.Upper;

    [Pure]
    public override int GetHashCode() => HashCode.Combine(Lower, Upper);

    [Pure]
    public override string ToString()
    {
        return HexString.ToHexString(this, pad16To32: true, lowerCase: true);
    }
}
