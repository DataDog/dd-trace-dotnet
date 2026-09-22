// <copyright file="CoverageBackfillPathMatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Holds the backend coverage entry selected for one local coverage source path.
/// </summary>
internal readonly struct CoverageBackfillPathMatch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageBackfillPathMatch"/> struct.
    /// </summary>
    /// <param name="backendKey">Backend repository-relative coverage key.</param>
    /// <param name="bitmap">Backend coverage bitmap.</param>
    /// <param name="normalizedLocalCandidate">Normalized local path candidate that matched.</param>
    /// <param name="kind">Kind of path match used.</param>
    public CoverageBackfillPathMatch(string backendKey, byte[] bitmap, string normalizedLocalCandidate, CoverageBackfillPathMatchKind kind)
    {
        BackendKey = backendKey;
        Bitmap = bitmap;
        NormalizedLocalCandidate = normalizedLocalCandidate;
        Kind = kind;
    }

    /// <summary>
    /// Gets the backend repository-relative coverage key.
    /// </summary>
    public string BackendKey { get; }

    /// <summary>
    /// Gets the backend coverage bitmap.
    /// </summary>
    public byte[] Bitmap { get; }

    /// <summary>
    /// Gets a value indicating whether the backend bitmap contains at least one covered line.
    /// </summary>
    public bool HasActiveBits
    {
        get
        {
            foreach (var value in Bitmap)
            {
                if (value != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the normalized local path candidate that matched.
    /// </summary>
    public string NormalizedLocalCandidate { get; }

    /// <summary>
    /// Gets the kind of path match used.
    /// </summary>
    public CoverageBackfillPathMatchKind Kind { get; }

    /// <summary>
    /// Returns the same backend match with a different normalized local candidate for ambiguity tracking.
    /// </summary>
    /// <param name="normalizedLocalCandidate">Normalized local source identity to record for this backend key.</param>
    public CoverageBackfillPathMatch WithNormalizedLocalCandidate(string normalizedLocalCandidate)
        => new(BackendKey, Bitmap, normalizedLocalCandidate, Kind);
}
