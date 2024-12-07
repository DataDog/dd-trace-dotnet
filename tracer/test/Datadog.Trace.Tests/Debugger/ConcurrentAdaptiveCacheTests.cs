// <copyright file="ConcurrentAdaptiveCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Caching;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ConcurrentAdaptiveCacheTests
    {
        [Fact]
        public void Add_And_TryGet_Should_Work_Correctly()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            cache.Add(42, keys: "answer");
            Assert.True(cache.TryGet("answer", out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void GetOrAdd_Should_Add_Item_If_Not_Present()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            int result = cache.GetOrAdd("key", k => 42);
            Assert.Equal(42, result);

            Assert.True(cache.TryGet("key", out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void GetOrAdd_Should_Return_Existing_Item_If_Present()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            cache.Add(42, keys: "key");
            int result = cache.GetOrAdd("key", k => 100);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Cache_Should_Evict_Items_When_Capacity_Is_Reached()
        {
            var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 3, evictionPolicyKind: EvictionPolicy.LRU);

            cache.Add("one", keys: 1);
            cache.Add("two", keys: 2);
            cache.Add("three", keys: 3);

            // This should evict the least recently used item (1)
            cache.Add("four", keys: 4);

            Assert.False(cache.TryGet(1, out _));
            Assert.True(cache.TryGet(2, out _));
            Assert.True(cache.TryGet(3, out _));
            Assert.True(cache.TryGet(4, out _));
        }

        [Fact]
        public void Cache_Should_Use_LRU_Eviction_Policy_Correctly()
        {
            var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 3, evictionPolicyKind: EvictionPolicy.LRU);

            cache.Add("one", keys: 1);
            cache.Add("two", keys: 2);
            cache.Add("three", keys: 3);

            // Access item 1, making it the most recently used
            cache.TryGet(1, out _);

            // This should evict item 2 (least recently used)
            cache.Add("four", keys: 4);

            Assert.True(cache.TryGet(1, out _));
            Assert.False(cache.TryGet(2, out _));
            Assert.True(cache.TryGet(3, out _));
            Assert.True(cache.TryGet(4, out _));
        }

        [Fact]
        public void Cache_Should_Use_LFU_Eviction_Policy_Correctly()
        {
            var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 3, evictionPolicyKind: EvictionPolicy.LFU);

            cache.Add("one", keys: 1);
            cache.Add("two", keys: 2);
            cache.Add("three", keys: 3);

            // Access item 1 twice, making it the most frequently used
            cache.TryGet(1, out _);
            cache.TryGet(1, out _);

            // Access item 2 once
            cache.TryGet(2, out _);

            // This should evict item 3 (least frequently used)
            cache.Add("four", keys: 4);

            Assert.True(cache.TryGet(1, out _));
            Assert.True(cache.TryGet(2, out _));
            Assert.False(cache.TryGet(3, out _));
            Assert.True(cache.TryGet(4, out _));
        }

        [Fact]
        public void Cache_Should_Handle_Sliding_Expiration()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);
            var shortExpiration = TimeSpan.FromMilliseconds(50);

            cache.Add(42, shortExpiration, keys: "key");

            // Item should still be in cache
            Assert.True(cache.TryGet("key", out int value));
            Assert.Equal(42, value);

            // Wait for the item to expire
            Thread.Sleep(100);

            // Item should have been removed due to expiration
            Assert.False(cache.TryGet("key", out _));
        }

        [Fact]
        public void Cache_Should_Adapt_To_Low_Resource_Environment()
        {
            var mockMemoryChecker = new Mock<IMemoryChecker>();
            mockMemoryChecker.Setup(m => m.IsLowResourceEnvironment()).Returns(true);

            var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: null,
                memoryChecker: mockMemoryChecker.Object);

            // Add more items than the default low resource capacity
            for (int i = 0; i < 1000; i++)
            {
                cache.Add(i, keys: $"key{i}");
            }

            // Check that the cache size is limited to the low resource capacity
            Assert.Equal(512, cache.Count);
        }

        [Fact]
        public void Cache_Should_Be_Thread_Safe()
        {
            var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 1000);

            Parallel.For(0, 1000, i =>
            {
                cache.Add($"value{i}", keys: i);
            });

            Parallel.For(0, 1000, i =>
            {
                Assert.True(cache.TryGet(i, out string value));
                Assert.Equal($"value{i}", value);
            });

            Assert.Equal(1000, cache.Count);
        }

        [Fact]
        public void Cache_Should_Calculate_Hit_Rate_Correctly()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            cache.Add(42, keys: "hit");
            cache.TryGet("hit", out _);
            cache.TryGet("miss", out _);

            Assert.Equal(0.5, cache.HitRate);
        }

        [Fact]
        public void Dispose_Should_Clean_Up_Resources()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);
            cache.Add(42, keys: "key");

            cache.Dispose();

            Assert.Throws<ObjectDisposedException>(() => cache.Add(43, keys: "new_key"));
            Assert.Throws<ObjectDisposedException>(() => cache.TryGet("key", out _));
        }

        [Fact]
        public async Task Adaptive_Cleanup_Should_Adjust_Interval_Based_On_Expired_Items()
        {
            var mockMemoryChecker = new Mock<IMemoryChecker>();
            mockMemoryChecker.Setup(m => m.IsLowResourceEnvironment()).Returns(false);

            var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 100,
                memoryChecker: mockMemoryChecker.Object);

            // Add items with short expiration
            for (int i = 0; i < 50; i++)
            {
                cache.Add(i, TimeSpan.FromMilliseconds(50), keys: $"key{i}");
            }

            // Wait for items to expire
            await Task.Delay(100);

            // Manually trigger cleanup
            int itemsRemoved = cache.PerformCleanup();
            Assert.True(itemsRemoved > 0);

            // Check that cleanup interval decreased
            var initialInterval = cache.CurrentCleanupInterval;
            cache.AdjustCleanupInterval(itemsRemoved);
            Assert.True(cache.CurrentCleanupInterval < initialInterval);

            // Add more items with longer expiration
            for (int i = 50; i < 100; i++)
            {
                cache.Add(i, TimeSpan.FromSeconds(10), keys: $"key{i}");
            }

            // Manually trigger cleanup again
            itemsRemoved = cache.PerformCleanup();
            Assert.True(itemsRemoved == 0);

            // Check that cleanup interval increased
            initialInterval = cache.CurrentCleanupInterval;
            cache.AdjustCleanupInterval(itemsRemoved);
            Assert.True(cache.CurrentCleanupInterval > initialInterval);
        }

        [Fact]
        public void Adaptive_Cleanup_Should_Respect_Min_And_Max_Intervals()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 100);

            // Simulate many cleanups with no items removed
            for (int i = 0; i < 100; i++)
            {
                cache.AdjustCleanupInterval(0);
            }

            Assert.True(cache.CurrentCleanupInterval.TotalSeconds <= ConcurrentAdaptiveCache<string, int>.MaxCleanupIntervalSeconds);

            // Simulate many cleanups with items removed
            for (int i = 0; i < 100; i++)
            {
                cache.AdjustCleanupInterval(10);
            }

            Assert.True(cache.CurrentCleanupInterval.TotalSeconds >= ConcurrentAdaptiveCache<string, int>.MinCleanupIntervalSeconds);
        }

        [Fact]
        public async Task AdaptiveCleanupAsync_Should_Continuously_Adjust_Interval()
        {
            var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 100);
            var initialInterval = cache.CurrentCleanupInterval;

            // Start the cleanup task
            var cleanupTask = cache.AdaptiveCleanupAsync();

            // Add items with varying expiration times
            for (int i = 0; i < 50; i++)
            {
                cache.Add(i, TimeSpan.FromMilliseconds(50), keys: $"shortKey{i}");
            }
            for (int i = 50; i < 100; i++)
            {
                cache.Add(i, TimeSpan.FromSeconds(10), keys: $"longKey{i}");
            }

            // Wait for some cleanups to occur
            await Task.Delay(TimeSpan.FromSeconds(15));

            // Check that the cleanup interval has changed
            Assert.NotEqual(initialInterval, cache.CurrentCleanupInterval);

            // Check that short-lived items have been removed
            for (int i = 0; i < 50; i++)
            {
                Assert.False(cache.TryGet($"shortKey{i}", out _));
            }

            // Check that long-lived items are still present
            for (int i = 50; i < 100; i++)
            {
                Assert.True(cache.TryGet($"longKey{i}", out _));
            }
        }
    }
}
