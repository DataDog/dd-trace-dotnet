// <copyright file="DDSketchPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.Vendors.Datadog.Sketches;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DDSketchPoolTests
{
    [Fact]
    public void CanGetAndReleaseSketchRepeatedly()
    {
        var pool = new DDSketchPool();
        var sketch = pool.Get();
        pool.Release(sketch);

        var sketch2 = pool.Get();
        sketch2.Should().Be(sketch);
        pool.Release(sketch2);

        var sketch3 = pool.Get();
        sketch2.Should().Be(sketch);
        pool.Release(sketch3);
    }

    [Fact]
    public void WhenPoolFull_GetsNewInstance()
    {
        const int maxPoolSize = 10;
        var pool = new DDSketchPool(maxPoolSize);
        var sketches = new List<DDSketch>(maxPoolSize + 1);

        // Fetch 11 sketches. These are all unique
        for (var i = 0; i < sketches.Count; i++)
        {
            sketches.Add(pool.Get());
        }

        // Release all the sketches. The last sketch is _not_ saved in the pool
        foreach (var sketch in sketches)
        {
            pool.Release(sketch);
        }

        // Fetch all the sketches out of the pool
        // We have seen all of these sketches before
        var sketches2 = new List<DDSketch>(maxPoolSize);
        for (var i = 0; i < sketches2.Count; i++)
        {
            sketches2.Add(pool.Get());
        }

        sketches.Should().OnlyHaveUniqueItems();
        sketches2.Should().OnlyHaveUniqueItems();
        sketches2.Should().BeSubsetOf(sketches);

        // This one should be new
        var newSketch = pool.Get();
        sketches.Should().NotContain(newSketch);
    }

    [Fact]
    public void WhenSketchIsReleasedToPool_ItIsCleared()
    {
        var pool = new DDSketchPool();
        var sketch = pool.Get();

        sketch.Add(5);
        sketch.IsEmpty().Should().BeFalse();

        pool.Release(sketch);
        sketch.IsEmpty().Should().BeTrue();
    }
}
