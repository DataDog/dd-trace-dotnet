// <copyright file="AspectClassAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Iast.Helpers;

namespace Datadog.Trace.Iast.Dataflow;

[AttributeUsage(AttributeTargets.Class)]
internal class AspectClassAttribute : Attribute
{
    private readonly List<object> parameters = new List<object>();

    public AspectClassAttribute()
        : this(string.Empty)
    {
    }

    public AspectClassAttribute(string defaultAssembly)
        : this(defaultAssembly, new AspectFilter[0], AspectType.Propagation)
    {
    }

    public AspectClassAttribute(string defaultAssembly, AspectType defaultAspectType, params VulnerabilityType[] defaultVulnerabilityTypes)
        : this(defaultAssembly, new AspectFilter[0], defaultAspectType, defaultVulnerabilityTypes)
    {
    }

    public AspectClassAttribute(string defaultAssembly, AspectFilter[] filters, AspectType defaultAspectType = AspectType.Propagation, params VulnerabilityType[] defaultVulnerabilityTypes)
    {
        if (filters.Length == 0) { filters = new AspectFilter[] { AspectFilter.None }; }

        parameters.Add(defaultAssembly);
        parameters.Add(filters);
        parameters.Add(defaultAspectType);
        parameters.Add(defaultVulnerabilityTypes ?? new VulnerabilityType[0]);

        DefaultAssembly = AspectAttribute.GetAssemblyList(defaultAssembly);
        Filters = filters;
        DefaultAspectType = defaultAspectType;
        DefaultVulnerabilityTypes = defaultVulnerabilityTypes ?? new VulnerabilityType[0];
    }

    public List<string> DefaultAssembly { get; private set; }

    public AspectFilter[] Filters { get; private set; }

    public AspectType DefaultAspectType { get; private set; }

    public VulnerabilityType[] DefaultVulnerabilityTypes { get; private set; }
}
