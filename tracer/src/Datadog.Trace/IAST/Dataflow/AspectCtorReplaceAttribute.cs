// <copyright file="AspectCtorReplaceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class AspectCtorReplaceAttribute : AspectAttribute
{
    public AspectCtorReplaceAttribute(string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectCtorReplaceAttribute(string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, filters)
    {
    }

    public AspectCtorReplaceAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
        : base(targetMethod, aspectType, vulnerabilityTypes)
    {
    }
}
