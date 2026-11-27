// <copyright file="RedactionCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Snapshots;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class RedactionCacheTests
    {
        [Fact]
        public void TryAdd_StopsCachingAtCapacity()
        {
            var cache = new RedactionCache<string>(capacity: 2);

            cache.TryAdd("a", true).Should().BeTrue();
            cache.TryAdd("b", false).Should().BeTrue();
            cache.TryAdd("c", true).Should().BeFalse();

            cache.Count.Should().Be(2);
            cache.TryGetValue("a", out var a).Should().BeTrue();
            a.Should().BeTrue();
            cache.TryGetValue("b", out var b).Should().BeTrue();
            b.Should().BeFalse();
            cache.TryGetValue("c", out _).Should().BeFalse();
        }

        [Fact]
        public void TryAdd_DuplicateKey_DoesNotUseCapacity()
        {
            var cache = new RedactionCache<string>(capacity: 2);

            cache.TryAdd("a", true).Should().BeTrue();
            cache.TryAdd("a", false).Should().BeTrue();
            cache.TryAdd("b", false).Should().BeTrue();

            cache.Count.Should().Be(2);
            cache.TryGetValue("a", out var a).Should().BeTrue();
            a.Should().BeTrue();
            cache.TryGetValue("b", out _).Should().BeTrue();
        }

        [Fact]
        public void TryAdd_AtCapacity_ReturnsTrueOnlyForAStoredKey()
        {
            var cache = new RedactionCache<string>(capacity: 1);
            cache.TryAdd("a", true).Should().BeTrue();

            cache.TryAdd("a", false).Should().BeTrue();
            cache.TryAdd("b", true).Should().BeFalse();

            cache.Count.Should().Be(1);
            cache.TryGetValue("a", out var a).Should().BeTrue();
            a.Should().BeTrue();
        }

        [Fact]
        public void AddOrReplace_WhenNotFull_AddsToTheSameCache()
        {
            var cache = new RedactionCache<string>(capacity: 2);
            var field = cache;

            cache.AddOrReplace(ref field, "a", true);

            field.Should().BeSameAs(cache);
            cache.TryGetValue("a", out var a).Should().BeTrue();
            a.Should().BeTrue();
        }

        [Fact]
        public void AddOrReplace_WhenFull_StartsANewCache()
        {
            var full = new RedactionCache<string>(capacity: 1);
            full.TryAdd("a", false).Should().BeTrue();
            var field = full;

            full.AddOrReplace(ref field, "b", true);

            field.Should().NotBeSameAs(full);
            field.Capacity.Should().Be(1);
            field.TryGetValue("b", out var b).Should().BeTrue();
            b.Should().BeTrue();
            field.TryGetValue("a", out _).Should().BeFalse();
        }

        [Fact]
        public void AddOrReplace_WhenFullCacheWasAlreadyReplaced_KeepsTheReplacement()
        {
            var full = new RedactionCache<string>(capacity: 1);
            full.TryAdd("a", false).Should().BeTrue();
            var installed = new RedactionCache<string>(capacity: 1);
            var field = installed;

            full.AddOrReplace(ref field, "b", true);

            field.Should().BeSameAs(installed);
            installed.TryGetValue("b", out _).Should().BeFalse();
        }

        [Fact]
        public void TryReplace_WhenAnotherCacheWasInstalled_KeepsIt()
        {
            // The replacement raced with another thread or a configuration change that installed a new cache first.
            var full = new RedactionCache<string>(capacity: 1);
            var installed = new RedactionCache<string>(capacity: 1);
            var field = installed;

            full.TryReplace(ref field, "b", true).Should().BeFalse();

            field.Should().BeSameAs(installed);
            installed.TryGetValue("b", out _).Should().BeFalse();
        }

        [Fact]
        public void TryAdd_Concurrent_NeverExceedsCapacity()
        {
            const int capacity = 100;
            const int keys = 10_000;
            var cache = new RedactionCache<int>(capacity);

            Parallel.For(0, keys, i => cache.TryAdd(i, true));

            cache.Count.Should().Be(capacity);
            Enumerable.Range(0, keys).Count(i => cache.TryGetValue(i, out _)).Should().Be(capacity);
        }
    }
}
