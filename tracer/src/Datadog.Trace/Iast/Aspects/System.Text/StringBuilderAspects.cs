// <copyright file="StringBuilderAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Text;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

namespace Datadog.Trace.Iast.Aspects.System.Text;

/// <summary> StringBuilder class aspects </summary>
[AspectClass("mscorlib,netstandard,System.Runtime")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public partial class StringBuilderAspects
{
#pragma warning disable S2259 // Null pointers should not be dereferenced

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <returns> New StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string? value)
    {
        var result = new StringBuilder(value);
        PropagationModuleImpl.PropagateTaint(value, result);
        return result;
    }

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <param name="capacity"> StringBuilder initial capacity </param>
    /// <returns> Nerw StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string value, int capacity)
    {
        var result = new StringBuilder(value, capacity);
        PropagationModuleImpl.PropagateTaint(value, result);
        return result;
    }

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <param name="startIndex"> string startIndex </param>
    /// <param name="length"> string length </param>
    /// <param name="capacity"> StringBuilder initial capacity </param>
    /// <returns> Nerw StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String,System.Int32,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string value, int startIndex, int length, int capacity)
    {
        var result = new StringBuilder(value, startIndex, length, capacity);
        StringBuilderModuleImpl.OnStringBuilderSubSequence(value, startIndex, length, result);
        return result;
    }

    /// <summary>  StringBuilder.ToString aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <returns> instance.ToString() </returns>
    [AspectMethodReplace("System.Object::ToString()", "System.Text.StringBuilder")]
    public static string ToString(StringBuilder target)
    {
        var result = target.ToString();
        PropagationModuleImpl.PropagateTaint(target, result);
        PropagationModuleImpl.FixRangesIfNeeded(result);
        return result;
    }

    /// <summary>  StringBuilder.ToString aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="length"> length parameter </param>
    /// <returns> instance.ToString() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::ToString(System.Int32,System.Int32)")]
    public static string ToString(StringBuilder target, int startIndex, int length)
    {
        var result = target.ToString(startIndex, length);
        PropagationModuleImpl.OnStringSubSequence(target, startIndex, result, result.Length);
        PropagationModuleImpl.FixRangesIfNeeded(result);
        return result;
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> value parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Append(StringBuilder target, string? value)
    {
        var initialLength = target.Length;
        var length = value?.Length ?? 0;
        return StringBuilderModuleImpl.OnStringBuilderAppend(target.Append(value), initialLength, value, length, 0, length);
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> value parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Text.StringBuilder)")]
    public static StringBuilder Append(StringBuilder target, StringBuilder? value)
    {
        var initialLength = target.Length;
        var length = value?.Length ?? 0;
        return StringBuilderModuleImpl.OnStringBuilderAppend(target.Append(value), initialLength, value, length, 0, length);
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="count"> count parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.String,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Append(StringBuilder target, string? value, int startIndex, int count)
    {
        var initialLength = target.Length;
        return StringBuilderModuleImpl.OnStringBuilderAppend(target!.Append(value, startIndex, count), initialLength, value, value?.Length ?? 0, startIndex, count);
    }

#if !NETFRAMEWORK
    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="count"> count parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Text.StringBuilder,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Append(StringBuilder target, StringBuilder? value, int startIndex, int count)
    {
        var initialLength = target.Length;
        return StringBuilderModuleImpl.OnStringBuilderAppend(target!.Append(value?.ToString(), startIndex, count), initialLength, value, value?.Length ?? 0, startIndex, count);
    }
#endif

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="charCount"> charCount parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char[],System.Int32,System.Int32)")]
    public static StringBuilder Append(StringBuilder target, char[]? value, int startIndex, int charCount)
    {
        var initialLength = target.Length;
        return StringBuilderModuleImpl.OnStringBuilderAppend(target!.Append(value, startIndex, charCount), initialLength, value, value?.Length ?? 0, startIndex, charCount);
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Object)")]
    public static StringBuilder Append(StringBuilder target, object? value)
    {
        var initialLength = target.Length;

        object? valueObject;
        int length;
        if (value is StringBuilder valueStringBuilder)
        {
            valueObject = valueStringBuilder;
            length = valueStringBuilder!.Length;
        }
        else
        {
            valueObject = value?.ToString();
            length = (valueObject as string)?.Length ?? 0;
        }

        return StringBuilderModuleImpl.OnStringBuilderAppend(target!.Append(value), initialLength, valueObject, length, 0, length);
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char[])")]
    public static StringBuilder Append(StringBuilder target, char[]? value)
    {
        var initialLength = target.Length;
        var length = value?.Length ?? 0;
        return StringBuilderModuleImpl.OnStringBuilderAppend(target.Append(value), initialLength, value, length, 0, length);
    }

    /// <summary>  StringBuilder.AppendLine aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <returns> instance.AppendLine() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendLine(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder AppendLine(StringBuilder target, string? value)
    {
        var initialLength = target.Length;
        var length = value?.Length ?? 0;
        // We do not take into account the endline char because it is not tainted
        return StringBuilderModuleImpl.OnStringBuilderAppend(target.AppendLine(value), initialLength, value, length, 0, length);
    }
}
