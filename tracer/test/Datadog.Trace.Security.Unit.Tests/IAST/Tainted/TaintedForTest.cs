// <copyright file="TaintedForTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Iast;
using Range = Datadog.Trace.Iast.Range;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class TaintedForTest : ITaintedObject
{
    private bool _alive = true;
    private Range[] _ranges;
    private object _value;

    internal TaintedForTest(object value, Range[] ranges)
    {
        _value = value;
        PositiveHashCode = IastUtils.IdentityHashCode(value) & DefaultTaintedMap.PositiveMask;
        _ranges = ranges;
    }

    public bool IsAlive => _alive;

    ITaintedObject? ITaintedObject.Next { get; set; }

    public object Value => _value;

    public int PositiveHashCode { get; set; }

    public void SetAlive(bool isAlive)
    {
        _alive = isAlive;
    }

    internal Range[]? GetRanges()
    {
        return _ranges;
    }
}
