// <copyright file="CoverageBackfillResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Describes the result of applying backend ITR coverage to a local coverage model.
/// </summary>
internal readonly struct CoverageBackfillResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageBackfillResult"/> struct.
    /// </summary>
    /// <param name="coverageDataEvaluated">Whether valid backend coverage was evaluated against the local coverage model.</param>
    /// <param name="matchedFiles">Number of local files that matched backend coverage keys.</param>
    /// <param name="updatedFiles">Number of local files whose executed bitmap changed.</param>
    /// <param name="hasBackendCoverage">Whether the evaluated backend payload contained any file coverage.</param>
    /// <param name="canPublishCoverage">Whether the local coverage result is safe to publish.</param>
    public CoverageBackfillResult(bool coverageDataEvaluated, int matchedFiles, int updatedFiles, bool hasBackendCoverage = false, bool? canPublishCoverage = null)
    {
        Applied = coverageDataEvaluated;
        MatchedFiles = matchedFiles;
        UpdatedFiles = updatedFiles;
        HasBackendCoverage = hasBackendCoverage;
        CanPublishCoverage = canPublishCoverage ?? true;
    }

    /// <summary>
    /// Gets a value indicating whether valid backend coverage was evaluated against the local coverage model.
    /// </summary>
    /// <remarks>
    /// This can be true with zero matched or updated files: it means the backfill input was valid and the local model was checked.
    /// Use <see cref="UpdatedFiles"/> to determine whether coverage data actually changed.
    /// </remarks>
    public bool Applied { get; }

    /// <summary>
    /// Gets a value indicating whether backend ITR coverage was safely reconciled with the local coverage result.
    /// </summary>
    public bool Backfilled => Applied && HasBackendCoverage && CanPublishCoverage && MatchedFiles > 0;

    /// <summary>
    /// Gets a value indicating whether the evaluated backend payload contained at least one file coverage entry.
    /// </summary>
    public bool HasBackendCoverage { get; }

    /// <summary>
    /// Gets a value indicating whether the local coverage result is safe to publish after the backfill attempt.
    /// </summary>
    public bool CanPublishCoverage { get; }

    /// <summary>
    /// Gets the number of local files that matched backend coverage keys.
    /// </summary>
    public int MatchedFiles { get; }

    /// <summary>
    /// Gets the number of local files whose executed bitmap changed.
    /// </summary>
    public int UpdatedFiles { get; }
}
