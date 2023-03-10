// <copyright file="PropagationModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Propagation;

internal static class PropagationModuleImpl
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PropagationModuleImpl));

    public static object? PropagateTaint(object? input, object? result)
    {
        try
        {
            if (result is null || input is null)
            {
                return result;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext == null)
            {
                return result;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var taintedSelf = taintedObjects.Get(input);

            if (taintedSelf == null)
            {
                return result;
            }

            taintedObjects.Taint(result, taintedSelf.Ranges);
        }
        catch (Exception err)
        {
            Log.Error(err, "StringModuleImpl.TaintIfInputIsTainted exception");
        }

        return result;
    }

    internal static TaintedObject? GetTainted(TaintedObjects taintedObjects, object? value)
    {
        return value == null ? null : taintedObjects.Get(value);
    }

    /// <summary> Taints a string.substring operation </summary>
    /// <param name="self"> original string </param>
    /// <param name="beginIndex"> start index </param>
    /// <param name="result"> the substring result </param>
    /// <param name="resultLength"> Result's length </param>
    public static void OnStringSubSequence(object self, int beginIndex, object result, int resultLength)
    {
        var iastContext = IastModule.GetIastContext();
        if (iastContext == null)
        {
            return;
        }

        var taintedObjects = iastContext.GetTaintedObjects();
        var selfTainted = taintedObjects.Get(self);
        if (selfTainted == null)
        {
            return;
        }

        var rangesSelf = selfTainted.Ranges;
        if (rangesSelf.Length == 0)
        {
            return;
        }

        var newRanges = Ranges.ForSubstring(beginIndex, resultLength, rangesSelf);
        if (newRanges != null && newRanges.Length > 0)
        {
            taintedObjects.Taint(result, newRanges);
        }
    }
}
