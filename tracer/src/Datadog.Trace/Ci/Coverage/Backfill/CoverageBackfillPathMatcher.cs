// <copyright file="CoverageBackfillPathMatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Matches local coverage report source paths to backend repository-relative coverage keys.
/// </summary>
internal static class CoverageBackfillPathMatcher
{
    /// <summary>
    /// Finds the backend bitmap for a local source path by trying exact normalized candidates first and then unique suffix matches.
    /// </summary>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="rawCandidates">Local source path forms emitted by the active coverage tool.</param>
    /// <returns>The matching backend bitmap, or null when no safe unambiguous match exists.</returns>
    public static byte[]? GetBackendBitmap(CoverageBackfillData backfillData, IEnumerable<string> rawCandidates)
    {
        var normalizedCandidates = new List<string>();
        byte[]? matchingBitmap = null;
        string? matchingKey = null;
        foreach (var candidate in rawCandidates)
        {
            string normalized;
            try
            {
                normalized = CoverageBackfillData.NormalizePath(candidate);
            }
            catch
            {
                continue;
            }

            normalizedCandidates.Add(normalized);
            if (!backfillData.ExecutedLinesByRelativePath.TryGetValue(normalized, out var bitmap))
            {
                continue;
            }

            if (!TryRecordMatch(normalized, bitmap, ref matchingKey, ref matchingBitmap))
            {
                return null;
            }
        }

        if (matchingBitmap is not null)
        {
            return matchingBitmap;
        }

        foreach (var candidate in normalizedCandidates)
        {
            foreach (var backendEntry in backfillData.ExecutedLinesByRelativePath)
            {
                if (!IsUnambiguousSuffixCandidate(candidate, backendEntry.Key))
                {
                    continue;
                }

                if (!TryRecordMatch(backendEntry.Key, backendEntry.Value, ref matchingKey, ref matchingBitmap))
                {
                    return null;
                }
            }
        }

        return matchingBitmap;
    }

    /// <summary>
    /// Records a single backend path match and rejects competing backend keys for the same local source path.
    /// </summary>
    private static bool TryRecordMatch(string key, byte[] bitmap, ref string? matchingKey, ref byte[]? matchingBitmap)
    {
        if (matchingKey is not null && !string.Equals(matchingKey, key, StringComparison.Ordinal))
        {
            matchingBitmap = null;
            return false;
        }

        matchingKey = key;
        matchingBitmap = bitmap;
        return true;
    }

    /// <summary>
    /// Checks whether one path is a boundary-safe suffix of the other while rejecting basename-only matches.
    /// </summary>
    private static bool IsUnambiguousSuffixCandidate(string localPath, string backendPath)
    {
        if (localPath.IndexOf('/') < 0 || backendPath.IndexOf('/') < 0)
        {
            return false;
        }

        return IsPathSuffix(localPath, backendPath) || IsPathSuffix(backendPath, localPath);
    }

    /// <summary>
    /// Checks whether <paramref name="candidatePath"/> ends with <paramref name="suffixPath"/> at a path-segment boundary.
    /// </summary>
    private static bool IsPathSuffix(string candidatePath, string suffixPath)
    {
        if (candidatePath.Length < suffixPath.Length ||
            !candidatePath.EndsWith(suffixPath, StringComparison.Ordinal))
        {
            return false;
        }

        return candidatePath.Length == suffixPath.Length ||
               candidatePath[candidatePath.Length - suffixPath.Length - 1] == '/';
    }
}
