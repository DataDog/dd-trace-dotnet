// <copyright file="CodeCoverageReportSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Identifies the coverage source that produced a session-level line coverage result.
/// </summary>
internal enum CodeCoverageReportSource
{
    /// <summary>
    /// The sender did not identify the coverage source. This is kept for compatibility with older IPC messages.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Datadog's internal coverage collector produced the result.
    /// </summary>
    DatadogInternal = 1,

    /// <summary>
    /// A configured external XML coverage report produced the result.
    /// </summary>
    ExternalXml = 2,

    /// <summary>
    /// Coverlet produced the result through an in-process coverage model.
    /// </summary>
    Coverlet = 3,

    /// <summary>
    /// Microsoft CodeCoverage produced the result.
    /// </summary>
    MicrosoftCodeCoverage = 4,
}
