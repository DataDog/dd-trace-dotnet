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
    public static unsafe void Append(IntPtr target, string? value)
    {
        FullTaintIfAnyTainted(target, value);
    }

    public static unsafe void FullTaintIfAnyTainted(IntPtr target, string? input)
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

            var rangesResult = new[] { new Range(0, 0, tainted!.Ranges[0].Source, tainted.Ranges[0].SecureMarks) };
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
            IastModule.Log.Error(error, $"{nameof(DefaultInterpolatedStringHandlerModuleImpl)}.{nameof(FullTaintIfAnyTainted)} exception");
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
        }
        catch (Exception err)
        {
            IastModule.Log.Error(err, $"{nameof(DefaultInterpolatedStringHandlerModuleImpl)}.{nameof(PropagateTaint)} exception");
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
