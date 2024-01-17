// <copyright file="AspectAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable CS8603
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Iast.Helpers;

namespace Datadog.Trace.Iast.Dataflow;

/// <summary>
/// Attribute to define am aspect method for Dataflow
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
internal abstract class AspectAttribute : Attribute
{
    private static Regex nameSplitter = new Regex(@"(?:([^|]+)\|)?(([^:]+)(?:::[^()]+\(.*\))?)", RegexOptions.Compiled); // 1->Assembly 2->Function 3->Type
    private readonly List<object> parameters = new List<object>();

    public AspectAttribute(string targetMethod, string targetType, int[] paramShift, bool[] boxParam, AspectFilter[] filters, AspectType aspectType = AspectType.Propagation, params VulnerabilityType[] vulnerabilityTypes)
    {
        if (paramShift == null || paramShift.Length == 0) { paramShift = new int[] { 0 }; }
        if (boxParam == null || boxParam.Length == 0) { boxParam = new bool[] { false }; }
        if (filters == null || filters.Length == 0) { filters = new AspectFilter[] { AspectFilter.None }; }

        if (paramShift.Length > 1 && boxParam.Length == 1)
        {
            boxParam = Enumerable.Repeat(boxParam[0], paramShift.Length).ToArray();
        }

        Debug.Assert(paramShift.Length == boxParam.Length, "paramShift and boxParam must be same len");

        parameters.Add(targetMethod);
        parameters.Add(targetType);
        parameters.Add(paramShift);
        parameters.Add(boxParam);
        parameters.Add(filters);
        parameters.Add(aspectType);
        parameters.Add(vulnerabilityTypes ?? new VulnerabilityType[0]);

        var targetMethodMatch = nameSplitter.Match(targetMethod);
        TargetMethodAssemblies = GetAssemblyList(targetMethodMatch.Groups[1].Value);
        TargetMethod = targetMethodMatch.Groups[2].Value;
        TargetMethodType = targetMethodMatch.Groups[3].Value;
        TargetTypeAssemblies = TargetMethodAssemblies;
        TargetType = TargetMethodType;

        if (!string.IsNullOrEmpty(targetType))
        {
            var targetTypeMatch = nameSplitter.Match(targetType);
            TargetTypeAssemblies = GetAssemblyList(targetTypeMatch.Groups[1].Value);
            TargetType = targetTypeMatch.Groups[3].Value;
        }

        AspectType = aspectType;
        VulnerabilityTypes = vulnerabilityTypes ?? new VulnerabilityType[0];
        IsVirtual = (TargetMethodType != TargetType);

        ParamShift = paramShift;
        BoxParam = boxParam;
        Filters = filters;
    }

    // Target method data (base virtual)
    public List<string> TargetMethodAssemblies { get; private set; }

    public string TargetMethodType { get; private set; }

    public string TargetMethod { get; }

    // Final type data
    public List<string> TargetTypeAssemblies { get; private set; }

    public string TargetType { get; private set; }

    public bool IsVirtual { get; }

    public int[] ParamShift { get; } // Number of parameters to move up in stack before inyecting the Aspect

    public bool[] BoxParam { get; } // Box parameter before adding call

    public AspectFilter[] Filters { get; } // Filters applied to aspect insertion

    public AspectType AspectType { get; private set; }

    public VulnerabilityType[] VulnerabilityTypes { get; private set; }

    internal static List<string> GetAssemblyList(string expression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return new List<string>();
        }

        return expression.Split(',').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList<string>();
    }
}
