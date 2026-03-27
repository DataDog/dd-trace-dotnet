// <copyright file="CallTargetAotManifest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the persisted generator output consumed by the rewrite step.
/// </summary>
internal sealed class CallTargetAotManifest
{
    /// <summary>
    /// Gets or sets the manifest schema version.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the tracer assembly that seeded generation.
    /// </summary>
    public string TracerAssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the generated registry assembly.
    /// </summary>
    public string RegistryAssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated registry assembly name.
    /// </summary>
    public string RegistryAssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full name of the generated bootstrap type.
    /// </summary>
    public string RegistryBootstrapTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bootstrap method name that the rewrite step injects into module initializers.
    /// </summary>
    public string RegistryBootstrapMethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the diagnostic marker emitted by the generated bootstrap.
    /// </summary>
    public string BootstrapMarker { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the standalone rewrite-plan artifact emitted next to the manifest.
    /// </summary>
    public string RewritePlanPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rewrite plan used by the generated targets file.
    /// </summary>
    public CallTargetAotRewritePlan RewritePlan { get; set; } = new();

    /// <summary>
    /// Gets or sets the complete set of evaluated bindings, including unsupported entries and their diagnostics.
    /// </summary>
    public List<CallTargetAotMatchedDefinition> EvaluatedDefinitions { get; set; } = [];

    /// <summary>
    /// Gets or sets the concrete target method matches selected for registry emission and rewrite.
    /// </summary>
    public List<CallTargetAotMatchedDefinition> MatchedDefinitions { get; set; } = [];

    /// <summary>
    /// Gets or sets the dependent DuckType AOT registry metadata when CallTarget bindings require generated proxies.
    /// </summary>
    public CallTargetAotDuckTypeDependency? DuckTypeDependency { get; set; }
}
