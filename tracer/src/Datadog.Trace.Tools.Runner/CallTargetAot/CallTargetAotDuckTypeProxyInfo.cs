// <copyright file="CallTargetAotDuckTypeProxyInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the generated proxy type for a canonical DuckType mapping.
/// </summary>
internal sealed class CallTargetAotDuckTypeProxyInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotDuckTypeProxyInfo"/> class.
    /// </summary>
    public CallTargetAotDuckTypeProxyInfo(string generatedProxyAssemblyName, string generatedProxyTypeName)
    {
        GeneratedProxyAssemblyName = generatedProxyAssemblyName;
        GeneratedProxyTypeName = generatedProxyTypeName;
    }

    /// <summary>
    /// Gets the generated proxy assembly name.
    /// </summary>
    public string GeneratedProxyAssemblyName { get; }

    /// <summary>
    /// Gets the generated proxy type full name.
    /// </summary>
    public string GeneratedProxyTypeName { get; }
}
