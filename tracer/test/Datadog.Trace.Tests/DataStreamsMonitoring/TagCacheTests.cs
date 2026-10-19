// <copyright file="TagCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class TagCacheTests
{
    [Fact]
    public void FullCache_PreservesActiveEntries_WhenWorkingSetExceedsCapacity()
    {
        long now = 0;
        var cache = new TagCache<int, object>(GlobalSettings.DefaultDataStreamsTagCacheSize, () => now);
        var results = new object[1200];
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = GetTags(cache, i);
        }

        // Keep cycling for longer than the idle timeout. The first 1000 keys must stay cached.
        for (var cycle = 0; cycle < 3; cycle++)
        {
            now += TagCache<int, object>.IdleTimeoutSeconds - 1;
            for (var i = 0; i < results.Length; i++)
            {
                var tags = GetTags(cache, i);
                if (i < GlobalSettings.DefaultDataStreamsTagCacheSize)
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

    [Fact]
    public void FullCache_ReplacesOnlyIdleEntries_AndCachesNewKey()
    {
        long now = 0;
        var cache = new TagCache<int, object>(3, () => now);
        var first = GetTags(cache, 0);
        now = 1;
        var second = GetTags(cache, 1);
        now = 2;
        var third = GetTags(cache, 2);

        now = 10;
        GetTags(cache, 0).Should().BeSameAs(first);

        now = 61;
        var replacement = GetTags(cache, 3);
        GetTags(cache, 3).Should().BeSameAs(replacement);
        GetTags(cache, 0).Should().BeSameAs(first);
        GetTags(cache, 2).Should().BeSameAs(third);
        GetTags(cache, 1).Should().NotBeSameAs(second);

        // Once older keys become idle too, a previously evicted key can be cached again.
        now += TagCache<int, object>.IdleTimeoutSeconds;
        GetTags(cache, 3).Should().BeSameAs(replacement);
        var recreated = GetTags(cache, 1);
        GetTags(cache, 1).Should().BeSameAs(recreated);
    }

    [Fact]
    public void ConcurrentInsertionsAndIdleEvictions_KeepCapacityAvailable()
    {
        long now = 0;
        var cache = new TagCache<int, object>(GlobalSettings.DefaultDataStreamsTagCacheSize, () => Volatile.Read(ref now));
        var results = new object[GlobalSettings.DefaultDataStreamsTagCacheSize];
        Parallel.For(0, results.Length, i => results[i] = GetTags(cache, i));
        for (var i = 0; i < results.Length; i++)
        {
            GetTags(cache, i).Should().BeSameAs(results[i]);
        }

        for (var generation = 1; generation <= 3; generation++)
        {
            Interlocked.Add(ref now, TagCache<int, object>.IdleTimeoutSeconds);
            var offset = generation * results.Length;
            Parallel.For(0, results.Length, i => results[i] = GetTags(cache, offset + i));
            for (var i = 0; i < results.Length; i++)
            {
                GetTags(cache, offset + i).Should().BeSameAs(results[i]);
            }

            // Every slot is now active, so another key cannot evict any of them.
            var uncached = GetTags(cache, -1);
            GetTags(cache, -1).Should().NotBeSameAs(uncached);
        }
    }

    [Fact]
    public void FullCache_EvictsEveryIdleEntryInOnePass()
    {
        long now = 0;
        var cache = new TagCache<int, object>(3, () => now);
        var first = GetTags(cache, 0);
        GetTags(cache, 1);
        GetTags(cache, 2);

        now = 10;
        GetTags(cache, 0).Should().BeSameAs(first);

        // Keys 1 and 2 are idle, so the first miss frees both slots and the next new key also gets cached.
        now = TagCache<int, object>.IdleTimeoutSeconds;
        var replacement1 = GetTags(cache, 3);
        var replacement2 = GetTags(cache, 4);

        GetTags(cache, 3).Should().BeSameAs(replacement1);
        GetTags(cache, 4).Should().BeSameAs(replacement2);
        GetTags(cache, 0).Should().BeSameAs(first);

        // The cache is full of active entries again, so another key bypasses it.
        var uncached = GetTags(cache, 5);
        GetTags(cache, 5).Should().NotBeSameAs(uncached);
    }

    [Fact]
    public void ConcurrentRequestsForSameKey_ReturnSameInstance()
    {
        var cache = new TagCache<int, object>(1);
        var results = new object[1000];
        Parallel.For(0, results.Length, i => results[i] = GetTags(cache, 0));
        results.Should().OnlyContain(value => ReferenceEquals(value, results[0]));
    }

    [Fact]
    public void FactoryFailure_DoesNotEvictExistingEntry()
    {
        long now = 0;
        var cache = new TagCache<int, object>(1, () => now);
        var existing = GetTags(cache, 0);
        now = TagCache<int, object>.IdleTimeoutSeconds;

        Action create = () => cache.GetOrCreate(1, false, static (_, _) => throw new InvalidOperationException());
        create.Should().Throw<InvalidOperationException>();
        GetTags(cache, 0).Should().BeSameAs(existing);
    }

    private static object GetTags(TagCache<int, object> cache, int key)
        => cache.GetOrCreate(key, false, static (k, _) => new EdgeTags([$"tag:{k}"]));
}
