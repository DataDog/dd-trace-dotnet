// <copyright file="CIVisibilitySettings.Shared.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Ci.Configuration;

internal partial class CIVisibilitySettings
{
    /// <summary>
    /// Gets a value indicating whether the Code Coverage Jit Optimizations should be enabled
    /// </summary>
    public bool CodeCoverageEnableJitOptimizations { get; }

    /// <summary>
    /// Gets the code coverage mode
    /// </summary>
    public string? CodeCoverageMode { get; private set; }

    /// <summary>
    /// Gets the snk filepath to re-signing assemblies after the code coverage modification.
    /// </summary>
    public string? CodeCoverageSnkFilePath { get; }

    internal void SetCodeCoverageMode(string? coverageMode)
    {
        CodeCoverageMode = coverageMode;
    }
}
