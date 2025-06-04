// <copyright file="AspectClassFromVersionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.Iast.Dataflow;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class AspectClassFromVersionAttribute : AspectClassAttribute
{
    private readonly List<object> parameters = new List<object>();

    public AspectClassFromVersionAttribute(string version)
        : base()
    {
    }

    public AspectClassFromVersionAttribute(string version, string defaultAssembly, InstrumentationCategory category = InstrumentationCategory.Iast)
        : base(defaultAssembly, category)
    {
    }

    public AspectClassFromVersionAttribute(string version, string defaultAssembly, InstrumentationCategory category, AspectType defaultAspectType, params VulnerabilityType[] defaultVulnerabilityTypes)
        : base(defaultAssembly, category, defaultAspectType, defaultVulnerabilityTypes)
    {
    }

    public AspectClassFromVersionAttribute(string version, string defaultAssembly, AspectFilter[] filters, InstrumentationCategory category, AspectType defaultAspectType = AspectType.Propagation, params VulnerabilityType[] defaultVulnerabilityTypes)
        : base(defaultAssembly, filters, category, defaultAspectType, defaultVulnerabilityTypes)
    {
    }
}
