// <copyright file="CallTargetAotRewritePlan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the rewrite instructions emitted alongside the manifest.
/// </summary>
internal sealed class CallTargetAotRewritePlan
{
    /// <summary>
    /// Gets or sets the target assembly simple names that should be rewritten during publish integration.
    /// </summary>
    public List<string> TargetAssemblyNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the target assembly file names that should be swapped into publish inputs after rewrite.
    /// </summary>
    public List<string> TargetAssemblyFileNames { get; set; } = [];
}
