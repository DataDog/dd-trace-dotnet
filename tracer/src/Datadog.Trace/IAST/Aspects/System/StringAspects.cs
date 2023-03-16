// <copyright file="StringAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
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
public partial class StringAspects
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StringAspects));

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

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <param name="param3"> Third param </param>
    /// <param name="param4"> Fourth param </param>
    /// <param name="param5"> Fifth param </param>
    /// <returns> String.Concat(param1, param2, param3, param4, param5) </returns>
    [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String,System.String,System.String)", AspectFilter.StringLiterals)]
    public static string Concat(string param1, string param2, string param3, string param4, string param5)
    {
        return StringModuleImpl.OnStringConcat(new StringConcatParams(param1, param2, param3, param4, param5), string.Concat(param1, param2, param3, param4, param5));
    }

    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="param1"> First param </param>
    /// <param name="param2"> Second param </param>
    /// <param name="param3"> Third param </param>
    /// <param name="param4"> Fourth param </param>
    /// <param name="param5"> Fifth param </param>
    /// <returns> String.Concat(param1, param2, param3, param4, param5) </returns>
    [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object,System.Object,System.Object)")]
    public static string Concat(object param1, object param2, object param3, object param4, object param5)
    {
        return StringModuleImpl.OnStringConcat(new StringConcatParams(param1?.ToString(), param2?.ToString(), param3?.ToString(), param4?.ToString(), param5?.ToString()), string.Concat(param1, param2, param3, param4, param5));
    }

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
        StringModuleImpl.PropagateTaint(target, result);
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
        return OnStringJoin(string.Join(separator.ToString(), values), values);
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
    public static string Join(string separator, IEnumerable values)
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
    public static string Join2(string separator, IEnumerable values)
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
        StringModuleImpl.PropagateTaint(target, result);
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
        StringModuleImpl.PropagateTaint(target, result);
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
        StringModuleImpl.PropagateTaint(target, result);
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
        StringModuleImpl.PropagateTaint(target, result);
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
        StringModuleImpl.PropagateTaint(target, result);
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
        StringModuleImpl.PropagateTaint(target, result);
        return result;
    }
}
