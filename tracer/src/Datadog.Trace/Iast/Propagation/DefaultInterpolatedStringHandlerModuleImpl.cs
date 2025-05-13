// <copyright file="DefaultInterpolatedStringHandlerModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Iast.Propagation;

internal static class DefaultInterpolatedStringHandlerModuleImpl
{
    private const int MaxStackSize = 4;

    [ThreadStatic]
    private static Stack<object>? _taintedRefStructs;

    private static Stack<object> TaintedRefStructs
    {
        get => _taintedRefStructs ??= new Stack<object>(MaxStackSize);
    }

    public static void Append(IntPtr target, string? value)
    {
        FullTaintIfAnyTainted(target, value);
    }

    public static void FullTaintIfAnyTainted(IntPtr target, string? input)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();

            if (input is null)
            {
                return;
            }

            var iastContext = IastModule.GetIastContext();
            if (iastContext is null)
            {
                return;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var tainted = PropagationModuleImpl.GetTainted(taintedObjects, target);
            var targetIsTainted = tainted is not null;

            if (!targetIsTainted && (tainted = GetTaintedWithRanges(taintedObjects, input)) is null)
            {
                return;
            }

            var rangesResult = new[] { new Range(tainted!.Ranges[0].Source, tainted.Ranges[0].SecureMarks) };
            if (!targetIsTainted)
            {
                // Safe guard to avoid memory leak
                if (TaintedRefStructs.Count >= MaxStackSize)
                {
                    TaintedRefStructs.Clear();
                }

                object targetObj = target;
                TaintedRefStructs.Push(targetObj);

                taintedObjects.Taint(targetObj, rangesResult);
            }
            else
            {
                tainted.Ranges = rangesResult;
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    public static object? PropagateTaint(object? input, string? result)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();

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

            var range = new Range(0, result.Length, taintedSelf.Ranges[0].Source, taintedSelf.Ranges[0].SecureMarks);
            taintedObjects.Taint(result, [range]);
            taintedSelf.Invalidate();
            if (TaintedRefStructs.Count > 0)
            {
                TaintedRefStructs.Pop();
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }

        return result;
    }

    private static TaintedObject? GetTaintedWithRanges(TaintedObjects taintedObjects, object? value)
    {
        var tainted = PropagationModuleImpl.GetTainted(taintedObjects, value);
        return tainted is not null && tainted?.Ranges.Length > 0 ? tainted : null;
    }
}

#endif
