// <copyright file="StringAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Logging;
using static Datadog.Trace.Iast.Propagation.StringModuleImpl;

namespace Datadog.Trace.Iast.Aspects.System;

/// <summary> String class aspects </summary>
[AspectClass("mscorlib,netstandard,System.Private.CoreLib,System.Runtime", AspectFilter.StringOptimization)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class StringAspects
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringAspects));

    /// <summary>
    /// String.Trim aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <returns> String.Trim() </returns>
    [AspectMethodReplace("System.String::Trim()", AspectFilter.StringLiteral_0)]
    public static string Trim(string target)
    {
        return StringModuleImpl.OnStringTrim(target, target.Trim(), null, true, true);
    }

    /// <summary>
    /// String.Trim aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="trimChars"> chars to trim </param>
    /// <returns> String.Trim() </returns>
    [AspectMethodReplace("System.String::Trim(System.Char[])", AspectFilter.StringLiteral_0)]
    public static string Trim(string target, char[] trimChars)
    {
        if (trimChars != null && trimChars.Length > 0)
        {
            return StringModuleImpl.OnStringTrimArray(target, target.Trim(trimChars), trimChars, true, true);
        }
        else
        {
            return StringModuleImpl.OnStringTrim(target, target.Trim(trimChars), null, true, true);
        }
    }

#if !NETFRAMEWORK
    /// <summary>
    /// String.Trim aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="trimChar"> char to trim </param>
    /// <returns> String.Trim() </returns>
    [AspectMethodReplace("System.String::Trim(System.Char)", AspectFilter.StringLiteral_0)]
    public static string Trim(string target, char trimChar)
    {
        return StringModuleImpl.OnStringTrim(target, target.Trim(trimChar), trimChar, true, true);
    }
#endif

    /// <summary>
    /// String.TrimStart aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="trimChars"> chars to trim </param>
    /// <returns> String.TrimStart() </returns>
    [AspectMethodReplace("System.String::TrimStart(System.Char[])", AspectFilter.StringLiteral_0)]
    public static string TrimStart(string target, char[] trimChars)
    {
        if (trimChars != null && trimChars.Length > 0)
        {
            return StringModuleImpl.OnStringTrimArray(target, target.TrimStart(trimChars), trimChars, true, false);
        }
        else
        {
            return StringModuleImpl.OnStringTrim(target, target.TrimStart(trimChars), null, true, false);
        }
    }

#if !NETFRAMEWORK
    /// <summary>
    /// String.TrimStart aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="trimChar"> char to trim </param>
    /// <returns> String.TrimStart() </returns>
    [AspectMethodReplace("System.String::TrimStart(System.Char)", AspectFilter.StringLiteral_0)]
    public static string TrimStart(string target, char trimChar)
    {
        return StringModuleImpl.OnStringTrim(target, target.TrimStart(trimChar), trimChar, true, false);
    }

    /// <summary>
    /// String.TrimStart aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <returns> String.TrimStart() </returns>
    [AspectMethodReplace("System.String::TrimStart()", AspectFilter.StringLiteral_0)]
    public static string TrimStart(string target)
    {
        return StringModuleImpl.OnStringTrim(target, target.TrimStart(), null, true, false);
    }
#endif

    /// <summary>
    /// String.TrimEnd aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="trimChars"> chars to trim </param>
    /// <returns> String.TrimEnd() </returns>
    [AspectMethodReplace("System.String::TrimEnd(System.Char[])", AspectFilter.StringLiteral_0)]
    public static string TrimEnd(string target, char[] trimChars)
    {
        if (trimChars != null && trimChars.Length > 0)
        {
            return StringModuleImpl.OnStringTrimArray(target, target.TrimEnd(trimChars), trimChars, false, true);
        }
        else
        {
            return StringModuleImpl.OnStringTrim(target, target.TrimEnd(trimChars), null, false, true);
        }
    }

#if !NETFRAMEWORK
    /// <summary>
    /// String.TrimEnd aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="trimChar"> char to trim </param>
    /// <returns> String.TrimEnd() </returns>
    [AspectMethodReplace("System.String::TrimEnd(System.Char)", AspectFilter.StringLiteral_0)]
    public static string TrimEnd(string target, char trimChar)
    {
        return StringModuleImpl.OnStringTrim(target, target.TrimEnd(trimChar), trimChar, false, true);
    }

    /// <summary>
    /// String.TrimEnd aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <returns> String.TrimEnd() </returns>
    [AspectMethodReplace("System.String::TrimEnd()", AspectFilter.StringLiteral_0)]
    public static string TrimEnd(string target)
    {
        return StringModuleImpl.OnStringTrim(target, target.TrimEnd(), null, false, true);
    }
#endif

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <returns> String.Concat(param1, param2) </returns>
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string param1, string param2)
    {
        return StringModuleImpl.OnStringConcat(param1, param2, string.Concat(param1, param2));
    }

    /// <summary>
    /// String.Concat aspect optimization for literals
    /// </summary>
    /// <param name="param1"> First param (literal) </param>
    /// <param name="param2"> Second param </param>
    /// <returns> String.Concat(param1, param2) </returns>
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiteral_0)]
    public static string Concat_0(string param1, string param2)
    {
        return StringModuleImpl.OnStringConcat(param1, param2, string.Concat(param1, param2), AspectFilter.StringLiteral_0);
    }

    /// <summary>
    /// String.Concat aspect optimization for literals
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param (literal) </param>
    /// <returns> String.Concat(param1, param2) </returns>
    [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiteral_1)]
    public static string Concat_1(string param1, string param2)
    {
        return StringModuleImpl.OnStringConcat(param1, param2, string.Concat(param1, param2), AspectFilter.StringLiteral_1);
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <returns> String.Concat(param1, param2) </returns>
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object)")]
    public static string Concat(object param1, object param2)
    {
        return StringModuleImpl.OnStringConcat(param1?.ToString(), param2?.ToString(), string.Concat(param1, param2));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <param name="param3"> Third param </param>
    /// <returns> String.Concat(param1, param2, param3) </returns>
    [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String)", AspectFilter.StringLiterals)]
    public static string Concat(string param1, string param2, string param3)
    {
        return StringModuleImpl.OnStringConcat(new StringConcatParams(param1, param2, param3), string.Concat(param1, param2, param3));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <param name="param3"> Third param </param>
    /// <returns> String.Concat(param1, param2, param3) </returns>
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)")]
    public static string Concat(object param1, object param2, object param3)
    {
        return StringModuleImpl.OnStringConcat(new StringConcatParams(param1?.ToString(), param2?.ToString(), param3?.ToString()), string.Concat(param1, param2, param3));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <param name="param3"> Third param </param>
    /// <param name="param4"> Fourth param </param>
    /// <returns> String.Concat(param1, param2, param3, param4) </returns>
    [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String,System.String)", AspectFilter.StringLiterals)]
    public static string Concat(string param1, string param2, string param3, string param4)
    {
        return StringModuleImpl.OnStringConcat(new StringConcatParams(param1, param2, param3, param4), string.Concat(param1, param2, param3, param4));
    }

#if NETFRAMEWORK
    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <param name="param3"> Third param </param>
    /// <param name="param4"> Fourth param </param>
    /// <returns> String.Concat(param1, param2, param3, param4) </returns>
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object,System.Object)")]
    public static string Concat(object param1, object param2, object param3, object param4)
    {
        return StringModuleImpl.OnStringConcat(new StringConcatParams(param1?.ToString(), param2?.ToString(), param3?.ToString(), param4?.ToString()), string.Concat(param1, param2, param3, param4));
    }
#endif

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="values"> Parameters </param>
    /// <returns> String.Concat(values) </returns>
    [AspectMethodReplace("System.String::Concat(System.String[])")]
    public static string Concat(string[] values)
    {
        return StringModuleImpl.OnStringConcat(values, string.Concat(values));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="values"> Parameters </param>
    /// <returns> String.Concat(values) </returns>
    [AspectMethodReplace("System.String::Concat(System.Object[])")]
    public static string Concat(object[] values)
    {
        return StringModuleImpl.OnStringConcat(values, string.Concat(values));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="values"> Parameters </param>
    /// <returns> String.Concat(values) </returns>
    [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<System.String>)")]
    public static string Concat(IEnumerable values)
    {
        var valuesConverted = values as IEnumerable<string>;
        return StringModuleImpl.OnStringConcat(valuesConverted, string.Concat(valuesConverted));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="values"> Parameters </param>
    /// <returns> String.Concat(values) </returns>
    [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<!!0>)")]
    public static string Concat2(IEnumerable values)
    {
        if (values is null)
        {
            return string.Concat(values);
        }

        var valuesConverted = values as IEnumerable<object>;
        if (valuesConverted != null)
        {
            return StringModuleImpl.OnStringConcat(valuesConverted, string.Concat(valuesConverted));
        }

        // We have a IEnumerable of structs or basic types. This is a corner case.

        try
        {
            valuesConverted = values.Cast<object>();
        }
        catch
        {
            // This sould never happen.
            Log.Warning("Cannot process values in System.String::Concat(System.Collections.Generic.IEnumerable`1<!!0>)");
            return string.Concat(values);
        }

        return StringModuleImpl.OnStringConcat(values, string.Concat(valuesConverted));
    }

    /// <summary>
    /// String.Substring aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <param name="startIndex"> the starting index </param>
    /// <returns> target.Substring(startIndex) </returns>
    [AspectMethodReplace("System.String::Substring(System.Int32)", AspectFilter.StringLiteral_0)]
    public static string Substring(string target, int startIndex)
    {
        return StringModuleImpl.OnStringSubSequence(target, startIndex, target.Substring(startIndex));
    }

    /// <summary>
    /// String.Substring aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <param name="startIndex"> the starting index </param>
    /// <param name="length"> the length </param>
    /// <returns> target.Substring(beginIndex) </returns>
    [AspectMethodReplace("System.String::Substring(System.Int32,System.Int32)", AspectFilter.StringLiteral_0)]
    public static string Substring(string target, int startIndex, int length)
    {
        return StringModuleImpl.OnStringSubSequence(target, startIndex, target.Substring(startIndex, length));
    }

    /// <summary>
    /// String.ToCharArray aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <returns> String.ToCharArray() </returns>
    [AspectMethodReplace("System.String::ToCharArray()", AspectFilter.StringLiteral_0)]
    public static char[] ToCharArray(string target)
    {
        var result = target.ToCharArray();
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.ToCharArray aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="length"> length parameter </param>
    /// <returns> String.ToCharArray(System.Int32,System.Int32) </returns>
    [AspectMethodReplace("System.String::ToCharArray(System.Int32,System.Int32)", AspectFilter.StringLiteral_0)]
    public static char[] ToCharArray(string target, int startIndex, int length)
    {
        return StringModuleImpl.OnStringSubSequence(target, startIndex, target.ToCharArray(startIndex, length));
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <param name="startIndex"> start index </param>
    /// <param name="count"> number of elemnts to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.String,System.String[],System.Int32,System.Int32)")]
    public static string Join(string separator, string[] values, int startIndex, int count)
    {
        return OnStringJoin(string.Join(separator, values, startIndex, count), separator, values, startIndex, count);
    }

#if NETSTANDARD || NETCOREAPP
    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.Char,System.String[])")]
    public static string Join(char separator, string[] values)
    {
        return OnStringJoin(string.Join(separator.ToString(), values), values);
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.Char,System.Object[])")]
    public static string Join(char separator, object[] values)
    {
        return Join(separator.ToString(), values);
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <param name="startIndex"> start index </param>
    /// <param name="count"> number of elemnts to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.Char,System.String[],System.Int32,System.Int32)")]
    public static string Join(char separator, string[] values, int startIndex, int count)
    {
        return OnStringJoin(string.Join(separator.ToString(), values, startIndex, count), values, startIndex, count);
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.Char,System.Collections.Generic.IEnumerable`1<!!0>)")]
    public static string Join(char separator, IEnumerable values)
    {
        return Join(separator.ToString(), values);
    }
#endif

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.String,System.Object[])")]
    public static string Join(string separator, object[] values)
    {
        return OnStringJoin(string.Join(separator, values), separator, values);
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.String,System.String[])")]
    public static string Join(string separator, string[] values)
    {
        return OnStringJoin(string.Join(separator, values), separator, values);
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.String,System.Collections.Generic.IEnumerable`1<System.String>)")]
    public static string JoinString(string separator, IEnumerable values)
    {
        if (values is null)
        {
            return OnStringJoin(string.Join(separator, values), separator, null);
        }

        var valuesConverted = values as IEnumerable<string>;
        if (valuesConverted != null)
        {
            return OnStringJoin(string.Join(separator, valuesConverted), separator, valuesConverted);
        }
        else
        {
            // This should never happen
            Log.Warning("Could not taint the string.join call in System.String::Join(System.String,System.Collections.Generic.IEnumerable`1<System.String>)");
            return string.Join(separator, values);
        }
    }

    /// <summary>
    /// String.Join aspect
    /// </summary>
    /// <param name="separator"> sparator </param>
    /// <param name="values"> values to join </param>
    /// <returns> Join result </returns>
    [AspectMethodReplace("System.String::Join(System.String,System.Collections.Generic.IEnumerable`1<!!0>)")]
    public static string Join(string separator, IEnumerable values)
    {
        if (values is null)
        {
            return OnStringJoin(string.Join(separator, values), separator, null);
        }

        var valuesConverted = values as IEnumerable<object>;
        if (valuesConverted != null)
        {
            return OnStringJoin(string.Join(separator, valuesConverted), separator, valuesConverted);
        }

        // We have a IEnumerable of structs or basic types. This is a corner case.
        try
        {
            valuesConverted = values.Cast<object>();
        }
        catch
        {
            // This sould never happen, but just in case, we return the join...
            Log.Warning("Could not taint the string.join call in System.String::Join(System.String,System.Collections.Generic.IEnumerable`1<!!0>)");
            return string.Join(separator, values);
        }

        return OnStringJoin(string.Join(separator, valuesConverted), separator, valuesConverted);
    }

    /// <summary>
    /// String.ToUpper aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <returns> ToUpper result </returns>
    [AspectMethodReplace("System.String::ToUpper()", AspectFilter.StringLiteral_0)]
    public static string ToUpper(string target)
    {
        var result = target.ToUpper();
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.ToUpper aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <param name="culture"> the CultureInfo parameter </param>
    /// <returns> ToUpper result </returns>
    [AspectMethodReplace("System.String::ToUpper(System.Globalization.CultureInfo)", AspectFilter.StringLiteral_0)]
    public static string ToUpper(string target, global::System.Globalization.CultureInfo culture)
    {
        var result = target.ToUpper(culture);
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.ToUpperInvariant aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <returns> ToUpperInvariant result </returns>
    [AspectMethodReplace("System.String::ToUpperInvariant()", AspectFilter.StringLiteral_0)]
    public static string ToUpperInvariant(string target)
    {
        var result = target.ToUpperInvariant();
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.ToLower aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <returns> ToLower result </returns>
    [AspectMethodReplace("System.String::ToLower()", AspectFilter.StringLiteral_0)]
    public static string ToLower(string target)
    {
        var result = target.ToLower();
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.ToLower aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <param name="culture"> the CultureInfo parameter </param>
    /// <returns> ToLower result </returns>
    [AspectMethodReplace("System.String::ToLower(System.Globalization.CultureInfo)", AspectFilter.StringLiteral_0)]
    public static string ToLower(string target, global::System.Globalization.CultureInfo culture)
    {
        var result = target.ToLower(culture);
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.ToLowerInvariant aspect
    /// </summary>
    /// <param name="target"> the target string </param>
    /// <returns> ToLowerInvariant result </returns>
    [AspectMethodReplace("System.String::ToLowerInvariant()", AspectFilter.StringLiteral_0)]
    public static string ToLowerInvariant(string target)
    {
        var result = target.ToLowerInvariant();
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.Remove aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <returns> String.Remove() </returns>
    [AspectMethodReplace("System.String::Remove(System.Int32)", AspectFilter.StringLiteral_0)]
    public static string Remove(string target, int startIndex)
    {
        string result = target.Remove(startIndex);
        PropagationModuleImpl.OnStringRemove(target, result, startIndex, target.Length);
        return result;
    }

    /// <summary>
    /// String.Remove aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="count"> count parameter </param>
    /// <returns> String.Remove() </returns>
    [AspectMethodReplace("System.String::Remove(System.Int32,System.Int32)", AspectFilter.StringLiteral_0)]
    public static string Remove(string target, int startIndex, int count)
    {
        string result = target.Remove(startIndex, count);
        PropagationModuleImpl.OnStringRemove(target, result, startIndex, startIndex + count);
        return result;
    }

    /// <summary>
    /// String.Insert aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="startIndex"> startIndex parameter </param>
    /// <param name="value"> value to insert </param>
    /// <returns> String.Insert() </returns>
    [AspectMethodReplace("System.String::Insert(System.Int32,System.String)", AspectFilter.StringOptimization)]
    public static string Insert(string target, int startIndex, string value)
    {
        var result = target.Insert(startIndex, value);
        OnStringInsert(target, startIndex, value, result);
        return result;
    }

    /// <summary>
    /// String.PadLeft aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="totalWidth"> totalWidth parameter </param>
    /// <returns> String.PadLeft() </returns>
    [AspectMethodReplace("System.String::PadLeft(System.Int32)", AspectFilter.StringLiteral_0)]
    public static string PadLeft(string target, int totalWidth)
    {
        var result = target.PadLeft(totalWidth);
        PropagationModuleImpl.PropagateTaint(target, result, (result?.Length - target?.Length) ?? 0);
        return result;
    }

    /// <summary>
    /// String.PadLeft aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="totalWidth"> totalWidth parameter </param>
    /// <param name="paddingChar"> paddingChar parameter </param>
    /// <returns> String.PadLeft() </returns>
    [AspectMethodReplace("System.String::PadLeft(System.Int32,System.Char)", AspectFilter.StringLiteral_0)]
    public static string PadLeft(string target, int totalWidth, char paddingChar)
    {
        var result = target.PadLeft(totalWidth, paddingChar);
        PropagationModuleImpl.PropagateTaint(target, result, (result?.Length - target?.Length) ?? 0);
        return result;
    }

    /// <summary>
    /// String.PadRight aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="totalWidth"> totalWidth parameter </param>
    /// <returns> String.PadRight() </returns>
    [AspectMethodReplace("System.String::PadRight(System.Int32)", AspectFilter.StringLiteral_0)]
    public static string PadRight(string target, int totalWidth)
    {
        var result = target.PadRight(totalWidth);
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.PadRight aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="totalWidth"> totalWidth parameter </param>
    /// <param name="paddingChar"> paddingChar parameter </param>
    /// <returns> String.PadRight() </returns>
    [AspectMethodReplace("System.String::PadRight(System.Int32,System.Char)", AspectFilter.StringLiteral_0)]
    public static string PadRight(string target, int totalWidth, char paddingChar)
    {
        var result = target.PadRight(totalWidth, paddingChar);
        PropagationModuleImpl.PropagateTaint(target, result);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="format"> format of the string </param>
    /// <param name="arg0"> format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.String,System.Object)", AspectFilter.StringLiterals)]
    public static string Format(string format, object arg0)
    {
        var result = string.Format(format, arg0);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, format, arg0);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="format"> format of the string </param>
    /// <param name="arg0"> first format argument </param>
    /// <param name="arg1"> second format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.String,System.Object,System.Object)", AspectFilter.StringLiterals)]
    public static string Format(string format, object arg0, object arg1)
    {
        var result = string.Format(format, arg0, arg1);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, format, arg0, arg1);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="format"> format of the string </param>
    /// <param name="arg0"> first format argument </param>
    /// <param name="arg1"> second format argument </param>
    /// <param name="arg2"> third format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.String,System.Object,System.Object,System.Object)", AspectFilter.StringLiterals)]
    public static string Format(string format, object arg0, object arg1, object arg2)
    {
        var result = string.Format(format, arg0, arg1, arg2);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, format, arg0, arg1, arg2);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="format"> format of the string </param>
    /// <param name="args"> first format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.String,System.Object[])")]
    public static string Format(string format, object[] args)
    {
        var result = string.Format(format, args);
        PropagationModuleImpl.PropagateResultWhenInputArrayTainted(result, format, args);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="provider"> format provider </param>
    /// <param name="format"> format of the string </param>
    /// <param name="arg0"> first format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.IFormatProvider,System.String,System.Object)")]
    public static string Format(IFormatProvider provider, string format, object arg0)
    {
        var result = string.Format(provider, format, arg0);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, format, arg0);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="provider"> format provider </param>
    /// <param name="format"> format of the string </param>
    /// <param name="arg0"> first format argument </param>
    /// <param name="arg1"> second format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.IFormatProvider,System.String,System.Object,System.Object)")]
    public static string Format(IFormatProvider provider, string format, object arg0, object arg1)
    {
        var result = string.Format(provider, format, arg0, arg1);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, format, arg0, arg1);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="provider"> format provider </param>
    /// <param name="format"> format of the string </param>
    /// <param name="arg0"> first format argument </param>
    /// <param name="arg1"> second format argument </param>
    /// <param name="arg2"> third format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.IFormatProvider,System.String,System.Object,System.Object,System.Object)")]
    public static string Format(IFormatProvider provider, string format, object arg0, object arg1, object arg2)
    {
        var result = string.Format(provider, format, arg0, arg1, arg2);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, format, arg0, arg1, arg2);
        return result;
    }

    /// <summary>
    /// String.Format aspect
    /// </summary>
    /// <param name="provider"> format provider </param>
    /// <param name="format"> format of the string </param>
    /// <param name="args"> first format argument </param>
    /// <returns> String.Format() </returns>
    [AspectMethodReplace("System.String::Format(System.IFormatProvider,System.String,System.Object[])")]
    public static string Format(IFormatProvider provider, string format, object[] args)
    {
        var result = string.Format(provider, format, args);
        PropagationModuleImpl.PropagateResultWhenInputArrayTainted(result, format, args);
        return result;
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// String.Replace aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="oldValue"> old value  argument</param>
    /// <param name="newValue"> new value argument </param>
    /// <param name="ignore"> true to ignore casing when comparing; false otherwise. </param>
    /// <param name="culture"> cluture argument </param>
    /// <returns> String.Replace() </returns>
    [AspectMethodReplace("System.String::Replace(System.String,System.String,System.Boolean,System.Globalization.CultureInfo)")]
    public static string Replace(string target, string oldValue, string newValue, bool ignore, CultureInfo culture)
    {
        var result = target.Replace(oldValue, newValue, ignore, culture);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target, oldValue, newValue);
        return result;
    }

    /// <summary>
    /// String.Replace aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="oldValue"> old value  argument</param>
    /// <param name="newValue"> new value argument </param>
    /// <param name="comparison"> comparison argument </param>
    /// <returns> String.Replace() </returns>
    [AspectMethodReplace("System.String::Replace(System.String,System.String,System.StringComparison)")]
    public static string Replace(string target, string oldValue, string newValue, StringComparison comparison)
    {
        var result = target.Replace(oldValue, newValue, comparison);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target, oldValue, newValue);
        return result;
    }
#endif

    /// <summary>
    /// String.Replace aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="oldChar"> old value  argument</param>
    /// <param name="newChar"> new value argument </param>
    /// <returns> String.Replace() </returns>
    [AspectMethodReplace("System.String::Replace(System.Char,System.Char)", AspectFilter.StringLiteral_0)]
    public static string Replace(string target, char oldChar, char newChar)
    {
        var result = target.Replace(oldChar, newChar);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Replace aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="oldValue"> old value  argument</param>
    /// <param name="newValue"> new value argument </param>
    /// <returns> String.Replace() </returns>
    [AspectMethodReplace("System.String::Replace(System.String,System.String)", AspectFilter.StringLiterals)]
    public static string Replace(string target, string oldValue, string newValue)
    {
        var result = target.Replace(oldValue, newValue);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target, oldValue, newValue);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.Char[])", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, char[] separator)
    {
        var result = target.Split(separator);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="count"> count argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.Char[],System.Int32)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, char[] separator, int count)
    {
        var result = target.Split(separator, count);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.Char[],System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, char[] separator, StringSplitOptions options)
    {
        var result = target.Split(separator, options);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="count"> count argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.Char[],System.Int32,System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, char[] separator, int count, StringSplitOptions options)
    {
        var result = target.Split(separator, count, options);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.String[],System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, string[] separator, StringSplitOptions options)
    {
        var result = target.Split(separator, options);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="count"> count argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.String[],System.Int32,System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, string[] separator, int count, StringSplitOptions options)
    {
        var result = target.Split(separator, count, options);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

#if !NETFRAMEWORK
    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="count"> count argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.String,System.Int32,System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, string separator, int count, StringSplitOptions options)
    {
#if NETSTANDARD
        var result = target.Split(new string[] { separator }, count, options);
#else
        var result = target.Split(separator, count, options);
#endif
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.String,System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, string separator, StringSplitOptions options)
    {
#if NETSTANDARD
        var result = target.Split(new string[] { separator }, options);
#else
        var result = target.Split(separator, options);
#endif
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.Char,System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, char separator, StringSplitOptions options)
    {
#if NETSTANDARD
        var result = target.Split(new char[] { separator }, options);
#else
        var result = target.Split(separator, options);
#endif
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }

    /// <summary>
    /// String.Split aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <param name="separator"> separator argument </param>
    /// <param name="count"> count argument </param>
    /// <param name="options"> options argument </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Split(System.Char,System.Int32,System.StringSplitOptions)", AspectFilter.StringLiteral_0)]
    public static string[] Split(string target, char separator, int count, StringSplitOptions options)
    {
#if NETSTANDARD
        var result = target.Split(new char[] { separator }, count, options);
#else
        var result = target.Split(separator, count, options);
#endif
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }
#endif

#pragma warning disable CS0618 // Obsolete
    /// <summary>
    /// String.Copy aspect
    /// </summary>
    /// <param name="target"> instance of the string </param>
    /// <returns> String.Split() </returns>
    [AspectMethodReplace("System.String::Copy(System.String)", AspectFilter.StringLiteral_0)]
    public static string Copy(string target)
    {
        var result = string.Copy(target);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, target);
        return result;
    }
#pragma warning restore CS0618 // Obsolete
}
