// <copyright file="DefaultInterpolatedStringHandlerModuleImpl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.InteropServices;

namespace Datadog.Trace.Iast.Propagation;

internal static class DefaultInterpolatedStringHandlerModuleImpl
{
    public static unsafe void AppendFormatted(IntPtr target, string value)
    {
        FullTaintIfAnyTainted(target, value);
    }

    public static unsafe void FullTaintIfAnyTainted(IntPtr target, object? firstInput = null, object? secondInput = null, object? thirdInput = null, object? fourthInput = null)
    {
        try
        {
            IastModule.OnExecutedPropagationTelemetry();

            var iastContext = IastModule.GetIastContext();
            if (iastContext is null)
            {
                return;
            }

            var taintedObjects = iastContext.GetTaintedObjects();
            var tainted = PropagationModuleImpl.GetTainted(taintedObjects, target);
            bool targetIsTainted = tainted is not null;

            if (!targetIsTainted)
            {
                if (((tainted = GetTaintedWithRanges(taintedObjects, firstInput)) is null) &&
                    ((tainted = GetTaintedWithRanges(taintedObjects, secondInput)) is null) &&
                    ((tainted = GetTaintedWithRanges(taintedObjects, thirdInput)) is null) &&
                    ((tainted = GetTaintedWithRanges(taintedObjects, fourthInput)) is null))
                {
                    return;
                }
            }

            var rangesResult = new Range[] { new Range(0, -1, tainted!.Ranges[0].Source) };
            if (!targetIsTainted)
            {
                taintedObjects.Taint(target, rangesResult);
            }
            else
            {
                tainted.Ranges = rangesResult;
            }
        }
        catch (Exception error)
        {
            IastModule.Log.Error(error, $"{nameof(StringBuilderModuleImpl)}.{nameof(FullTaintIfAnyTainted)} exception");
        }
    }

    public static object? PropagateTaint(object? input, object? result, int offset = 0, bool addTelemetry = true)
    {
        try
        {
            if (addTelemetry)
            {
                IastModule.OnExecutedPropagationTelemetry();
            }

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

            if (offset != 0)
            {
                var newRanges = new Range[taintedSelf.Ranges.Length];
                Ranges.CopyShift(taintedSelf.Ranges, newRanges, 0, offset);
                taintedObjects.Taint(result, newRanges);
            }
            else
            {
                taintedObjects.Taint(result, taintedSelf.Ranges);
            }
        }
        catch (Exception err)
        {
            IastModule.Log.Error(err, "PropagationModuleImpl.PropagateTaint exception");
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
