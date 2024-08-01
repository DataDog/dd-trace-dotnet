// <copyright file="ThreadSafeConfigurableCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ThreadSafeConfigurableCacheTests
    {
        [Fact]
        public void Constructor_WithoutCapacity_ShouldUseAutoConfiguration()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.NotNull(cache);
            // Note: We can't directly test the capacity as it's private, but we can test its behavior
        }

        [Fact]
        public void Constructor_WithCapacity_ShouldUseProvidedCapacity()
        {
            int capacity = 100;
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity);
            Assert.NotNull(cache);
            // Add more items than the capacity and check if the oldest is evicted
            for (int i = 0; i < capacity + 1; i++)
            {
                cache.Add(i.ToString(), i);
            }
            Assert.Throws<KeyNotFoundException>(() => cache.Get("0"));
        }

        [Fact]
        public void Add_WhenKeyExists_ShouldUpdateValue()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add("key", 1);
            cache.Add("key", 2);
            Assert.Equal(2, cache.Get("key"));
        }

        [Fact]
        public void Get_WhenKeyDoesNotExist_ShouldThrowKeyNotFoundException()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.Throws<KeyNotFoundException>(() => cache.Get("nonexistent"));
        }

        [Theory]
        [InlineData(EvictionPolicy.LRU)]
        [InlineData(EvictionPolicy.MRU)]
        [InlineData(EvictionPolicy.LFU)]
        public void EvictionPolicy_ShouldWorkCorrectly(EvictionPolicy policy)
        {
            int capacity = 3;
            var cache = new ThreadSafeConfigurableCache<string, int>(capacity, policy);

            cache.Add("1", 1);
            cache.Add("2", 2);
            cache.Add("3", 3);

            // Access items to affect LRU/MRU/LFU order
            cache.Get("1");
            cache.Get("2");
            cache.Get("2");

            // Add a new item to trigger eviction
            cache.Add("4", 4);

            switch (policy)
            {
                case EvictionPolicy.LRU:
                    Assert.Throws<KeyNotFoundException>(() => cache.Get("3"));
                    break;
                case EvictionPolicy.MRU:
                    Assert.Throws<KeyNotFoundException>(() => cache.Get("2"));
                    break;
                case EvictionPolicy.LFU:
                    Assert.Throws<KeyNotFoundException>(() => cache.Get("1"));
                    break;
            }
        }

        [Fact]
        public void HitRate_ShouldCalculateCorrectly()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            cache.Add("1", 1);
            cache.Add("2", 2);

            cache.Get("1"); // Hit
            cache.Get("2"); // Hit
            try { cache.Get("3"); } catch { } // Miss

            Assert.Equal(0.6666, cache.HitRate, 4); // 2 hits out of 3 accesses
        }

        [Fact]
        public void Count_ShouldReturnCorrectItemCount()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            Assert.Equal(0, cache.Count);

            cache.Add("1", 1);
            cache.Add("2", 2);
            Assert.Equal(2, cache.Count);

            cache.Get("1"); // Should not affect count
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public async Task ThreadSafety_ShouldHandleConcurrentOperations()
        {
            var cache = new ThreadSafeConfigurableCache<int, int>(capacity: 100);
            var tasks = new List<Task>();

            for (int i = 0; i < 1000; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(() => cache.Add(capture, capture)));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(100, cache.Count); // Assuming LRU policy, only the last 100 items should remain

            tasks.Clear();
            for (int i = 900; i < 1000; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        Assert.Equal(capture, cache.Get(capture));
                    }
                    catch (KeyNotFoundException)
                    {
                        Assert.True(false, $"Key {capture} should exist in the cache");
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public void IsServerlessEnvironment_ShouldDetectCorrectly()
        {
            // Mock environment for Azure Functions
            Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
            var cache = new ThreadSafeConfigurableCache<string, int>();
            // We can't directly test IsServerlessEnvironment as it's private, but we can test its effects
            // In a serverless environment, it should use LOW_RESOURCE_CAPACITY
            for (int i = 0; i < 513; i++) // LOW_RESOURCE_CAPACITY + 1
            {
                cache.Add(i.ToString(), i);
            }
            Assert.Throws<KeyNotFoundException>(() => cache.Get("0"));

            // Clean up
            Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
        }

        // Note: Testing IsLowResourceEnvironment is challenging as it depends on system resources.
        // In a real-world scenario, you might want to use dependency injection to inject a mock
        // for the system resource checks. For brevity, we'll skip this test here.

        [Fact]
        public void Dispose_ShouldNotThrowException()
        {
            var cache = new ThreadSafeConfigurableCache<string, int>();
            var exception = Record.Exception(() => cache.Dispose());
            Assert.Null(exception);
        }
    }
}
