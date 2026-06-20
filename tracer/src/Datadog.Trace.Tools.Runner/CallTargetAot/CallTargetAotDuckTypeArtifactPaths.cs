// <copyright file="CallTargetAotDuckTypeArtifactPaths.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the DuckType artifact paths derived for the CallTarget companion registry.
/// </summary>
internal sealed class CallTargetAotDuckTypeArtifactPaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotDuckTypeArtifactPaths"/> class.
    /// </summary>
    public CallTargetAotDuckTypeArtifactPaths(string assemblyName, string outputAssemblyPath, string propsPath, string trimmerDescriptorPath, string mapFilePath)
    {
        AssemblyName = assemblyName;
        OutputAssemblyPath = outputAssemblyPath;
        PropsPath = propsPath;
        TrimmerDescriptorPath = trimmerDescriptorPath;
        MapFilePath = mapFilePath;
    }

    /// <summary>
    /// Gets the generated DuckType registry assembly name.
    /// </summary>
    public string AssemblyName { get; }

    /// <summary>
    /// Gets the generated DuckType registry assembly path.
    /// </summary>
    public string OutputAssemblyPath { get; }

    /// <summary>
    /// Gets the generated DuckType props file path.
    /// </summary>
    public string PropsPath { get; }

    /// <summary>
    /// Gets the generated DuckType trimmer descriptor path.
    /// </summary>
    public string TrimmerDescriptorPath { get; }

    /// <summary>
    /// Gets the generated canonical DuckType map path.
    /// </summary>
    public string MapFilePath { get; }
}
