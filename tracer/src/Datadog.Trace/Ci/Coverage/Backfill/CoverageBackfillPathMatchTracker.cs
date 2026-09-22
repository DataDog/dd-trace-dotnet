// <copyright file="CoverageBackfillPathMatchTracker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Tracks fallback path matches within one coverage processing pass to prevent ambiguous backend-key reuse.
/// </summary>
internal sealed class CoverageBackfillPathMatchTracker
{
    private static readonly StringComparer LocalCandidateComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly Dictionary<string, string> _matchesByBackendKey = new(StringComparer.Ordinal);

    /// <summary>
    /// Records a path match and rejects one backend path being reused for multiple distinct local paths through fallback matching.
    /// </summary>
    /// <param name="match">Path match to record.</param>
    /// <returns>True when the match is safe to use.</returns>
    public bool TryRecord(CoverageBackfillPathMatch match)
    {
        if (!match.HasActiveBits)
        {
            return true;
        }

        if (_matchesByBackendKey.TryGetValue(match.BackendKey, out var existingLocalCandidate))
        {
            return LocalCandidateComparer.Equals(existingLocalCandidate, match.NormalizedLocalCandidate);
        }

        _matchesByBackendKey[match.BackendKey] = match.NormalizedLocalCandidate;
        return true;
    }
}
