// <copyright file="ThreadSafeConfigurableCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Caching;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ThreadSafeConfigurableCacheTests
    {
        private const int DefaultCapacity = 2048;
        private const int LowResourceCapacity = 512;
        private const int RestrictedEnvironmentCapacity = 1024;

        [Fact]
        public void Constructor_DefaultParameters_CreatesCache()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.NotNull(cache);
            Assert.Equal(DefaultCapacity, cache.Capacity);
        }

        [Fact]
        public void Add_SingleKey_AddsToCache()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add(42, "key1");
            Assert.True(cache.TryGet("key1", out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void Add_MultipleKeys_AddsToCache()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add(42, "key1", "key2", "key3");

            Assert.True(cache.TryGet("key1", out int value1));
            Assert.True(cache.TryGet("key2", out int value2));
            Assert.True(cache.TryGet("key3", out int value3));

            Assert.Equal(42, value1);
            Assert.Equal(42, value2);
            Assert.Equal(42, value3);
        }

        [Fact]
        public void Add_NullKey_ThrowsNullKeyException()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.Throws<NullKeyException>(() => cache.Add(42, null));
        }

        [Fact]
        public void Add_KeyValuePairs_AddsToCache()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            var items = new Dictionary<string, int>
            {
                { "key1", 42 },
                { "key2", 43 }
            };

            cache.Add(items);

            Assert.True(cache.TryGet("key1", out int value1));
            Assert.True(cache.TryGet("key2", out int value2));

            Assert.Equal(42, value1);
            Assert.Equal(43, value2);
        }

        [Fact]
        public void TryGet_ExistingKey_ReturnsTrue()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add(42, "key1");
            Assert.True(cache.TryGet("key1", out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void TryGet_NonExistingKey_ReturnsFalse()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.False(cache.TryGet("key1", out int value));
            Assert.Equal(default(int), value);
        }

        [Fact]
        public void TryGet_NullKey_ThrowsNullKeyException()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.Throws<NullKeyException>(() => cache.TryGet(null, out int value));
        }

        [Fact]
        public void GetOrAdd_ExistingKey_ReturnsExistingValue()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add(42, "key1");
            int result = cache.GetOrAdd("key1", k => 43);
            Assert.Equal(42, result);
        }

        [Fact]
        public void GetOrAdd_NonExistingKey_AddsNewValue()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            int result = cache.GetOrAdd("key1", k => 42);
            Assert.Equal(42, result);
            Assert.True(cache.TryGet("key1", out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void GetOrAdd_NullKey_ThrowsNullKeyException()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.Throws<NullKeyException>(() => cache.GetOrAdd(null, k => 42));
        }

        [Fact]
        public void Count_ReturnsCorrectNumber()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add(42, "key1", "key2");
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void HitRate_CalculatesCorrectly()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add(42, "key1");

            cache.TryGet("key1", out _); // Hit
            cache.TryGet("key2", out _); // Miss
            cache.TryGet("key1", out _); // Hit

            Assert.Equal(2.0 / 3.0, cache.HitRate, 3);
        }

        [Fact]
        public void EvictionPolicy_LRU_EvictsOldestItem()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity: 2, evictionPolicy: EvictionPolicy.LRU);
            cache.Add(1, "key1");
            cache.Add(2, "key2");
            cache.TryGet("key1", out _); // Access key1 to make it most recently used
            cache.Add(3, "key3"); // This should evict key2

            Assert.True(cache.TryGet("key1", out _));
            Assert.True(cache.TryGet("key3", out _));
            Assert.False(cache.TryGet("key2", out _));
        }

        [Fact]
        public void EvictionPolicy_MRU_EvictsNewestItem()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity: 2, evictionPolicy: EvictionPolicy.MRU);
            cache.Add(1, "key1");
            cache.Add(2, "key2");
            cache.TryGet("key2", out _); // Access key2 to make it most recently used
            cache.Add(3, "key3"); // This should evict key2

            Assert.True(cache.TryGet("key1", out _));
            Assert.True(cache.TryGet("key3", out _));
            Assert.False(cache.TryGet("key2", out _));
        }

        [Fact]
        public void EvictionPolicy_LFU_EvictsLeastFrequentlyUsedItem()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity: 2, evictionPolicy: EvictionPolicy.LFU);
            cache.Add(1, "key1");
            cache.Add(2, "key2");
            cache.TryGet("key1", out _); // Access key1 to increase its frequency
            cache.Add(3, "key3"); // This should evict key2

            Assert.True(cache.TryGet("key1", out _));
            Assert.True(cache.TryGet("key3", out _));
            Assert.False(cache.TryGet("key2", out _));
        }

        [Fact]
        public void Capacity_DefaultEnvironment_ReturnsDefaultCapacity()
        {
            var mockEnvironmentChecker = new Mock<IEnvironmentChecker>();
            mockEnvironmentChecker.Setup(m => m.IsServerlessEnvironment()).Returns(false);

            var mockMemoryChecker = new Mock<IMemoryChecker>();
            mockMemoryChecker.Setup(m => m.IsLowResourceEnvironment()).Returns(false);

            var cache = new ThreadSafeConfigurableCache<string, int>(
                environmentChecker: mockEnvironmentChecker.Object,
                memoryChecker: mockMemoryChecker.Object);

            Assert.Equal(DefaultCapacity, cache.Capacity);
        }

        [Fact]
        public void Capacity_ServerlessEnvironment_ReturnsLowResourceCapacity()
        {
            var mockEnvironmentChecker = new Mock<IEnvironmentChecker>();
            mockEnvironmentChecker.Setup(m => m.IsServerlessEnvironment()).Returns(true);

            var cache = new ThreadSafeConfigurableCache<string, int>(
                environmentChecker: mockEnvironmentChecker.Object);

            Assert.Equal(LowResourceCapacity, cache.Capacity);
        }

        [Fact]
        public void Capacity_LowResourceEnvironment_ReturnsLowResourceCapacity()
        {
            var mockEnvironmentChecker = new Mock<IEnvironmentChecker>();
            mockEnvironmentChecker.Setup(m => m.IsServerlessEnvironment()).Returns(false);

            var mockMemoryChecker = new Mock<IMemoryChecker>();
            mockMemoryChecker.Setup(m => m.IsLowResourceEnvironment()).Returns(true);

            var cache = new ThreadSafeConfigurableCache<string, int>(
                environmentChecker: mockEnvironmentChecker.Object,
                memoryChecker: mockMemoryChecker.Object);

            Assert.Equal(LowResourceCapacity, cache.Capacity);
        }

        [Fact]
        public void Capacity_ExceptionInEnvironmentCheck_ReturnsRestrictedEnvironmentCapacity()
        {
            var mockEnvironmentChecker = new Mock<IEnvironmentChecker>();
            mockEnvironmentChecker.Setup(m => m.IsServerlessEnvironment()).Throws<Exception>();

            var cache = new ThreadSafeConfigurableCache<string, int>(
                environmentChecker: mockEnvironmentChecker.Object);

            Assert.Equal(RestrictedEnvironmentCapacity, cache.Capacity);
        }

        [Fact]
        public void Capacity_ExplicitlySet_ReturnsSetCapacity()
        {
            const int explicitCapacity = 100;
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity: explicitCapacity);
            Assert.Equal(explicitCapacity, cache.Capacity);
        }

        [Fact]
        public void Concurrency_MultipleThreads_AddAndRetrieveSuccessfully()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity: 1000);
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int capturedI = i;
                tasks.Add(Task.Run(() =>
                {
                    cache.Add(capturedI, $"key{capturedI}");
                    Assert.True(cache.TryGet($"key{capturedI}", out int value));
                    Assert.Equal(capturedI, value);
                }));
            }

            Task.WaitAll(tasks.ToArray());
            Assert.Equal(100, cache.Count);
        }

        [Fact]
        public void Add_BeyondCapacity_EvictsItems()
        {
            const int capacity = 2;
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity: capacity);

            cache.Add(1, "key1");
            cache.Add(2, "key2");
            Assert.Equal(capacity, cache.Count);

            cache.Add(3, "key3");
            Assert.Equal(capacity, cache.Count);
            Assert.False(cache.TryGet("key1", out _) && cache.TryGet("key2", out _) && cache.TryGet("key3", out _), "One of the keys should have been evicted");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Capacity_VariousValues_RespectsLimit(int capacity)
        {
            var cache = new ThreadSafeConfigurableCache<int, string>(capacity: capacity);

            for (int i = 0; i < capacity * 2; i++)
            {
                cache.Add($"Value{i}", i);
            }

            Assert.Equal(capacity, cache.Count);
        }
    }
}
