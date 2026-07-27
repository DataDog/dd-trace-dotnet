// <copyright file="CodeCoverageAggregationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Represents the selected session coverage result after source arbitration.
/// </summary>
internal readonly struct CodeCoverageAggregationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeCoverageAggregationResult"/> struct.
    /// </summary>
    /// <param name="source">Coverage source selected for publication.</param>
    /// <param name="percentage">Line coverage percentage to publish.</param>
    /// <param name="backfilled">Whether the selected result used backend ITR coverage backfill.</param>
    /// <param name="executableLines">Executable-line count, when available.</param>
    /// <param name="coveredLines">Covered-line count, when available.</param>
    /// <param name="diagnostic">Compact diagnostic text, when available.</param>
    /// <param name="resultId">Stable result identity used to deduplicate IPC delivery and persisted fallback copies of the same producer result.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    /// <param name="supersededResultIds">Stable identities of partial producer results represented by this merged result.</param>
    public CodeCoverageAggregationResult(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string? diagnostic, string? resultId = null, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null, string[]? supersededResultIds = null)
    {
        Source = source;
        Percentage = percentage;
        Backfilled = backfilled;
        ExecutableLines = executableLines;
        CoveredLines = coveredLines;
        Diagnostic = diagnostic;
        ResultId = resultId;
        SupersededResultIds = supersededResultIds ?? Array.Empty<string>();
        BackfillNotApplicable = backfillNotApplicable;
        BackfillValidation = backfillValidation;
        BackfillValidated = backfillValidated || backfillValidation?.CanPublish() == true;
    }

    /// <summary>
    /// Gets the coverage source selected for publication.
    /// </summary>
    public CodeCoverageReportSource Source { get; }

    /// <summary>
    /// Gets the line coverage percentage to publish.
    /// </summary>
    public double Percentage { get; }

    /// <summary>
    /// Gets a value indicating whether the selected result used backend ITR coverage backfill.
    /// </summary>
    public bool Backfilled { get; }

    /// <summary>
    /// Gets a value indicating whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.
    /// </summary>
    public bool BackfillValidated { get; }

    /// <summary>
    /// Gets a value indicating whether backend ITR coverage was evaluated and did not apply to this producer result.
    /// </summary>
    public bool BackfillNotApplicable { get; }

    /// <summary>
    /// Gets backend ITR coverage validation data that can be merged with other same-source results.
    /// </summary>
    public CodeCoverageBackfillValidation? BackfillValidation { get; }

    /// <summary>
    /// Gets the executable-line count, when available.
    /// </summary>
    public double? ExecutableLines { get; }

    /// <summary>
    /// Gets the covered-line count, when available.
    /// </summary>
    public double? CoveredLines { get; }

    /// <summary>
    /// Gets compact diagnostic text, when available.
    /// </summary>
    public string? Diagnostic { get; }

    /// <summary>
    /// Gets the stable result identity used to deduplicate IPC delivery and persisted fallback copies of the same producer result.
    /// </summary>
    public string? ResultId { get; }

    /// <summary>
    /// Gets stable identities of partial producer results represented by this merged result.
    /// </summary>
    public string[] SupersededResultIds { get; }
}
