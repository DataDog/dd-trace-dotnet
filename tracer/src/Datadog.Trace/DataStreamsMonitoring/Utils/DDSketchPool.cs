// <copyright file="DDSketchPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Datadog.Sketches;
using Datadog.Trace.Vendors.Datadog.Sketches.Mappings;
using Datadog.Trace.Vendors.Datadog.Sketches.Stores;

namespace Datadog.Trace.DataStreamsMonitoring.Utils;

/// <summary>
/// A simple pool for <see cref="DDSketch"/> to reduce the number of allocations
/// </summary>
internal class DDSketchPool
{
    private readonly BoundedConcurrentQueue<DDSketch> _pool;

    public DDSketchPool(int maxPoolSize = 1000)
    {
        _pool = new(maxPoolSize);
    }

    /// <summary>
    /// Retrieves a sketch from the pool, or creates a new one
    /// </summary>
    /// <returns>An empty <see cref="DDSketch"/></returns>
    public DDSketch Get()
    {
        if (_pool.TryDequeue(out var sketch))
        {
            return sketch;
        }

        return CreateSketch();
    }

    /// <summary>
    /// Clears the sketch and adds it to the pool.
    /// </summary>
    /// <param name="sketch">The sketch to add</param>
    public void Release(DDSketch sketch)
    {
        // clear out the sketch before returning
        sketch.Clear();
        _pool.TryEnqueue(sketch);
    }

    /// <summary>
    /// Internal for testing
    /// </summary>
    internal static DDSketch CreateSketch()
    {
        // dd-go and dd-java use different sketch parameters here
        // dd-go uses a logarithmic mapping with relativeAccuracy = 0.01 and unbounded dense store
        // https://cs.github.com/DataDog/data-streams-go/blob/6772b163707c0a8ecc8c9a3b28e0dab7e0cf58d4/datastreams/aggregator.go#L30
        // while dd-java uses BitwiseLinearlyInterpolatedMapping(1.0 / 128.0) with CollapsingLowestDenseStore(1024)
        // https://cs.github.com/DataDog/dd-trace-java/blob/3386bd137e58ed7450d1704e269d3567aeadf4c0/utils/histograms/src/main/java/datadog/trace/core/histogram/DDSketchHistogram.java#L16
        // Any relative accuracy works in the backend, and they normalize it
        // But if we match the values they use it avoids the conversion step
        return new DDSketch(
            new LogarithmicMapping(gamma: 1.015625, indexOffset: 1338.5),
            new CollapsingLowestDenseStore(1024),
            new CollapsingLowestDenseStore(1024));
    }
}
