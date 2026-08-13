// <copyright file="DefaultTaintedMapConcurrencyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class DefaultTaintedMapConcurrencyTests
{
    // A bucket index whose hash does not trigger an implicit Purge() from Put()
    // ((hash & PurgeMask) != 0), so that Put/Put races can be tested in isolation.
    private const int NonPurgingHash = 1;

    [Fact]
    public void GivenATaintedObjectMap_WhenConcurrentPutsCollideOnTheSameBucket_NoEntryIsLost()
    {
        const int threads = 8;
        const int perThread = 400;

        var map = new DefaultTaintedMap();
        var entries = new TestTaintedObject[threads][];

        for (var t = 0; t < threads; t++)
        {
            entries[t] = new TestTaintedObject[perThread];
            for (var i = 0; i < perThread; i++)
            {
                entries[t][i] = new TestTaintedObject(new HashedValue(NonPurgingHash));
            }
        }

        RunConcurrently(threads, t =>
        {
            foreach (var entry in entries[t])
            {
                map.Put(entry);
            }
        });

        var lost = 0;
        foreach (var perThreadEntries in entries)
        {
            foreach (var entry in perThreadEntries)
            {
                if (map.Get(entry.Key) is null)
                {
                    lost++;
                }
            }
        }

        lost.Should().Be(0, "concurrent Put on the same bucket must not drop entries from the chain");
        map.GetEstimatedSize().Should().Be(threads * perThread);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPurgingWhileInsertingOnTheSameBuckets_LiveEntriesAreNotLost()
    {
        // Every dead entry is alone in its bucket, so RemoveDeadKeys() collects all of these keys
        // as "dead keys" and only removes them in a second pass. A live entry inserted on one of
        // those buckets in between is silently deleted by that second pass.
        const int buckets = 2048;
        const int iterations = 10;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var map = new DefaultTaintedMap();
            var live = new List<TestTaintedObject>(buckets);

            for (var i = 0; i < buckets; i++)
            {
                // Odd hashes only, so that no Put triggers an implicit Purge().
                var hash = (i * 2) + 1;

                // Insert alive and then invalidate, mimicking a collected WeakReference target.
                var dead = new TestTaintedObject(new HashedValue(hash));
                map.Put(dead);
                dead.Invalidate();

                live.Add(new TestTaintedObject(new HashedValue(hash)));
            }

            RunConcurrently(2, t =>
            {
                if (t == 0)
                {
                    map.Purge();
                }
                else
                {
                    foreach (var entry in live)
                    {
                        map.Put(entry);
                    }
                }
            });

            var lost = 0;
            foreach (var entry in live)
            {
                if (map.Get(entry.Key) is null)
                {
                    lost++;
                }
            }

            lost.Should().Be(0, $"Purge() must not remove entries inserted concurrently (iteration {iteration})");
        }
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenGettingWhilePurgingAndInserting_LiveEntriesStayReachable()
    {
        const int buckets = 512;
        const int entriesPerBucket = 4;
        const int readerIterations = 100;

        var map = new DefaultTaintedMap();
        var live = new List<TestTaintedObject>();

        // Chains that mix live and dead entries, so RemoveDeadKeys() has to splice them.
        for (var i = 0; i < buckets; i++)
        {
            var hash = (i * 2) + 1;
            for (var e = 0; e < entriesPerBucket; e++)
            {
                var entry = new TestTaintedObject(new HashedValue(hash));
                map.Put(entry);

                if (e % 2 == 0)
                {
                    entry.Invalidate();
                }
                else
                {
                    live.Add(entry);
                }
            }
        }

        RunConcurrently(4, t =>
        {
            switch (t)
            {
                case 0:
                    for (var i = 0; i < 20; i++)
                    {
                        map.Purge();
                    }

                    break;

                case 1:
                    for (var i = 0; i < buckets; i++)
                    {
                        var entry = new TestTaintedObject(new HashedValue((i * 2) + 1));
                        map.Put(entry);
                        entry.Invalidate();
                    }

                    break;

                default:
                    for (var i = 0; i < readerIterations; i++)
                    {
                        foreach (var entry in live)
                        {
                            map.Get(entry.Key);
                        }
                    }

                    break;
            }
        });

        var lost = 0;
        foreach (var entry in live)
        {
            if (map.Get(entry.Key) is null)
            {
                lost++;
            }
        }

        lost.Should().Be(0, "purging dead entries must not drop the live ones sharing their chain");
    }

    private static void RunConcurrently(int threads, Action<int> body)
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = new Task[threads];

        for (var t = 0; t < threads; t++)
        {
            var index = t;
            tasks[t] = Task.Factory.StartNew(
                () =>
                {
                    start.Wait();
                    body(index);
                },
                TaskCreationOptions.LongRunning);
        }

        start.Set();
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Value with a controlled hash code, so that entries can be forced into a chosen bucket.
    /// Equality stays reference-based, which is what DefaultTaintedMap.Get relies on.
    /// </summary>
    private class HashedValue
    {
        private readonly int _hash;

        public HashedValue(int hash)
        {
            _hash = hash;
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => _hash;
    }

    /// <summary>
    /// ITaintedObject with deterministic liveness, so purging does not depend on the GC.
    /// </summary>
    private class TestTaintedObject : ITaintedObject
    {
        private readonly HashedValue _value;
        private bool _isAlive = true;

        public TestTaintedObject(HashedValue value)
        {
            _value = value;
            PositiveHashCode = IastUtils.IdentityHashCode(value) & DefaultTaintedMap.PositiveMask;
        }

        public object Value => _isAlive ? _value : null;

        public bool IsAlive => _isAlive;

        public int PositiveHashCode { get; }

        public ITaintedObject Next { get; set; }

        /// <summary>Gets the value, regardless of liveness, for lookups from the test.</summary>
        public object Key => _value;

        public void Invalidate() => _isAlive = false;
    }
}
