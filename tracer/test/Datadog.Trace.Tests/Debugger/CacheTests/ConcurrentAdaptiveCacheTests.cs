// <copyright file="ConcurrentAdaptiveCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Caching;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.CacheTests
{
    public class ConcurrentAdaptiveCacheTests
    {
        [Fact]
        public void Add_And_TryGet_Should_Work_Correctly()
        {
            using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            cache.Add(42, keys: "answer");
            Assert.True(cache.TryGet("answer", out var value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void GetOrAdd_Should_Add_Item_If_Not_Present()
        {
            using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            var result = cache.GetOrAdd("key", _ => 42);
            Assert.Equal(42, result);

            Assert.True(cache.TryGet("key", out var value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void GetOrAdd_Should_Return_Existing_Item_If_Present()
        {
            using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

            cache.Add(42, keys: "key");
            int result = cache.GetOrAdd("key", _ => 100);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Cache_Should_Evict_Items_When_Capacity_Is_Reached()
        {
            using var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 3, evictionPolicyKind: EvictionPolicy.Lru);

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
        public void Cache_Should_Use_Lru_Eviction_Policy_Correctly()
        {
            using var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 3, evictionPolicyKind: EvictionPolicy.Lru);

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
        public void Cache_Should_Use_Lfu_Eviction_Policy_Correctly()
        {
            using var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 3, evictionPolicyKind: EvictionPolicy.Lfu);

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
            // Setup mock time provider
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => currentTime);

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 10,
                timeProvider: mockTimeProvider.Object);

            var shortExpiration = TimeSpan.FromMilliseconds(50);

            cache.Add(42, shortExpiration, keys: "key");

            // Item should still be in cache
            Assert.True(cache.TryGet("key", out int value));
            Assert.Equal(42, value);

            // Wait for the item to expire
            currentTime = currentTime.AddMilliseconds(100);
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(currentTime);

            cache.PerformCleanup();

            // Item should have been removed due to expiration
            Assert.False(cache.TryGet("key", out _));
        }

        [Fact]
        public void Cache_Should_Use_Default_Sliding_Expiration_When_None_Provided()
        {
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => currentTime);

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 10,
                timeProvider: mockTimeProvider.Object);

            cache.Add(42, slidingExpiration: null, keys: "key");
            Assert.True(cache.TryGet("key", out int value));
            Assert.Equal(42, value);

            // Advance time past default expiration (60 minutes)
            currentTime = currentTime.AddHours(1).AddSeconds(1);
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(currentTime);

            cache.PerformCleanup();
            Assert.False(cache.TryGet("key", out _));
        }

        [Fact]
        public void Cache_Should_Adapt_To_Low_Resource_Environment()
        {
            var mockMemoryChecker = new Mock<IMemoryChecker>();
            mockMemoryChecker.Setup(m => m.IsLowResourceEnvironment).Returns(true);

            using var cache = new ConcurrentAdaptiveCache<string, int>(
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
        public void Cache_Should_Use_Restricted_Capacity_When_Environment_Check_Fails()
        {
            var mockEnvironmentChecker = new Mock<IEnvironmentChecker>();
            mockEnvironmentChecker
               .Setup(m => m.IsServerlessEnvironment)
               .Throws(new Exception("Simulated environment check failure"));

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                environmentChecker: mockEnvironmentChecker.Object);

            // Add more items than the restricted environment capacity
            for (int i = 0; i < 2000; i++)
            {
                cache.Add(i, keys: $"key{i}");
            }

            // Should be limited to RestrictedEnvironmentCapacity (1024)
            Assert.Equal(1024, cache.Count);
        }

        [Fact]
        public async Task Cache_Should_Be_Thread_Safe()
        {
            using var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 1000);

            var addTask = Task.Run(
                () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        cache.Add($"value{i}", keys: i);
                    }
                });

            var readTask = Task.Run(
                async () =>
                {
                    await addTask; // Wait for adds to complete
                    for (int i = 0; i < 1000; i++)
                    {
                        Assert.True(cache.TryGet(i, out var value));
                        Assert.Equal($"value{i}", value);
                    }
                });

            await Task.WhenAll(addTask, readTask);
            Assert.Equal(1000, cache.Count);
        }

        [Fact]
        public void Cache_Should_Calculate_Hit_Rate_Correctly()
        {
            using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 10);

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
        public void Adaptive_Cleanup_Should_Respect_Min_And_Max_Intervals()
        {
            using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 100);
            const int iterationCount = 100;

            // Simulate many cleanups with no items removed
            for (int i = 0; i < iterationCount; i++)
            {
                cache.AdjustCleanupInterval(0);
            }

            Assert.True(
                cache.CurrentCleanupInterval.TotalSeconds <= ConcurrentAdaptiveCache<string, int>.MaxCleanupIntervalSeconds,
                $"Cleanup interval {cache.CurrentCleanupInterval.TotalSeconds}s exceeds maximum {ConcurrentAdaptiveCache<string, int>.MaxCleanupIntervalSeconds}s");

            // Simulate many cleanups with items removed
            for (int i = 0; i < iterationCount; i++)
            {
                cache.AdjustCleanupInterval(10);
            }

            Assert.True(
                cache.CurrentCleanupInterval.TotalSeconds >= ConcurrentAdaptiveCache<string, int>.MinCleanupIntervalSeconds,
                $"Cleanup interval {cache.CurrentCleanupInterval.TotalSeconds}s is below minimum {ConcurrentAdaptiveCache<string, int>.MinCleanupIntervalSeconds}s");
        }

        [Fact]
        [Flaky("Identified as flaky in error tracking. Marked as flaky until solved.")]
        public async Task AdaptiveCleanupAsync_Should_Handle_Cancellation_Gracefully()
        {
            // Setup
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            var cleanupStarted = new TaskCompletionSource<bool>();
            var delayStarted = new TaskCompletionSource<bool>();
            var delayCompleted = new TaskCompletionSource<bool>();

            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => currentTime);

            mockTimeProvider
               .Setup(tp => tp.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
               .Returns<TimeSpan, CancellationToken>(async (delay, token) =>
                {
                    delayStarted.SetResult(true);
                    cleanupStarted.SetResult(true);

                    try
                    {
                        // Wait until explicitly completed or cancelled
                        await Task.Delay(-1, token);
                        delayCompleted.SetResult(true);
                    }
                    catch (OperationCanceledException)
                    {
                        delayCompleted.SetResult(true);
                        throw;
                    }
                });

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 100,
                timeProvider: mockTimeProvider.Object);

            // Wait for cleanup cycle to start
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            // Add some test data to ensure the cache is being used
            cache.Add(1, TimeSpan.FromSeconds(1), "test");

            // Initiate disposal - this should trigger cancellation
            await Task.Run(() => cache.Dispose());

            // Wait for delay completion with timeout
            await delayCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            // Verify cleanup task was cancelled and cache is disposed
            Assert.Throws<ObjectDisposedException>(() => cache.Add(2, keys: "test"));
        }

        [Fact]
        public async Task Concurrent_Add_And_Get_With_Eviction_Should_Maintain_Consistency()
        {
            using var cache = new ConcurrentAdaptiveCache<int, string>(capacity: 100);
            var concurrentTasks = new List<Task>();

            // Task to add items
            concurrentTasks.Add(
                Task.Run(() =>
                {
                    for (int i = 0; i < 200; i++)
                    {
                        cache.Add($"value{i}", TimeSpan.FromMilliseconds(50), i);
                    }
                }));

            // Task to read items and verify values when found
            concurrentTasks.Add(
                Task.Run(() =>
                {
                    for (int i = 0; i < 200; i++)
                    {
                        if (cache.TryGet(i, out var value))
                        {
                            Assert.Equal($"value{i}", value);
                        }
                    }
                }));

            // Task to perform cleanups
            concurrentTasks.Add(
                Task.Run(async () =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        cache.PerformCleanup();
                        await Task.Delay(10);
                    }
                }));

            await Task.WhenAll(concurrentTasks);

            // Verify final state
            Assert.True(cache.Count <= 100, $"Cache count {cache.Count} exceeds capacity 100");
            Assert.True(cache.HitRate is >= 0 and <= 1, $"Invalid hit rate: {cache.HitRate}");
        }

        [Fact]
        public void Cache_Should_Handle_Cleanup_Interval_Boundaries()
        {
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => currentTime);

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 10,
                timeProvider: mockTimeProvider.Object);

            // Test minimum interval boundary
            for (int i = 0; i < 100; i++)
            {
                cache.AdjustCleanupInterval(100); // Many items removed
                Assert.True(cache.CurrentCleanupInterval.TotalSeconds >=
                    ConcurrentAdaptiveCache<string, int>.MinCleanupIntervalSeconds);
            }

            // Test maximum interval boundary
            for (int i = 0; i < 100; i++)
            {
                cache.AdjustCleanupInterval(0); // No items removed
                Assert.True(cache.CurrentCleanupInterval.TotalSeconds <=
                    ConcurrentAdaptiveCache<string, int>.MaxCleanupIntervalSeconds);
            }

            // Verify interval adjustments are symmetric
            var interval = cache.CurrentCleanupInterval;
            cache.AdjustCleanupInterval(50);
            var newInterval = cache.CurrentCleanupInterval;
            Assert.NotEqual(interval, newInterval);
        }

        [Fact]
        public async Task Adaptive_Cleanup_Should_Adjust_Interval_Based_On_Expired_Items()
        {
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            var timeUpdated = new TaskCompletionSource<bool>();

            mockTimeProvider.Setup(tp => tp.UtcNow)
                            .Returns(() => currentTime)
                            .Callback(() => timeUpdated.TrySetResult(true));

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 100,
                timeProvider: mockTimeProvider.Object);

            // Add items expiration
            var expiration = TimeSpan.FromMilliseconds(1000);
            cache.Add(42, expiration, "test-key");

            // Verify item is present
            Assert.True(cache.TryGet("test-key", out var value));
            Assert.Equal(42, value);

            // Advance time beyond expiration
            currentTime = currentTime.Add(expiration.Add(TimeSpan.FromMilliseconds(1500)));

            await timeUpdated.Task.WaitAsync(TimeSpan.FromMilliseconds(250));

            cache.PerformCleanup();

            // Verify item was actually removed
            Assert.False(cache.TryGet("test-key", out _));
        }

        [Fact]
        public async Task AdaptiveCleanupAsync_Should_Handle_Lifecycle_Correctly()
        {
            // Setup
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => currentTime);

            var delayTask = new TaskCompletionSource<bool>();
            var cleanupStarted = new TaskCompletionSource<bool>();

            mockTimeProvider
                .Setup(tp => tp.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns<TimeSpan, CancellationToken>((_, token) =>
                {
                    cleanupStarted.SetResult(true);
                    return delayTask.Task;
                });

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 100,
                timeProvider: mockTimeProvider.Object,
                maxErrors: 1);

            // Wait for first cleanup cycle to start
            await cleanupStarted.Task;

            // Force cleanup error
            mockTimeProvider
                .Setup(tp => tp.UtcNow)
                .Throws(new InvalidOperationException("Simulated failure"));

            // Complete delay to trigger cleanup
            delayTask.SetResult(true);

            // Wait for error state - with timeout
            var timeout = TimeSpan.FromSeconds(2);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (cache.State != CacheState.Error && sw.Elapsed < timeout)
            {
                await Task.Delay(100);
            }

            Assert.Equal(CacheState.Error, cache.State);
        }

        [Fact]
        public async Task AdaptiveCleanup_Should_Handle_Concurrent_Operations_During_Error()
        {
            // Setup
            var mockTimeProvider = new Mock<ITimeProvider>();
            var currentTime = DateTime.UtcNow;
            mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => currentTime);

            var delayTask = new TaskCompletionSource<bool>();
            var errorStarted = new TaskCompletionSource<bool>();

            mockTimeProvider
                .Setup(tp => tp.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns<TimeSpan, CancellationToken>((_, token) => delayTask.Task);

            using var cache = new ConcurrentAdaptiveCache<string, int>(
                capacity: 100,
                timeProvider: mockTimeProvider.Object,
                maxErrors: 1);

            // Start concurrent operations
            var addTask = Task.Run(async () =>
            {
                for (int i = 0; i < 50 && cache.State != CacheState.Error; i++)
                {
                    try
                    {
                        cache.Add(i, TimeSpan.FromMilliseconds(50), $"key{i}");
                        await Task.Delay(1);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            });

            // Let some operations complete
            await Task.Delay(100);

            // Trigger error state
            mockTimeProvider
                .Setup(tp => tp.UtcNow)
                .Callback(() => errorStarted.TrySetResult(true))
                .Throws(new InvalidOperationException("Simulated failure"));

            // Complete delay to trigger cleanup
            delayTask.SetResult(true);

            // Wait for error state
            await errorStarted.Task;
            await Task.Delay(100); // Give time for error state to propagate

            Assert.Equal(CacheState.Error, cache.State);
            Assert.Throws<InvalidCacheStateException>(() => cache.Add(1, keys: "test"));
        }
    }
}
