// <copyright file="ActivityKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Activity.Handlers;

internal readonly struct ActivityKey : IEquatable<ActivityKey>
{
    private readonly string _traceId;
    private readonly string _spanId;

    public ActivityKey(string traceId, string spanId)
    {
        _traceId = traceId;
        _spanId = spanId;
    }

    public ActivityKey(string id)
    {
        // Arbitrary which way around these are
        _spanId = id;
        _traceId = string.Empty;
    }

    public bool IsValid() => _traceId is not null && _spanId is not null;

    public bool Equals(ActivityKey other) =>
        string.Equals(_traceId, other._traceId, StringComparison.Ordinal) &&
        string.Equals(_spanId, other._spanId, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is ActivityKey other && Equals(other);

    public override int GetHashCode()
    {
        // TraceId and SpanId must be non-null, so can just just the values directly.
        // If we allow this to be called with any null values, then we must add null checks here
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(_traceId) * 397)
                 ^ StringComparer.Ordinal.GetHashCode(_spanId);
        }
    }
}
