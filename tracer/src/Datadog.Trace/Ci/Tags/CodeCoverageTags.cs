// <copyright file="CodeCoverageTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Code Coverage span tags
/// </summary>
internal static class CodeCoverageTags
{
    /// <summary>
    /// Test Session Code Coverage is enabled flag
    /// </summary>
    public const string Enabled = "test.code_coverage.enabled";

    /// <summary>
    /// Code coverage global percentage value
    /// </summary>
    public const string PercentageOfTotalLines = "test.code_coverage.lines_pct";

    /// <summary>
    /// Test Session Code Coverage is instrumented flag
    /// </summary>
    public const string Instrumented = "test.code_coverage.instrumented";
}
