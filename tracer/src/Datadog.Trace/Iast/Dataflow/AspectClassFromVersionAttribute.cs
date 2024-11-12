// <copyright file="AspectClassFromVersionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.Iast.Dataflow;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class AspectClassFromVersionAttribute : AspectClassAttribute
{
    private readonly List<object> parameters = new List<object>();

    public AspectClassFromVersionAttribute(string version)
        : base()
    {
    }

    public AspectClassFromVersionAttribute(string version, string defaultAssembly)
        : base(defaultAssembly)
    {
    }

    public AspectClassFromVersionAttribute(string version, string defaultAssembly, AspectType defaultAspectType, params VulnerabilityType[] defaultVulnerabilityTypes)
        : base(defaultAssembly, defaultAspectType, defaultVulnerabilityTypes)
    {
    }

    public AspectClassFromVersionAttribute(string version, string defaultAssembly, AspectFilter[] filters, AspectType defaultAspectType = AspectType.Propagation, params VulnerabilityType[] defaultVulnerabilityTypes)
        : base(defaultAssembly, filters, defaultAspectType, defaultVulnerabilityTypes)
    {
    }
}
