// <copyright file="StringBuilderAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects.System.Text;

/// <summary> StringBuilder class aspects </summary>
[AspectClass("mscorlib,netstandard,System.Runtime")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class StringBuilderAspects
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringBuilderAspects));

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <returns> New StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string? value)
    {
        var result = new StringBuilder(value);
        try
        {
            PropagationModuleImpl.PropagateTaint(value, result);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Init)}");
        }

        return result;
    }

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <param name="capacity"> StringBuilder initial capacity </param>
    /// <returns> Nerw StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string? value, int capacity)
    {
        var result = new StringBuilder(value, capacity);
        try
        {
            PropagationModuleImpl.PropagateTaint(value, result);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Init)}");
        }

        return result;
    }

    /// <summary> StringBuildr ctor aspect </summary>
    /// <param name="value"> Init string </param>
    /// <param name="startIndex"> string startIndex </param>
    /// <param name="length"> string length </param>
    /// <param name="capacity"> StringBuilder initial capacity </param>
    /// <returns> Nerw StringBuilder </returns>
    [AspectCtorReplace("System.Text.StringBuilder::.ctor(System.String,System.Int32,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Init(string? value, int startIndex, int length, int capacity)
    {
        var result = new StringBuilder(value, startIndex, length, capacity);
        try
        {
            StringBuilderModuleImpl.OnStringBuilderSubSequence(value, startIndex, length, result);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Init)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.ToString aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <returns> instance.ToString() </returns>
    [AspectMethodReplace("System.Object::ToString()", "System.Text.StringBuilder")]
    public static string? ToString(object? target)
    {
        var result = target!.ToString();
        try
        {
            if (target is StringBuilder)
            {
                PropagationModuleImpl.PropagateTaint(target, result);
                PropagationModuleImpl.FixRangesIfNeeded(result);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(ToString)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.ToString aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="length"> length parameter </param>
    /// <returns> instance.ToString() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::ToString(System.Int32,System.Int32)")]
    public static string ToString(StringBuilder? target, int startIndex, int length)
    {
        var result = target!.ToString(startIndex, length);
        try
        {
            PropagationModuleImpl.OnStringSubSequence(target, startIndex, result, result.Length);
            PropagationModuleImpl.FixRangesIfNeeded(result);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(ToString)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> value parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Append(StringBuilder? target, string? value)
    {
        var result = target!.Append(value);
        try
        {
            if (target is not null && value is not null)
            {
                var length = value.Length;
                var initialLength = target.Length - length;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, length, 0, length);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }

#if !NETFRAMEWORK
    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> value parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Text.StringBuilder)")]
    public static StringBuilder Append(StringBuilder? target, StringBuilder? value)
    {
        var result = target!.Append(value);
        try
        {
            if (target is not null && value is not null)
            {
                var length = value.Length;
                var initialLength = target.Length - length;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, length, 0, length);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }
#endif

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="count"> count parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.String,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Append(StringBuilder? target, string? value, int startIndex, int count)
    {
        var result = target!.Append(value, startIndex, count);
        try
        {
            if (target is not null && value is not null)
            {
                var initialLength = target.Length - count;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, value.Length, startIndex, count);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }

#if NETCOREAPP
    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="count"> count parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Text.StringBuilder,System.Int32,System.Int32)", AspectFilter.StringLiteral_1)]
    public static StringBuilder Append(StringBuilder? target, StringBuilder? value, int startIndex, int count)
    {
        var result = target!.Append(value, startIndex, count);
        try
        {
            if (target is not null && value is not null)
            {
                var initialLength = target.Length - count;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, value.Length, startIndex, count);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }
#endif

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="charCount"> charCount parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char[],System.Int32,System.Int32)")]
    public static StringBuilder Append(StringBuilder? target, char[]? value, int startIndex, int charCount)
    {
        var result = target!.Append(value, startIndex, charCount);
        try
        {
            if (target is not null && value is not null)
            {
                var initialLength = target.Length - charCount;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, value.Length, startIndex, charCount);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Object)")]
    public static StringBuilder Append(StringBuilder? target, object? value)
    {
        var result = target!.Append(value);
        try
        {
            if (target is not null && value is not null)
            {
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

                var initialLength = target.Length - length;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, valueObject, length, 0, length);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Append aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <returns> instance.Append() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::Append(System.Char[])")]
    public static StringBuilder Append(StringBuilder? target, char[]? value)
    {
        var result = target!.Append(value);
        try
        {
            if (target is not null && value is not null)
            {
                var length = value.Length;
                var initialLength = target.Length - length;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, length, 0, length);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Append)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendLine aspect </summary>
    /// <param name="target"> StringBuilder instance </param>
    /// <param name="value"> string parameter </param>
    /// <returns> instance.AppendLine() </returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendLine(System.String)", AspectFilter.StringLiteral_1)]
    public static StringBuilder AppendLine(StringBuilder? target, string? value)
    {
        var result = target!.AppendLine(value);
        try
        {
            // We do not take into account the endline char because it is not tainted
            if (target is not null && value is not null)
            {
                var length = value.Length;
                var initialLength = target.Length - length - Environment.NewLine.Length;
                return StringBuilderModuleImpl.OnStringBuilderAppend(result, initialLength, value, length, 0, length);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendLine)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="arg0">An object to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object)")]
    public static StringBuilder AppendFormat(StringBuilder? target, string? format, object? arg0)
    {
        var result = target!.AppendFormat(format!, arg0);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, format, arg0);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="arg0">The first object to format and append.</param>
    /// <param name="arg1">The second object to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object,System.Object)")]
    public static StringBuilder AppendFormat(StringBuilder? target, string? format, object? arg0, object? arg1)
    {
        var result = target!.AppendFormat(format!, arg0, arg1);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, format, arg0, arg1);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="arg0">The first object to format and append.</param>
    /// <param name="arg1">The second object to format and append.</param>
    /// <param name="arg2">The third object to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object,System.Object,System.Object)")]
    public static StringBuilder AppendFormat(StringBuilder? target, string? format, object? arg0, object? arg1, object? arg2)
    {
        var result = target!.AppendFormat(format!, arg0, arg1, arg2);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, format, arg0, arg1, arg2);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.String,System.Object[])")]
    public static StringBuilder AppendFormat(StringBuilder? target, string? format, object[]? args)
    {
        var result = target!.AppendFormat(format!, args!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, format, args);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="arg0">An object to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object)")]
    public static StringBuilder AppendFormat(StringBuilder? target, IFormatProvider? provider, string? format, object? arg0)
    {
        var result = target!.AppendFormat(provider, format!, arg0);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, format, arg0);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="arg0">The first object to format and append.</param>
    /// <param name="arg1">The second object to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object,System.Object)")]
    public static StringBuilder AppendFormat(StringBuilder? target, IFormatProvider? provider, string? format, object? arg0, object? arg1)
    {
        var result = target!.AppendFormat(provider, format!, arg0, arg1);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, format, arg0, arg1);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="arg0">The first object to format and append.</param>
    /// <param name="arg1">The second object to format and append.</param>
    /// <param name="arg2">The third object to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object,System.Object,System.Object)")]
    public static StringBuilder AppendFormat(StringBuilder? target, IFormatProvider? provider, string? format, object? arg0, object? arg1, object? arg2)
    {
        var result = target!.AppendFormat(provider, format!, arg0, arg1, arg2);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, format, arg0, arg1, arg2);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.AppendFormat aspect </summary>
    /// <param name="target">StringBuilder instance to append to.</param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A composite format string.</param>
    /// <param name="args">An array of objects to format and append.</param>
    /// <returns>A reference to the StringBuilder after the append operation has occurred.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendFormat(System.IFormatProvider,System.String,System.Object[])")]
    public static StringBuilder AppendFormat(StringBuilder? target, IFormatProvider? provider, string? format, object[]? args)
    {
        var result = target!.AppendFormat(provider, format!, args!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, format, args);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendFormat)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.CopyTo aspect </summary>
    /// <param name="target">The StringBuilder instance from which characters are copied.</param>
    /// <param name="sourceIndex">The index in this instance at which copying begins.</param>
    /// <param name="destination">The destination character array.</param>
    /// <param name="destinationIndex">The index in destination at which storing begins.</param>
    /// <param name="count">The number of characters to copy.</param>
    [AspectMethodReplace("System.Text.StringBuilder::CopyTo(System.Int32,System.Char[],System.Int32,System.Int32)")]
    public static void CopyTo(StringBuilder? target, int sourceIndex, char[]? destination, int destinationIndex, int count)
    {
        target!.CopyTo(sourceIndex, destination!, destinationIndex, count);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(destination, target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(CopyTo)}");
        }
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the string is inserted.</param>
    /// <param name="index">The index at which the string is inserted.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.String)")]
    public static StringBuilder Insert(StringBuilder? target, int index, string? value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null && value is not null)
            {
                var previousLength = target.Length - value.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the string is inserted.</param>
    /// <param name="index">The index at which the string is inserted.</param>
    /// <param name="value">The string to insert.</param>
    /// <param name="count">The number of copies of the string to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.String,System.Int32)")]
    public static StringBuilder Insert(StringBuilder? target, int index, string? value, int count)
    {
        var result = target!.Insert(index, value, count);
        try
        {
            if (target is not null && value is not null && count > 0)
            {
                var previousLength = target.Length - (value.Length * count);
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value, count);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the character is inserted.</param>
    /// <param name="index">The index at which the character is inserted.</param>
    /// <param name="value">The character to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Char)")]
    public static StringBuilder Insert(StringBuilder? target, int index, char value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var previousLength = target.Length - 1;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the characters are inserted.</param>
    /// <param name="index">The index at which the characters are inserted.</param>
    /// <param name="value">The character array to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Char[])")]
    public static StringBuilder Insert(StringBuilder? target, int index, char[]? value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null && value is not null)
            {
                var previousLength = target.Length - value.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the characters are inserted.</param>
    /// <param name="index">The index at which the characters are inserted.</param>
    /// <param name="value">The character array containing the characters to insert.</param>
    /// <param name="startIndex">The starting index in the character array.</param>
    /// <param name="charCount">The number of characters to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Char[],System.Int32,System.Int32)")]
    public static StringBuilder Insert(StringBuilder? target, int index, char[]? value, int startIndex, int charCount)
    {
        var result = target!.Insert(index, value, startIndex, charCount);
        try
        {
            if (target is not null && value is not null)
            {
                var previousLength = target.Length - charCount;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value, 1, startIndex, charCount);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the integer is inserted.</param>
    /// <param name="index">The index at which the integer is inserted.</param>
    /// <param name="value">The integer to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Int32)")]
    public static StringBuilder Insert(StringBuilder? target, int index, int value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the long integer is inserted.</param>
    /// <param name="index">The index at which the long integer is inserted.</param>
    /// <param name="value">The long integer to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Int64)")]
    public static StringBuilder Insert(StringBuilder? target, int index, long value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the single-precision floating-point number is inserted.</param>
    /// <param name="index">The index at which the single-precision floating-point number is inserted.</param>
    /// <param name="value">The single-precision floating-point number to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Single)")]
    public static StringBuilder Insert(StringBuilder? target, int index, float value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the double-precision floating-point number is inserted.</param>
    /// <param name="index">The index at which the double-precision floating-point number is inserted.</param>
    /// <param name="value">The double-precision floating-point number to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Double)")]
    public static StringBuilder Insert(StringBuilder? target, int index, double value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the decimal number is inserted.</param>
    /// <param name="index">The index at which the decimal number is inserted.</param>
    /// <param name="value">The decimal number to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Decimal)")]
    public static StringBuilder Insert(StringBuilder? target, int index, decimal value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the unsigned short integer is inserted.</param>
    /// <param name="index">The index at which the unsigned short integer is inserted.</param>
    /// <param name="value">The unsigned short integer to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.UInt16)")]
    public static StringBuilder Insert(StringBuilder? target, int index, ushort value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the unsigned integer is inserted.</param>
    /// <param name="index">The index at which the unsigned integer is inserted.</param>
    /// <param name="value">The unsigned integer to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.UInt32)")]
    public static StringBuilder Insert(StringBuilder? target, int index, uint value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the unsigned long integer is inserted.</param>
    /// <param name="index">The index at which the unsigned long integer is inserted.</param>
    /// <param name="value">The unsigned long integer to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.UInt64)")]
    public static StringBuilder Insert(StringBuilder? target, int index, ulong value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the Boolean value is inserted.</param>
    /// <param name="index">The index at which the Boolean value is inserted.</param>
    /// <param name="value">The Boolean value to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Boolean)")]
    public static StringBuilder Insert(StringBuilder? target, int index, bool value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the signed byte is inserted.</param>
    /// <param name="index">The index at which the signed byte is inserted.</param>
    /// <param name="value">The signed byte to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.SByte)")]
    public static StringBuilder Insert(StringBuilder? target, int index, sbyte value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the byte is inserted.</param>
    /// <param name="index">The index at which the byte is inserted.</param>
    /// <param name="value">The byte to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Byte)")]
    public static StringBuilder Insert(StringBuilder? target, int index, byte value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the short integer is inserted.</param>
    /// <param name="index">The index at which the short integer is inserted.</param>
    /// <param name="value">The short integer to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Int16)")]
    public static StringBuilder Insert(StringBuilder? target, int index, short value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>  StringBuilder.Insert aspect </summary>
    /// <param name="target">The StringBuilder instance into which the object is inserted.</param>
    /// <param name="index">The index at which the object is inserted.</param>
    /// <param name="value">The object to insert.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Insert(System.Int32,System.Object)")]
    public static StringBuilder Insert(StringBuilder? target, int index, object? value)
    {
        var result = target!.Insert(index, value);
        try
        {
            if (target is not null && value is not null)
            {
                var val = value.ToString();
                var previousLength = target.Length - val!.Length;
                StringBuilderModuleImpl.OnStringBuilderInsert(target!, previousLength, index, value);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Insert)}");
        }

        return result;
    }

    /// <summary>StringBuilder.Remove aspect</summary>
    /// <param name="target">The StringBuilder instance from which characters are removed.</param>
    /// <param name="startIndex">The starting index of the removal.</param>
    /// <param name="length">The number of characters to remove.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Remove(System.Int32,System.Int32)")]
    public static StringBuilder Remove(StringBuilder? target, int startIndex, int length)
    {
        var result = target!.Remove(startIndex, length);
        try
        {
            PropagationModuleImpl.OnStringRemove(target, result, startIndex, startIndex + length);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Remove)}");
        }

        return result;
    }

    /// <summary>StringBuilder.Replace aspect</summary>
    /// <param name="target">The StringBuilder instance in which the replacement occurs.</param>
    /// <param name="oldValue">The value to be replaced.</param>
    /// <param name="newValue">The replacement value.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Replace(System.String,System.String)")]
    public static StringBuilder Replace(StringBuilder? target, string? oldValue, string? newValue)
    {
        var result = target!.Replace(oldValue!, newValue);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, oldValue, newValue);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Replace)}");
        }

        return result;
    }

    /// <summary>StringBuilder.Replace aspect</summary>
    /// <param name="target">The StringBuilder instance in which the replacement occurs.</param>
    /// <param name="oldValue">The value to be replaced.</param>
    /// <param name="newValue">The replacement value.</param>
    /// <param name="startIndex">The starting index of the replacement.</param>
    /// <param name="count">The number of characters to be replaced.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Replace(System.String,System.String,System.Int32,System.Int32)")]
    public static StringBuilder Replace(StringBuilder? target, string? oldValue, string? newValue, int startIndex, int count)
    {
        var result = target!.Replace(oldValue!, newValue, startIndex, count);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target, oldValue, newValue);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Replace)}");
        }

        return result;
    }

    /// <summary>StringBuilder.Replace aspect</summary>
    /// <param name="target">The StringBuilder instance in which the replacement occurs.</param>
    /// <param name="oldChar">The character to be replaced.</param>
    /// <param name="newChar">The replacement character.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Replace(System.Char,System.Char)")]
    public static StringBuilder Replace(StringBuilder? target, char oldChar, char newChar)
    {
        var result = target!.Replace(oldChar, newChar);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Replace)}");
        }

        return result;
    }

    /// <summary>StringBuilder.Replace aspect</summary>
    /// <param name="target">The StringBuilder instance in which the replacement occurs.</param>
    /// <param name="oldChar">The character to be replaced.</param>
    /// <param name="newChar">The replacement character.</param>
    /// <param name="startIndex">The starting index of the replacement.</param>
    /// <param name="count">The number of characters to be replaced.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::Replace(System.Char,System.Char,System.Int32,System.Int32)")]
    public static StringBuilder Replace(StringBuilder? target, char oldChar, char newChar, int startIndex, int count)
    {
        var result = target!.Replace(oldChar, newChar, startIndex, count);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(Replace)}");
        }

        return result;
    }

    /// <summary>StringBuilder.set_Length aspect</summary>
    /// <param name="target">The StringBuilder instance for which the length is set.</param>
    /// <param name="length">The new length of the StringBuilder instance.</param>
    [AspectMethodReplace("System.Text.StringBuilder::set_Length(System.Int32)")]
    public static void SetLength(StringBuilder? target, int length)
    {
        target!.Length = length;
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTainted(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(SetLength)}");
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>StringBuilder.AppendJoin aspect</summary>
    /// <param name="target">The StringBuilder instance.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array that contains the strings to concatenate and append to the current instance of the string builder.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendJoin(System.String,System.String[])")]
    public static StringBuilder AppendJoin(StringBuilder? target, string? separator, string[]? values)
    {
        var result = target!.AppendJoin(separator, values!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, separator, values);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendJoin)}");
        }

        return result;
    }

    /// <summary>StringBuilder.AppendJoin aspect</summary>
    /// <param name="target">The StringBuilder instance.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array that contains the strings to concatenate and append to the current instance of the string builder.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendJoin(System.String,System.Object[])")]
    public static StringBuilder AppendJoin(StringBuilder? target, string? separator, object[]? values)
    {
        var result = target!.AppendJoin(separator, values!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, separator, values);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendJoin)}");
        }

        return result;
    }

    /// <summary>StringBuilder.AppendJoin aspect</summary>
    /// <param name="target">The StringBuilder instance.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array that contains the strings to concatenate and append to the current instance of the string builder.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendJoin(System.Char,System.String[])")]
    public static StringBuilder AppendJoin(StringBuilder? target, char separator, string[]? values)
    {
        var result = target!.AppendJoin(separator, values!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, null, values);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendJoin)}");
        }

        return result;
    }

    /// <summary>StringBuilder.AppendJoin aspect</summary>
    /// <param name="target">The StringBuilder instance.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array that contains the strings to concatenate and append to the current instance of the string builder.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplace("System.Text.StringBuilder::AppendJoin(System.Char,System.Object[])")]
    public static StringBuilder AppendJoin(StringBuilder? target, char separator, object[]? values)
    {
        var result = target!.AppendJoin(separator, values!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, null, values);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendJoin)}");
        }

        return result;
    }

    /// <summary>StringBuilder.AppendJoin aspect</summary>
    /// <typeparam name="T"> Joined element type </typeparam>
    /// <param name="target">The StringBuilder instance.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array that contains the strings to concatenate and append to the current instance of the string builder.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplaceFromVersion("3.2.0", "System.Text.StringBuilder::AppendJoin(System.Char,System.Collections.Generic.IEnumerable`1<!!0>)")]
    public static StringBuilder AppendJoin<T>(StringBuilder? target, char separator, IEnumerable<T>? values)
    {
        var result = target!.AppendJoin(separator, values!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, null, values);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendJoin)}");
        }

        return result;
    }

    /// <summary>StringBuilder.AppendJoin aspect</summary>
    /// <typeparam name="T"> Joined element type </typeparam>
    /// <param name="target">The StringBuilder instance.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array that contains the strings to concatenate and append to the current instance of the string builder.</param>
    /// <returns>The modified StringBuilder instance.</returns>
    [AspectMethodReplaceFromVersion("3.2.0", "System.Text.StringBuilder::AppendJoin(System.String,System.Collections.Generic.IEnumerable`1<!!0>)")]
    public static StringBuilder AppendJoin<T>(StringBuilder? target, string separator, IEnumerable<T>? values)
    {
        var result = target!.AppendJoin(separator, values!);
        try
        {
            StringBuilderModuleImpl.FullTaintIfAnyTaintedEnumerable(target, separator, values);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StringBuilderAspects)}.{nameof(AppendJoin)}");
        }

        return result;
    }

#endif
}
