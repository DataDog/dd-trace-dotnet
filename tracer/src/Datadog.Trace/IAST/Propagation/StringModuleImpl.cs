// <copyright file="StringModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Iast.Propagation;

internal static class StringModuleImpl
{
    internal static bool CanBeTainted(string txt)
    {
        return txt != null && txt.Length > 0;
    }

    internal static TaintedObject? GetTainted(TaintedObjects to, object? value)
    {
        return value == null ? null : to.Get(value);
    }

    public static void OnStringConcat(string left, string right, string result)
    {
        if (!CanBeTainted(result))
        {
            return;
        }

        if (!CanBeTainted(left) && !CanBeTainted(right))
        {
            return;
        }

        var ctx = IastModule.GetIastContext();
        if (ctx == null)
        {
            return;
        }

        TaintedObjects taintedObjects = ctx.GetTaintedObjects();
        TaintedObject? taintedLeft = GetTainted(taintedObjects, left);
        TaintedObject? taintedRight = GetTainted(taintedObjects, right);
        if (taintedLeft == null && taintedRight == null)
        {
            return;
        }

        Range[]? ranges;
        if (taintedRight == null)
        {
            ranges = taintedLeft!.Ranges;
        }
        else if (taintedLeft == null)
        {
            ranges = new Range[taintedRight!.Ranges!.Length];
            Ranges.CopyShift(taintedRight.Ranges, ranges, 0, left.Length);
        }
        else
        {
            ranges = Ranges.MergeRanges(left.Length, taintedLeft!.Ranges, taintedRight!.Ranges);
        }

        taintedObjects.Taint(result, ranges);
    }
}
