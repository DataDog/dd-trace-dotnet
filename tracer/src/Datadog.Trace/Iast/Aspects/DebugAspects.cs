// <copyright file="DebugAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if DEBUG

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> String class aspects </summary>
internal class DebugAspects
{
    private interface ITestStruct
    {
        public string GetText();
    }

    /// <summary>
    /// AspectMethodReplace test method
    /// </summary>
    /// <param name="target">main string instance (not static)</param>
    /// <param name="param1">first parameter (not static)</param>
    /// <returns>target concatenated to param1</returns>
    public static string AspectMethodReplace(string target, string param1)
    {
        Console.WriteLine($"[AspectMethodReplace]DebugAspects.AspectMethodReplace(string {target}, string {param1})");
        return string.Concat(target, param1);
    }

    /// <summary>
    /// AspectMethodReplace test method
    /// </summary>
    /// <param name="target">main object instance </param>
    /// <returns>target concatenated to param1</returns>
    public static string AspectMethodReplace(object target)
    {
        Console.WriteLine($"[AspectMethodReplace]DebugAspects.AspectMethodReplace(object {target})");
        var tTarget = target.DuckCast<ITestStruct>();
        return tTarget.GetText();
    }

    /// <summary>
    /// AspectMethodInsertBefore test method with last parameter
    /// </summary>
    /// <param name="target">target parameter</param>
    /// <returns>Returns the target parameter</returns>
    public static string AspectMethodInsertBefore_0(string target)
    {
        Console.WriteLine($"[AspectMethodInsertBefore]DebugAspects.AspectMethodInsertBefore_0(string {target})");
        return target;
    }

    /// <summary>
    /// AspectMethodInsertBefore test method with next to last parameter
    /// </summary>
    /// <param name="target">target parameter</param>
    /// <returns>Returns the target parameter</returns>
    public static string AspectMethodInsertBefore_1(string target)
    {
        Console.WriteLine($"[AspectMethodInsertBefore]DebugAspects.AspectMethodInsertBefore_1(string {target})");
        return target;
    }

    /// <summary>
    /// AspectMethodInsertBefore test method with next to last parameter
    /// </summary>
    /// <param name="target">target parameter</param>
    /// <returns>Returns the target parameter</returns>
    public static string AspectMethodInsertAfter(string target)
    {
        Console.WriteLine($"[AspectMethodInsertAfter]DebugAspects.AspectMethodInsertAfter(string {target})");
        return target;
    }

    /// <summary>
    /// AspectCtorReplace test method
    /// </summary>
    /// <param name="param1">first parameter (not static)</param>
    /// <param name="param2">second parameter (not static)</param>
    /// <returns>Returns the new object instance</returns>
    public static object AspectCtorReplace(string param1, string param2)
    {
        Console.WriteLine($"[AspectCtorReplace]DebugAspects.AspectCtorReplace(string {param1}, string {param2})");
        return param1 + param2;
    }
}

#endif
