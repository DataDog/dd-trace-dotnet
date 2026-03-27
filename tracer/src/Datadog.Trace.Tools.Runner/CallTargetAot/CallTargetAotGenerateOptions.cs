// <copyright file="CallTargetAotGenerateOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Stores the normalized inputs required to generate the NativeAOT CallTarget artifacts.
/// </summary>
internal sealed class CallTargetAotGenerateOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotGenerateOptions"/> class.
    /// </summary>
    /// <param name="tracerAssemblyPath">The tracer assembly used as the source of truth for future CallTarget discovery work.</param>
    /// <param name="targetFolders">The folders that contain candidate target assemblies.</param>
    /// <param name="targetFilters">The file globs used to select candidate target assemblies inside each target folder.</param>
    /// <param name="outputPath">The output path for the generated registry assembly.</param>
    /// <param name="assemblyName">The optional assembly name to assign to the generated registry assembly.</param>
    /// <param name="targetsPath">The output path for the generated MSBuild targets file.</param>
    /// <param name="propsPath">The output path for the generated MSBuild props file.</param>
    /// <param name="trimmerDescriptorPath">The output path for the generated trimmer descriptor.</param>
    /// <param name="manifestPath">The output path for the generated manifest.</param>
    /// <param name="rewritePlanPath">The output path for the standalone rewrite-plan artifact.</param>
    /// <param name="compatibilityReportPath">The output path for the generated compatibility report markdown file.</param>
    /// <param name="compatibilityMatrixPath">The output path for the generated compatibility report json file.</param>
    public CallTargetAotGenerateOptions(
        string tracerAssemblyPath,
        IReadOnlyList<string> targetFolders,
        IReadOnlyList<string> targetFilters,
        string outputPath,
        string? assemblyName,
        string targetsPath,
        string propsPath,
        string trimmerDescriptorPath,
        string manifestPath,
        string rewritePlanPath,
        string compatibilityReportPath,
        string compatibilityMatrixPath)
    {
        TracerAssemblyPath = tracerAssemblyPath;
        TargetFolders = targetFolders;
        TargetFilters = targetFilters;
        OutputPath = outputPath;
        AssemblyName = assemblyName;
        TargetsPath = targetsPath;
        PropsPath = propsPath;
        TrimmerDescriptorPath = trimmerDescriptorPath;
        ManifestPath = manifestPath;
        RewritePlanPath = rewritePlanPath;
        CompatibilityReportPath = compatibilityReportPath;
        CompatibilityMatrixPath = compatibilityMatrixPath;
    }

    /// <summary>
    /// Gets the tracer assembly path recorded in the manifest for future adapter discovery work.
    /// </summary>
    public string TracerAssemblyPath { get; }

    /// <summary>
    /// Gets the folders that contain candidate target assemblies.
    /// </summary>
    public IReadOnlyList<string> TargetFolders { get; }

    /// <summary>
    /// Gets the file globs used to select target assemblies from each target folder.
    /// </summary>
    public IReadOnlyList<string> TargetFilters { get; }

    /// <summary>
    /// Gets the generated registry assembly output path.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Gets the optional generated assembly name.
    /// </summary>
    public string? AssemblyName { get; }

    /// <summary>
    /// Gets the generated MSBuild targets file path.
    /// </summary>
    public string TargetsPath { get; }

    /// <summary>
    /// Gets the generated MSBuild props file path.
    /// </summary>
    public string PropsPath { get; }

    /// <summary>
    /// Gets the generated trimmer descriptor path.
    /// </summary>
    public string TrimmerDescriptorPath { get; }

    /// <summary>
    /// Gets the generated manifest path.
    /// </summary>
    public string ManifestPath { get; }

    /// <summary>
    /// Gets the generated standalone rewrite-plan path.
    /// </summary>
    public string RewritePlanPath { get; }

    /// <summary>
    /// Gets the generated compatibility report markdown path.
    /// </summary>
    public string CompatibilityReportPath { get; }

    /// <summary>
    /// Gets the generated compatibility matrix json path.
    /// </summary>
    public string CompatibilityMatrixPath { get; }
}
