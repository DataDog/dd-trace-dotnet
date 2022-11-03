// <copyright file = "TaintedObject.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;

namespace Datadog.Trace.Iast;

internal class TaintedObject
{
    private readonly Range[] _ranges;
    private object _reference;

    public TaintedObject(object obj, Range[] ranges, ConcurrentQueue<object> queue)
    {
        // TODO: Keep the weak reference and add it to the queue
        // super(obj, queue);
        _reference = obj;
        queue.Enqueue(obj);
        PositiveHashCode = IastUtils.IdentityHashCode(obj) & DefaultTaintedMap.PositiveMask;
        _ranges = ranges;
    }

    public int PositiveHashCode { get; }

    public TaintedObject Next { get; set; }

    public object Get()
    {
        return _reference;
    }

    /*
     * Get ranges. The array or its elements MUST NOT be mutated. This may be reused in multiple
     * instances.
     */
    public Range[] GetRanges()
    {
        return _ranges;
    }
}
