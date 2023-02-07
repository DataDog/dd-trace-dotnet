// <copyright file="StringAspects.Concat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if DEBUG

using System;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> String class aspects </summary>
[System.ComponentModel.Browsable(false)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public partial class StringAspects
{
    /// <summary>
    /// String.Concat aspect
    /// </summary>
    /// <param name="target"> First param </param>
    /// <param name="param1"> Second param </param>
    /// <returns> String.Concat(target, param1) </returns>
    [AspectMethodReplace("System.String::Concatt(System.String,System.String)", AspectFilter.StringLiterals_Any)]
    public static string Concat(string target, string param1)
    {
        Console.WriteLine($"string.Concat({target}, {param1}");

        return Concat_Internal(new string[] { target, param1 });
    }

    /*

        /// <summary>
        /// String.Concat aspect optimization for literals
        /// </summary>
        /// <param name="target"> First param (literal) </param>
        /// <param name="param1"> Second param </param>
        /// <returns> String.Concat(target, param1) </returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiteral_0)]
        public static string Concat_0(string target, string param1)
        {
            Console.WriteLine($"string.Concat({target}, {param1}");

            return Concat_Internal(new string[] { target, param1 }, 0);
        }

        /// <summary>
        /// String.Concat aspect optimization for literals
        /// </summary>
        /// <param name="target"> First param </param>
        /// <param name="param1"> Second param (literals) </param>
        /// <returns> String.Concat(target, param1) </returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiteral_1)]
        public static string Concat_1(string target, string param1)
        {
            Console.WriteLine($"string.Concat({target}, {param1}");

            return Concat_Internal(new string[] { target, param1 }, 1);
        }

        /// <summary>
        /// String.Concat aspect
        /// </summary>
        /// <param name="target"> First param </param>
        /// <param name="param1"> Second param </param>
        /// <returns> String.Concat(target, param1) </returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object)")]
        public static string Concat(object target, object param1)
        {
            Console.WriteLine($"string.Concat({target}, {param1}");

            return Concat_Internal(new string[] { target?.ToString(), param1?.ToString() });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="target"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String)", AspectFilter.StringLiterals)]
        public static string Concat(string target, string param1, string param2)
        {
            return Concat_Internal(new string[] { target, param1, param2 });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="target"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)")]
        public static string Concat(object target, object param1, object param2)
        {
            return Concat_Internal(new string[] { target?.ToString(), param1?.ToString(), param2?.ToString() });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="target"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String,System.String)", AspectFilter.StringLiterals)]
        public static string Concat(string target, string param1, string param2, string param3)
        {
            return Concat_Internal(new string[] { target, param1, param2, param3 });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="target"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object,System.Object)")]
        public static string Concat(object target, object param1, object param2, object param3)
        {
            return Concat_Internal(new string[] { target?.ToString(), param1?.ToString(), param2?.ToString(), param3?.ToString() });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="target"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <param name="param4"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String,System.String,System.String)", AspectFilter.StringLiterals)]
        public static string Concat(string target, string param1, string param2, string param3, string param4)
        {
            return Concat_Internal(new string[] { target, param1, param2, param3, param4 });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="target"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <param name="param4"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object,System.Object,System.Object)")]
        public static string Concat(object target, object param1, object param2, object param3, object param4)
        {
            return Concat_Internal(new string[] { target?.ToString(), param1?.ToString(), param2?.ToString(), param3?.ToString(), param4?.ToString() });
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.Object[])")]
        public static string Concat(object[] values)
        {
            return Concat_Internal(Array.ConvertAll(values, x => x?.ToString()));
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.String[])")]
        public static string Concat(string[] values)
        {
            return Concat_Internal(values);
        }

        /// <summary>
        /// -
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<System.String>)")]
        [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<!!0>)")]
        public static string Concat(object values)
        {
            return (Concat_Internal(GetArrayIfEnumerable(values)));
        }

    */
    private static string Concat_Internal(string[] values, int taintedCandidateIndex = -1)
    {
        return string.Concat(values);
    }
}

#endif
