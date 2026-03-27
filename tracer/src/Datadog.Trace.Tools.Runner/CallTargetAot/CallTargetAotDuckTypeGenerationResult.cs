// <copyright file="CallTargetAotDuckTypeGenerationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Carries the generated DuckType proxy type names keyed by canonical mapping key.
/// </summary>
internal sealed class CallTargetAotDuckTypeGenerationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotDuckTypeGenerationResult"/> class.
    /// </summary>
    public CallTargetAotDuckTypeGenerationResult(CallTargetAotDuckTypeDependency dependency, IReadOnlyDictionary<string, CallTargetAotDuckTypeProxyInfo> proxyTypesByMappingKey)
    {
        Dependency = dependency;
        ProxyTypesByMappingKey = proxyTypesByMappingKey;
    }

    /// <summary>
    /// Gets the generated DuckType dependency metadata.
    /// </summary>
    public CallTargetAotDuckTypeDependency Dependency { get; }

    /// <summary>
    /// Gets the generated proxy type information keyed by canonical DuckType mapping key.
    /// </summary>
    public IReadOnlyDictionary<string, CallTargetAotDuckTypeProxyInfo> ProxyTypesByMappingKey { get; }
}
