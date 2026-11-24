// <copyright file="TagCacheTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

// xUnit runs these inherited scenarios for both EdgeTagCacheTests and BacklogTagCacheTests.
public abstract class TagCacheTestsBase
{
    [Fact]
    public async Task GetOrCreateTags_BypassesCache_WhenAtMaxCapacityWithActiveEntries()
    {
        var dsm = CreateManager();
        try
        {
            // Every entry stays active for the duration of the test, so none can be evicted.
            var cached = new object[DataStreamsManager.MaxTagCacheSize];
            for (var i = 0; i < cached.Length; i++)
            {
                cached[i] = GetTags(i);
                GetTags(i).Should().BeSameAs(cached[i]);
            }

            var first = GetTags(cached.Length);
            var second = GetTags(cached.Length);

            // This key bypasses the full cache, so each call creates a new tag object with the same contents.
            second.Should().NotBeSameAs(first);
            // The overflow requests must preserve the original cached object for the active key 0.
            GetTags(0).Should().BeSameAs(cached[0]);
        }
        finally
        {
            await dsm.DisposeAsync();
        }

        object GetTags(int value) => GetOverflowTestTags<OverflowScenario>(dsm, value);
    }

    [Fact]
    public async Task GetOrCreateTags_ManagersShareCache()
    {
        var firstManager = CreateManager();
        var secondManager = CreateManager();
        try
        {
            var first = GetTags(firstManager, 0);
            GetTags(firstManager, 0).Should().BeSameAs(first);
            GetTags(secondManager, 0).Should().BeSameAs(first);

            // Entries inserted through either manager must be visible through the other.
            var second = GetTags(secondManager, 1);
            GetTags(firstManager, 1).Should().BeSameAs(second);
            GetTags(firstManager, 0).Should().BeSameAs(first);
        }
        finally
        {
            await firstManager.DisposeAsync();
            await secondManager.DisposeAsync();
        }

        object GetTags(DataStreamsManager manager, int value)
            => GetOverflowTestTags<SharedCacheScenario>(manager, value);
    }

    [Fact]
    public async Task GetOrCreateTags_ConcurrentFirstAccessReturnsSameCachedValue()
    {
        var dsm = CreateManager();
        try
        {
            var tasks = Enumerable.Range(0, 16)
                                  .Select(_ => Task.Run(() => GetOverflowTestTags<ConcurrentAccessScenario>(dsm, 0)));
            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                result.Should().BeSameAs(results[0]);
            }
        }
        finally
        {
            await dsm.DisposeAsync();
        }
    }

    private protected abstract object GetOverflowTestTags<TScenario>(DataStreamsManager manager, int value);

    private static DataStreamsManager CreateManager()
    {
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
        });
        return new DataStreamsManager(settings, new DataStreamsManagerTests.DataStreamsWriterMock(), Mock.Of<IDiscoveryService>());
    }

    private protected readonly struct OverflowTestKey<TScenario> : IEquatable<OverflowTestKey<TScenario>>
    {
        public readonly int Value;

        public OverflowTestKey(int value) => Value = value;

        public bool Equals(OverflowTestKey<TScenario> other) => Value == other.Value;

        public override bool Equals(object obj) => obj is OverflowTestKey<TScenario> other && Equals(other);

        public override int GetHashCode() => Value;
    }

    // Marker types give each scenario its own static cache while retaining the same key shape.
    private readonly struct OverflowScenario
    {
    }

    private readonly struct SharedCacheScenario
    {
    }

    private readonly struct ConcurrentAccessScenario
    {
    }
}
