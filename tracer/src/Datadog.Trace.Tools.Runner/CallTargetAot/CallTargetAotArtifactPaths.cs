// <copyright file="CallTargetAotArtifactPaths.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the concrete artifact paths used by the CallTarget NativeAOT generator.
/// </summary>
internal sealed class CallTargetAotArtifactPaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotArtifactPaths"/> class.
    /// </summary>
    /// <param name="outputAssemblyPath">The generated registry assembly path.</param>
    /// <param name="targetsPath">The generated MSBuild targets path.</param>
    /// <param name="propsPath">The generated MSBuild props path.</param>
    /// <param name="trimmerDescriptorPath">The generated trimmer descriptor path.</param>
    /// <param name="manifestPath">The generated manifest path.</param>
    /// <param name="rewritePlanPath">The generated standalone rewrite-plan path.</param>
    /// <param name="compatibilityReportPath">The generated compatibility report markdown path.</param>
    /// <param name="compatibilityMatrixPath">The generated compatibility report json path.</param>
    public CallTargetAotArtifactPaths(
        string outputAssemblyPath,
        string targetsPath,
        string propsPath,
        string trimmerDescriptorPath,
        string manifestPath,
        string rewritePlanPath,
        string compatibilityReportPath,
        string compatibilityMatrixPath)
    {
        OutputAssemblyPath = outputAssemblyPath;
        TargetsPath = targetsPath;
        PropsPath = propsPath;
        TrimmerDescriptorPath = trimmerDescriptorPath;
        ManifestPath = manifestPath;
        RewritePlanPath = rewritePlanPath;
        CompatibilityReportPath = compatibilityReportPath;
        CompatibilityMatrixPath = compatibilityMatrixPath;
    }

    /// <summary>
    /// Gets the generated registry assembly path.
    /// </summary>
    public string OutputAssemblyPath { get; }

    /// <summary>
    /// Gets the generated MSBuild targets path.
    /// </summary>
    public string TargetsPath { get; }

    /// <summary>
    /// Gets the generated MSBuild props path.
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
    /// Gets the generated compatibility report json path.
    /// </summary>
    public string CompatibilityMatrixPath { get; }

    /// <summary>
    /// Creates the normalized artifact path set for the supplied options.
    /// </summary>
    /// <param name="options">The generation options that define the requested output locations.</param>
    /// <returns>The normalized artifact path set.</returns>
    public static CallTargetAotArtifactPaths Create(CallTargetAotGenerateOptions options)
    {
        var outputAssemblyPath = Path.GetFullPath(options.OutputPath);
        var targetsPath = Path.GetFullPath(options.TargetsPath);
        var propsPath = Path.GetFullPath(options.PropsPath);
        var trimmerDescriptorPath = Path.GetFullPath(options.TrimmerDescriptorPath);
        var manifestPath = Path.GetFullPath(options.ManifestPath);
        var rewritePlanPath = Path.GetFullPath(options.RewritePlanPath);
        var compatibilityReportPath = Path.GetFullPath(options.CompatibilityReportPath);
        var compatibilityMatrixPath = Path.GetFullPath(options.CompatibilityMatrixPath);

        return new CallTargetAotArtifactPaths(
            outputAssemblyPath,
            targetsPath,
            propsPath,
            trimmerDescriptorPath,
            manifestPath,
            rewritePlanPath,
            compatibilityReportPath,
            compatibilityMatrixPath);
    }
}
