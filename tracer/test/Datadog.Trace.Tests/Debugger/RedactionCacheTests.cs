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
