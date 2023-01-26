// <copyright file="StringAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects
{
    /// <summary> String class aspects </summary>
    [AspectClass("mscorlib,netstandard,System.Private.CoreLib", AspectFilter.StringOptimization)]
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public partial class StringAspect
    {
        /// <summary>
        /// Concat with both strings not static
        /// </summary>
        /// <param name="target">main string instance (not static)</param>
        /// <param name="param1">first parameter (not static)</param>
        /// <returns>target concatenated to param1</returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiterals_Any)]
        public static string Concat(string target, string param1)
        {
            return string.Concat(target, param1);
        }

        /// <summary>
        /// Concat with param1 not static
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter (not static)</param>
        /// <returns>target concatenated to param1</returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiteral_0)]
        public static string Concat_0(string target, string param1)
        {
            return string.Concat(target, param1);
        }

        /// <summary>
        /// Concat with instance strings not static
        /// </summary>
        /// <param name="target">main string instance (not static)</param>
        /// <param name="param1">first parameter</param>
        /// <returns>target concatenated to param1</returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String)", AspectFilter.StringLiteral_1)]
        public static string Concat_1(string target, string param1)
        {
            return string.Concat(target, param1);
        }

        /// <summary>
        /// String Concat with object
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <returns>target concatenated to param1</returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object)")]
        public static string Concat(object target, object param1)
        {
            return string.Concat(target, param1);
        }

        /// <summary>
        /// String Concat with two strings
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <param name="param2">second parameter</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String)", AspectFilter.StringLiterals)]
        public static string Concat(string target, string param1, string param2)
        {
            return string.Concat(target, param1, param2);
        }

        /// <summary>
        /// String Concat with two objects
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <param name="param2">second parameter</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object)")]
        public static string Concat(object target, object param1, object param2)
        {
            return string.Concat(target, param1, param2);
        }

        /// <summary>
        /// String Concat with three strings
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <param name="param2">second parameter</param>
        /// <param name="param3">third parameter</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String,System.String)", AspectFilter.StringLiterals)]
        public static string Concat(string target, string param1, string param2, string param3)
        {
            return string.Concat(target, param1, param2, param3);
        }

        /// <summary>
        /// String Concat with three objects
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <param name="param2">second parameter</param>
        /// <param name="param3">third parameter</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object,System.Object)")]
        public static string Concat(object target, object param1, object param2, object param3)
        {
            return string.Concat(target, param1, param2, param3);
        }

        /// <summary>
        /// String Concat with four strings
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <param name="param2">second parameter</param>
        /// <param name="param3">third parameter</param>
        /// <param name="param4">fourth parameter</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.String,System.String,System.String,System.String,System.String)", AspectFilter.StringLiterals)]
        public static string Concat(string target, string param1, string param2, string param3, string param4)
        {
            return string.Concat(target, param1, param2, param3, param4);
        }

        /// <summary>
        /// String Concat with four objects
        /// </summary>
        /// <param name="target">main string instance</param>
        /// <param name="param1">first parameter</param>
        /// <param name="param2">second parameter</param>
        /// <param name="param3">third parameter</param>
        /// <param name="param4">fourth parameter</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.Object,System.Object,System.Object,System.Object,System.Object)")]
        public static string Concat(object target, object param1, object param2, object param3, object param4)
        {
            return string.Concat(target, param1, param2, param3, param4);
        }

        /// <summary>
        /// Concat of all elements in an object array
        /// </summary>
        /// <param name="values">the object array</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.Object[])")]
        public static string Concat(object[] values)
        {
            return string.Concat(values);
        }

        /// <summary>
        /// Concat of all elements in a string array
        /// </summary>
        /// <param name="values">the string array</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.String[])")]
        public static string Concat(string[] values)
        {
            return string.Concat(values);
        }

        /// <summary>
        /// Concat of all elements in an object IEnumerable
        /// </summary>
        /// <param name="values">the object array</param>
        /// <returns>target concatenated to params</returns>
        [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<System.String>)")]
        [AspectMethodReplace("System.String::Concat(System.Collections.Generic.IEnumerable`1<!!0>)")]
        public static string Concat(object values)
        {
            return string.Concat(values);
        }

        private static string Concat_Internal(string[] values, int taintedCandidateIndex = -1)
        {
            // TODO: do the TaintedObjects magic
            // try
            // {
            //    var context = ContextHolder.Current;
            //    var t = context.TaintedObjects;
            //    if (t != null)
            //    {
            //        return StringConcatTainted(context, values, taintedCandidateIndex);
            //    }
            // }
            // catch (AST.Commons.Exceptions.HdivException) { throw; }
            // catch (Exception ex)
            // {
            //    logger.Error(ex.ToFormattedString());
            // }

            return string.Concat(values);
        }

        private static string[] GetArrayIfEnumerable(object values)
        {
            if (values is string[] stringArray) { return stringArray; }
            var list = GetListIfEnumerable(values);
            if (list != null)
            {
                var res = new string[list.Count]; // Avoid the use of .ToArray because in NetStandard sometimes crashes
                for (int x = 0; x < list.Count; x++)
                {
                    res[x] = list[x];
                }

                return res;
            }

            return new string[0];
        }

        private static List<string> GetListIfEnumerable(object values)
        {
            if (values is List<string> stringList) { return stringList; }

            var strs = new List<string>();
            if (values is global::System.Collections.IEnumerable valuesEnumerable)
            {
                var en = valuesEnumerable.GetEnumerator();
                while (en.MoveNext())
                {
                    string value = en.Current?.ToString();
                    if (value != null)
                    {
                        strs.Add(value);
                    }
                }
            }

            return strs;
        }
    }
}
