// <copyright file="DefaultInterpolatedStringHandlerAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
using InlineIL;
using static InlineIL.IL.Emit;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Runtime;

#pragma warning disable DD0005
#pragma warning disable SA1642
#pragma warning disable SA1107

/// <summary> DefaultInterpolatedString class aspect </summary>
[AspectClass("System.Runtime")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class DefaultInterpolatedStringHandlerAspect
{
    /// <summary>
    /// System.Reflection Assembly.Load aspects
    /// </summary>
    /// <param name="target"> target </param>
    /// <param name="value"> value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(System.String)")]
    public static void AppendFormatted(ref DefaultInterpolatedStringHandler target, string value)
    {
        target.AppendFormatted(value);
        try
        {
            DefaultInterpolatedStringHandlerModuleImpl.AppendFormatted(ToPointer(ref target), value);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DefaultInterpolatedStringHandlerAspect)}.{nameof(AppendFormatted)}");
        }
    }

    /// <summary>
    /// System.Reflection Assembly.Load aspects
    /// </summary>
    /// <param name="target"> target </param>
    /// <returns> string value </returns>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::ToStringAndClear()")]
    public static string ToStringAndClear(ref DefaultInterpolatedStringHandler target)
    {
        var result = target.ToStringAndClear();
        try
        {
            DefaultInterpolatedStringHandlerModuleImpl.PropagateTaint(ToPointer(ref target), result);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DefaultInterpolatedStringHandlerAspect)}.{nameof(AppendFormatted)}");
        }

        return result;
    }

    private static unsafe IntPtr ToPointer(ref DefaultInterpolatedStringHandler ts)
    {
        Ldarg(nameof(ts));
        return IL.Return<IntPtr>();
    }
}

#endif
