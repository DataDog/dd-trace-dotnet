// <copyright file = "TaintedObject.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal class TaintedObject : ITaintedObject
{
    private readonly Range[]? _ranges;

    private readonly WeakReference _weak;

    public TaintedObject(object value, Range[]? ranges)
    {
        _weak = new WeakReference(value);
        PositiveHashCode = IastUtils.IdentityHashCode(value) & DefaultTaintedMap.PositiveMask;
        _ranges = ranges;
    }

    public object? Value => _weak.Target;

    public bool IsAlive => _weak.IsAlive;

    public int PositiveHashCode { get; }

    public ITaintedObject? Next { get; set; }

    public Range[]? GetRanges()
    {
        return _ranges;
    }

    public override int GetHashCode()
    {
        return _weak?.Target?.GetHashCode() ?? 0;
    }
}
