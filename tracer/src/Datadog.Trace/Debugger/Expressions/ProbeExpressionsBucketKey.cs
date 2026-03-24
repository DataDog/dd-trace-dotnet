// <copyright file="ProbeExpressionsBucketKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Debugger.Expressions;

internal readonly struct ProbeExpressionsBucketKey : IEquatable<ProbeExpressionsBucketKey>
{
    private readonly int _hashCode;

    public ProbeExpressionsBucketKey(Type thisType, Type? returnType, int memberCount)
    {
        ThisType = thisType;
        ReturnType = returnType;
        MemberCount = memberCount;
        _hashCode = HashCode.Combine(thisType, returnType, memberCount);
    }

    public Type ThisType { get; }

    public Type? ReturnType { get; }

    public int MemberCount { get; }

    public override int GetHashCode() => _hashCode;

    public bool Equals(ProbeExpressionsBucketKey other) =>
        _hashCode == other._hashCode &&
        ThisType == other.ThisType &&
        ReturnType == other.ReturnType &&
        MemberCount == other.MemberCount;

    public override bool Equals(object? obj) => obj is ProbeExpressionsBucketKey other && Equals(other);
}
