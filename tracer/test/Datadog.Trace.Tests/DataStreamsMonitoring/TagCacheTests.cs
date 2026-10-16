// <copyright file="TagCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class TagCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullCache_PreservesActiveEntries_WhenWorkingSetExceedsCapacity(bool backlog)
    {
        long now = 0;
        var cache = new TagCache<int, object>(DataStreamsManager.MaxEdgeTagCacheSize, () => now);
        var results = new object[1200];
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = GetTags(cache, i, backlog);
        }

        // Keep cycling for longer than the idle timeout. The first 1000 keys must stay cached.
        for (var cycle = 0; cycle < 3; cycle++)
        {
            now += TagCache<int, object>.IdleTimeoutSeconds - 1;
            for (var i = 0; i < results.Length; i++)
            {
                var tags = GetTags(cache, i, backlog);
                if (i < DataStreamsManager.MaxEdgeTagCacheSize)
                {
                    tags.Should().BeSameAs(results[i]);
                }
                else
                {
                    tags.Should().NotBeSameAs(results[i]);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullCache_ReplacesLeastRecentlyUsedIdleEntry_AndCachesNewKey(bool backlog)
    {
        long now = 0;
        var cache = new TagCache<int, object>(3, () => now);
        var first = GetTags(cache, 0, backlog);
        now = 1;
        var second = GetTags(cache, 1, backlog);
        now = 2;
        var third = GetTags(cache, 2, backlog);

        now = 10;
        GetTags(cache, 0, backlog).Should().BeSameAs(first);

        now = 61;
        var replacement = GetTags(cache, 3, backlog);
        GetTags(cache, 3, backlog).Should().BeSameAs(replacement);
        GetTags(cache, 0, backlog).Should().BeSameAs(first);
        GetTags(cache, 2, backlog).Should().BeSameAs(third);
        GetTags(cache, 1, backlog).Should().NotBeSameAs(second);

        // Once older keys become idle too, a previously evicted key can be cached again.
        now += TagCache<int, object>.IdleTimeoutSeconds;
        GetTags(cache, 3, backlog).Should().BeSameAs(replacement);
        var recreated = GetTags(cache, 1, backlog);
        GetTags(cache, 1, backlog).Should().BeSameAs(recreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcurrentInsertionsAndIdleEvictions_KeepCapacityAvailable(bool backlog)
    {
        long now = 0;
        var cache = new TagCache<int, object>(DataStreamsManager.MaxEdgeTagCacheSize, () => Volatile.Read(ref now));
        var results = new object[DataStreamsManager.MaxEdgeTagCacheSize];
        Parallel.For(0, results.Length, i => results[i] = GetTags(cache, i, backlog));
        for (var i = 0; i < results.Length; i++)
        {
            GetTags(cache, i, backlog).Should().BeSameAs(results[i]);
        }

        for (var generation = 1; generation <= 3; generation++)
        {
            Interlocked.Add(ref now, TagCache<int, object>.IdleTimeoutSeconds);
            var offset = generation * results.Length;
            Parallel.For(0, results.Length, i => results[i] = GetTags(cache, offset + i, backlog));
            for (var i = 0; i < results.Length; i++)
            {
                GetTags(cache, offset + i, backlog).Should().BeSameAs(results[i]);
            }

            // Every slot is now active, so another key cannot evict any of them.
            var uncached = GetTags(cache, -1, backlog);
            GetTags(cache, -1, backlog).Should().NotBeSameAs(uncached);
        }
    }

    [Fact]
    public void ConcurrentRequestsForSameKey_ReturnSameInstance()
    {
        var cache = new TagCache<int, object>(1);
        var results = new object[1000];
        Parallel.For(0, results.Length, i => results[i] = GetTags(cache, 0, backlog: false));
        results.Should().OnlyContain(value => ReferenceEquals(value, results[0]));
    }

    [Fact]
    public void FactoryFailure_DoesNotEvictExistingEntry()
    {
        long now = 0;
        var cache = new TagCache<int, object>(1, () => now);
        var existing = GetTags(cache, 0, backlog: false);
        now = TagCache<int, object>.IdleTimeoutSeconds;

        Action create = () => cache.GetOrCreate(1, false, static (_, _) => throw new InvalidOperationException());
        create.Should().Throw<InvalidOperationException>();
        GetTags(cache, 0, backlog: false).Should().BeSameAs(existing);
    }

    private static object GetTags(TagCache<int, object> cache, int key, bool backlog)
        => cache.GetOrCreate(key, backlog, static (k, b) => b ? $"tag:{k}" : new EdgeTags([$"tag:{k}"]));
}
