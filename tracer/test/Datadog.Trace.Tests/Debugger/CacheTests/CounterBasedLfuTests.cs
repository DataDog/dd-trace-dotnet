// <copyright file="CounterBasedLfuTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Caching;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Debugger.CacheTests;

public class CounterBasedLfuTests(ITestOutputHelper output)
{
    [Fact]
    public void LFU_BatchOperations_ShouldProvideDeterministicEviction()
    {
        // Arrange
        var mockTimeProvider = new Mock<ITimeProvider>();
        var fixedTime = DateTime.UtcNow;
        mockTimeProvider.Setup(tp => tp.UtcNow).Returns(fixedTime);

        using var cache = new ConcurrentAdaptiveCache<string, int>(
            capacity: 3,
            evictionPolicyKind: EvictionPolicy.LFU,
            timeProvider: mockTimeProvider.Object);

        // Act - Add items in sequence to ensure deterministic access order
        cache.Add(1, keys: "A");
        cache.Add(2, keys: "B");
        cache.Add(3, keys: "C");

        // Access B and C more frequently than A
        cache.TryGet("B", out _);
        cache.TryGet("C", out _);
        cache.TryGet("B", out _);

        // Adding a new item should evict A (least frequently used)
        cache.Add(4, keys: "D");

        // Assert
        Assert.False(cache.TryGet("A", out _), "A should have been evicted (frequency=1)");
        Assert.True(cache.TryGet("B", out _), "B should remain (frequency=3)");
        Assert.True(cache.TryGet("C", out _), "C should remain (frequency=2)");
        Assert.True(cache.TryGet("D", out _), "D should be present (newest)");
    }

    [Fact]
    public async Task LFU_ConcurrentAccess_ShouldMaintainFrequencyOrder()
    {
        // Arrange
        using var cache = new ConcurrentAdaptiveCache<string, int>(
            capacity: 3,
            evictionPolicyKind: EvictionPolicy.LFU);

        // Add initial items
        cache.Add(1, keys: "A");
        cache.Add(2, keys: "B");
        cache.Add(3, keys: "C");

        // Act - Access items with different frequencies
        var tasks = new List<Task>();

        // Make B most frequently accessed
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() => cache.TryGet("B", out _)));
        }

        // Make C second most frequently accessed
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(() => cache.TryGet("C", out _)));
        }

        // A remains least frequently accessed
        tasks.Add(Task.Run(() => cache.TryGet("A", out _)));

        await Task.WhenAll(tasks);

        // Force eviction by adding new item
        cache.Add(4, keys: "D");

        // Assert
        Assert.False(cache.TryGet("A", out _), "A should be evicted (least frequent)");
        Assert.True(cache.TryGet("B", out _), "B should remain (most frequent)");
        Assert.True(cache.TryGet("C", out _), "C should remain (medium frequent)");
        Assert.True(cache.TryGet("D", out _), "D should be present (newest)");
    }

    [Fact]
    public void CounterBased_SequentialAccess_Analysis()
    {
        using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 3);
        var accessCounts = new Dictionary<string, int>();

        // Add and track initial items
        foreach (var key in new[] { "A", "B", "C" })
        {
            cache.Add(1, keys: key);
            accessCounts[key] = 1; // Initial add counts as one access
        }

        // Create different access patterns
        for (int i = 0; i < 5; i++)
        {
            cache.TryGet("B", out _);
            int currentCount;
            accessCounts["B"] = (accessCounts.TryGetValue("B", out currentCount) ? currentCount : 0) + 1;
        }

        for (int i = 0; i < 3; i++)
        {
            cache.TryGet("C", out _);
            int currentCount;
            accessCounts["C"] = (accessCounts.TryGetValue("C", out currentCount) ? currentCount : 0) + 1;
        }

        // Force eviction
        cache.Add(2, keys: "D");
        accessCounts["D"] = 1;

        // Output access statistics
        foreach (var kvp in accessCounts.OrderByDescending(x => x.Value))
        {
            bool exists = cache.TryGet(kvp.Key, out _);
            output.WriteLine($"Key: {kvp.Key}, Access Count: {kvp.Value}, Still in Cache: {exists}");
        }

        // Verify eviction behavior
        Assert.False(cache.TryGet("A", out _), "Least frequently used item should be evicted");
        Assert.True(cache.TryGet("B", out _), "Most frequently used item should remain");
        Assert.True(cache.TryGet("C", out _), "Second most frequently used item should remain");
        Assert.True(cache.TryGet("D", out _), "Newly added item should be present");
    }

    [Fact]
    public async Task CounterBased_ConcurrentAccess_Analysis()
    {
        using var cache = new ConcurrentAdaptiveCache<string, int>(capacity: 3);
        var accessCounts = new ConcurrentDictionary<string, int>();

        // Initialize items
        foreach (var key in new[] { "A", "B", "C" })
        {
            cache.Add(1, keys: key);
            accessCounts[key] = 1; // Initial count from Add operation
        }

        // Make sure initial state is correct
        output.WriteLine("Initial state:");
        foreach (var kvp in accessCounts)
        {
            output.WriteLine($"Key: {kvp.Key}, Initial Count: {kvp.Value}");
        }

        // Create tasks for concurrent access
        var tasks = new List<Task>();

        // Extremely heavy access to B - 100 accesses
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (cache.TryGet("B", out _))
                {
                    accessCounts.AddOrUpdate("B", 1, (_, count) => count + 1);
                }
            }));
        }

        // Medium access to C - 50 accesses
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (cache.TryGet("C", out _))
                {
                    accessCounts.AddOrUpdate("C", 1, (_, count) => count + 1);
                }
            }));
        }

        // No additional access to A - remains at initial count

        await Task.WhenAll(tasks);

        // Add a delay to ensure all operations are complete
        await Task.Delay(500);

        output.WriteLine("\nAccess counts before eviction:");
        foreach (var kvp in accessCounts.OrderByDescending(x => x.Value))
        {
            output.WriteLine($"Key: {kvp.Key}, Access Count: {kvp.Value}");
        }

        // Force eviction
        cache.Add(2, keys: "D");
        accessCounts["D"] = 1;

        await Task.Delay(100); // Give eviction time to complete

        // Output final state
        output.WriteLine("\nFinal cache state:");
        foreach (var key in new[] { "A", "B", "C", "D" })
        {
            bool exists = cache.TryGet(key, out _);
            int count = accessCounts.TryGetValue(key, out var value) ? value : 0;
            output.WriteLine($"Key: {key}, Access Count: {count}, In Cache: {exists}");
        }

        // Verify eviction behavior
        Assert.False(
            cache.TryGet("A", out _),
            $"A should be evicted as least frequently used (count: {accessCounts["A"]})");
        Assert.True(
            cache.TryGet("B", out _),
            $"B should remain as most frequently used (count: {accessCounts["B"]})");
        Assert.True(
            cache.TryGet("C", out _),
            $"C should remain as second most used (count: {accessCounts["C"]})");
        Assert.True(
            cache.TryGet("D", out _),
            "D should be present as newest item");
    }
}
