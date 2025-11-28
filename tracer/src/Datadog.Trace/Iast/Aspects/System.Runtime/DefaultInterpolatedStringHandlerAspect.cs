// <copyright file="DefaultInterpolatedStringHandlerAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Datadog.Trace.Iast.Aspects.System.Runtime;

/// <summary> DefaultInterpolatedString class aspect </summary>
[AspectClass("System.Runtime")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public sealed class DefaultInterpolatedStringHandlerAspect
{
    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(String) aspect
    /// </summary>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(System.String)")]
    public static void AppendFormatted1(ref DefaultInterpolatedStringHandler target, string value)
    {
        target.AppendFormatted(value);
        try
        {
            DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), value);
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(String, Int32, String) aspect
    /// </summary>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    /// <param name="alignment"> the alignment value </param>
    /// <param name="format"> the format value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(System.String,System.Int32,System.String)")]
    public static void AppendFormatted2(ref DefaultInterpolatedStringHandler target, string? value, int alignment, string? format)
    {
        target.AppendFormatted(value, alignment, format);
        try
        {
            DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), value);
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(Object, Int32, String) aspect
    /// </summary>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the object value </param>
    /// <param name="alignment"> the alignment value </param>
    /// <param name="format"> the format value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(System.Object,System.Int32,System.String)")]
    public static void AppendFormatted3(ref DefaultInterpolatedStringHandler target, object? value, int alignment, string? format)
    {
        target.AppendFormatted(value, alignment, format);
        try
        {
            if (value is string str)
            {
                DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), str);
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(T) aspect
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(!!0)")]
    public static void AppendFormatted4<T>(ref DefaultInterpolatedStringHandler target, T value)
    {
        target.AppendFormatted<T>(value);
        try
        {
            if (value is string str)
            {
                DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), str);
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(T, String) aspect
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    /// <param name="alignment"> the alignment value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(!!0,System.Int32)")]
    public static void AppendFormatted5<T>(ref DefaultInterpolatedStringHandler target, T value, int alignment)
    {
        target.AppendFormatted(value, alignment);
        try
        {
            if (value is string str)
            {
                DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), str);
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(T, String) aspect
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    /// <param name="format"> the format value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(!!0,System.String)")]
    public static void AppendFormatted6<T>(ref DefaultInterpolatedStringHandler target, T value, string? format)
    {
        target.AppendFormatted(value, format);
        try
        {
            if (value is string str)
            {
                DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), str);
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendFormatted(T, String) aspect
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    /// <param name="alignment"> the alignment value </param>
    /// <param name="format"> the format value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendFormatted(!!0,System.Int32,System.String)")]
    public static void AppendFormatted7<T>(ref DefaultInterpolatedStringHandler target, T value, int alignment, string? format)
    {
        target.AppendFormatted(value, alignment, format);
        try
        {
            if (value is string str)
            {
                DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), str);
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.AppendLiteral(String) aspect
    /// </summary>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <param name="value"> the string value </param>
    [AspectMethodReplace("System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::AppendLiteral(System.String)")]
    public static void AppendLiteral(ref DefaultInterpolatedStringHandler target, string value)
    {
        target.AppendLiteral(value);
        try
        {
            DefaultInterpolatedStringHandlerModuleImpl.Append(ToPointer(ref target), value);
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    /// <summary>
    /// System.Runtime DefaultInterpolatedStringHandler.ToStringAndClear aspect
    /// </summary>
    /// <param name="target"> the ref DefaultInterpolatedStringHandler </param>
    /// <returns> the string value </returns>
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
            IastModule.LogAspectException(ex);
        }

        return result;
    }

    private static IntPtr ToPointer(ref DefaultInterpolatedStringHandler ts)
    {
        Ldarg(nameof(ts));
        return IL.Return<IntPtr>();
    }
}

#endif
