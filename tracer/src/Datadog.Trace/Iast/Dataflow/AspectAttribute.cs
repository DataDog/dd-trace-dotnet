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
    public AspectAttribute(string targetMethod, string targetType, int[] paramShift, bool[] boxParam, AspectFilter[] filters, AspectType aspectType = AspectType.Propagation, params VulnerabilityType[] vulnerabilityTypes)
    {
        TargetMethod = targetMethod;
        TargetType = targetType;

        AspectType = aspectType;
        VulnerabilityTypes = vulnerabilityTypes ?? new VulnerabilityType[0];

        ParamShift = paramShift;
        BoxParam = boxParam;
        Filters = filters;
    }

    // Method
    public string TargetMethod { get; }

    // Final type data (for virtual method calls)
    public string TargetType { get; private set; }

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
