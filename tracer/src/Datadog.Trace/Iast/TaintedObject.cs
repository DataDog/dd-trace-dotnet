// <copyright file = "TaintedObject.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal class TaintedObject : IWeakAware
{
    private readonly Range[]? _ranges;

    public TaintedObject(object value, Range[]? ranges)
    {
        Weak = new WeakReference(value);
        PositiveHashCode = IastUtils.IdentityHashCode(value) & DefaultTaintedMap.PositiveMask;
        _ranges = ranges;
    }

    public object? Value => Weak.Target;

    public WeakReference Weak { get; private set; }

    public bool IsAlive => Weak.IsAlive;

    public int PositiveHashCode { get; }

    public TaintedObject? Next { get; set; }

    /*
     * Get ranges. The array or its elements MUST NOT be mutated. This may be reused in multiple
     * instances.
     */
    public Range[]? GetRanges()
    {
        return _ranges;
    }

    public override int GetHashCode()
    {
        // We exclude Next
        return IastUtils.GetHashCode(new object?[] { _ranges, Weak, PositiveHashCode });
    }
}
