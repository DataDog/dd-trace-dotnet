// <copyright file="AspNetCoreFeatureFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;

public enum AspNetCoreFeatureFlags
{
    /// <summary>
    /// No config features enabled
    /// </summary>
    None,

    /// <summary>
    /// Route Template resource names enabled
    /// </summary>
    RouteTemplateResourceNames,

    /// <summary>
    /// Single span aspnetcore enabled
    /// </summary>
    SingleSpan
}
