// <copyright file="CacheItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

namespace Datadog.Trace.Debugger.Caching;

internal class CacheItem<TValue>
{
    private long _lastAccessed;
    private long _accessCount;

    public CacheItem(TValue value, TimeSpan? slidingExpiration)
    {
        Value = value;
        Created = DateTime.UtcNow;
        LastAccessed = Created;
        SlidingExpiration = slidingExpiration;
    }

    public DateTimeOffset Created { get; set; }

    public TimeSpan? SlidingExpiration { get; set; }

    public TValue Value { get; set; }

    internal DateTimeOffset LastAccessed
    {
        get => DateTime.FromBinary(Interlocked.Read(ref _lastAccessed));
        set => Interlocked.Exchange(ref _lastAccessed, value.UtcDateTime.ToBinary());
    }

    internal long AccessCount => Interlocked.Read(ref _accessCount);

    internal void IncrementAccessCount() => Interlocked.Increment(ref _accessCount);
}
