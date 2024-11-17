// <copyright file="DefaultInterpolatedStringHandlerAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.Runtime;

#pragma warning disable DD0004

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
    /// <returns> DefaultInterpolatedStringHandler </returns>
    [AspectMethodInsertBefore("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(System.String)", 0)]
    public static DefaultInterpolatedStringHandler AppendFormatted(DefaultInterpolatedStringHandler target, string value)
    {
        try
        {
            target.AppendFormatted(value);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DefaultInterpolatedStringHandlerAspect)}.{nameof(AppendFormatted)}");
        }

        return target;
    }
}

#endif
