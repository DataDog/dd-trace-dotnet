// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Analyzers.AspectAnalyzers;

/// <summary>
/// Helper class for holding various diagnostics.
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The diagnostic ID displayed in error messages
    /// </summary>
    public const string BeforeAfterAspectDiagnosticId = "DD0004";

    /// <summary>
    /// The diagnostic ID displayed in error messages
    /// </summary>
    public const string ReplaceAspectDiagnosticId = "DD0005";
}
