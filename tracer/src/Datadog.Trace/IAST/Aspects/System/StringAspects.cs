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
[AspectClass("mscorlib,netstandard,System.Private.CoreLib", AspectFilter.StringOptimization)]
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
}
