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
    /// <param name="applied">Whether a valid backend-aware backfill path was used.</param>
    /// <param name="matchedFiles">Number of local files that matched backend coverage keys.</param>
    /// <param name="updatedFiles">Number of local files whose executed bitmap changed.</param>
    public CoverageBackfillResult(bool applied, int matchedFiles, int updatedFiles)
    {
        Applied = applied;
        MatchedFiles = matchedFiles;
        UpdatedFiles = updatedFiles;
    }

    /// <summary>
    /// Gets a value indicating whether a valid backend-aware backfill path was used.
    /// </summary>
    public bool Applied { get; }

    /// <summary>
    /// Gets the number of local files that matched backend coverage keys.
    /// </summary>
    public int MatchedFiles { get; }

    /// <summary>
    /// Gets the number of local files whose executed bitmap changed.
    /// </summary>
    public int UpdatedFiles { get; }
}
