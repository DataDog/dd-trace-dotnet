// <copyright file="AspectMethodInsertAfterAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class AspectMethodInsertAfterAttribute : AspectAttribute
{
    public AspectMethodInsertAfterAttribute(string targetMethod)
        : base(targetMethod, string.Empty, new int[0], new bool[0], new AspectFilter[0], AspectType.Default)
    {
    }

    public AspectMethodInsertAfterAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
        : base(targetMethod, string.Empty, new int[0], new bool[0], new AspectFilter[0], aspectType, vulnerabilityTypes)
    {
    }
}
