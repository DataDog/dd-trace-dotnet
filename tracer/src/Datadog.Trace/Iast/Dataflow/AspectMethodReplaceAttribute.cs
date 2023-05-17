// <copyright file="AspectMethodReplaceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class AspectMethodReplaceAttribute : AspectAttribute
{
    public AspectMethodReplaceAttribute(string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, filters)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
        : base(targetMethod, targetType, filters)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
        : base(targetMethod, aspectType, vulnerabilityTypes)
    {
    }
}
