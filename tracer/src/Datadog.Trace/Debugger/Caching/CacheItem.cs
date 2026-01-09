// <copyright file="CacheItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;

namespace Datadog.Trace.Debugger.Caching;

internal sealed class CacheItem<TValue>
{
    private readonly DateTime _created;
    private long _lastAccessed;
    private long _accessCount;

    public CacheItem(TValue? value, TimeSpan? slidingExpiration)
    {
        if (slidingExpiration.HasValue && slidingExpiration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentException("Sliding expiration must be positive", nameof(slidingExpiration));
        }

        Value = value;
        _created = DateTime.UtcNow;
        LastAccessed = _created;
        SlidingExpiration = slidingExpiration;
    }

    public DateTimeOffset Created => _created;

    public TimeSpan? SlidingExpiration { get; set; }

    public TValue? Value { get; set; }

    internal DateTimeOffset LastAccessed
    {
        get => DateTime.FromBinary(Interlocked.Read(ref _lastAccessed));
        set => Interlocked.Exchange(ref _lastAccessed, value.UtcDateTime.ToBinary());
    }

    internal long AccessCount => Interlocked.Read(ref _accessCount);

    internal void UpdateAccess(DateTime now)
    {
        LastAccessed = now;
        Interlocked.Increment(ref _accessCount);
    }
}
