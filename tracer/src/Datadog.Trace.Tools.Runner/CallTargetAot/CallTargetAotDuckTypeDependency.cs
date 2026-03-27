// <copyright file="CallTargetAotDuckTypeDependency.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Carries the generated DuckType dependency metadata needed by the CallTarget generator and generated props file.
/// </summary>
internal sealed class CallTargetAotDuckTypeDependency
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotDuckTypeDependency"/> class.
    /// </summary>
    public CallTargetAotDuckTypeDependency(string registryAssemblyPath, string registryAssemblyName, string registryBootstrapTypeName, string propsPath, string trimmerDescriptorPath)
    {
        RegistryAssemblyPath = registryAssemblyPath;
        RegistryAssemblyName = registryAssemblyName;
        RegistryBootstrapTypeName = registryBootstrapTypeName;
        PropsPath = propsPath;
        TrimmerDescriptorPath = trimmerDescriptorPath;
    }

    /// <summary>
    /// Gets the generated DuckType registry assembly path.
    /// </summary>
    public string RegistryAssemblyPath { get; }

    /// <summary>
    /// Gets the generated DuckType registry assembly name.
    /// </summary>
    public string RegistryAssemblyName { get; }

    /// <summary>
    /// Gets the full name of the generated DuckType bootstrap type.
    /// </summary>
    public string RegistryBootstrapTypeName { get; }

    /// <summary>
    /// Gets the generated DuckType props file path.
    /// </summary>
    public string PropsPath { get; }

    /// <summary>
    /// Gets the generated DuckType trimmer descriptor path.
    /// </summary>
    public string TrimmerDescriptorPath { get; }
}
