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
    private const StringComparison PathComparison = StringComparison.Ordinal;
    private const StringComparison FallbackPathComparison = StringComparison.OrdinalIgnoreCase;
    private static readonly bool AllowCaseInsensitivePathFallback = FrameworkDescription.Instance.IsWindows();

    /// <summary>
    /// Finds the backend bitmap for a local source path by trying exact normalized candidates first and then unique suffix matches.
    /// </summary>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="rawCandidates">Local source path forms emitted by the active coverage tool.</param>
    /// <returns>The matching backend bitmap, or null when no safe unambiguous match exists.</returns>
    public static byte[]? GetBackendBitmap(CoverageBackfillData backfillData, IEnumerable<string> rawCandidates)
        => TryGetBackendCoverage(backfillData, rawCandidates, out _, out var bitmap) ? bitmap : null;

    /// <summary>
    /// Finds backend coverage for a local source path and returns both the backend key and bitmap.
    /// </summary>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="rawCandidates">Local source path forms emitted by the active coverage tool.</param>
    /// <param name="backendKey">The matched backend coverage key, when a safe match exists.</param>
    /// <param name="bitmap">The matched backend bitmap, when a safe match exists.</param>
    /// <returns>True when one safe unambiguous backend coverage entry matched; otherwise, false.</returns>
    public static bool TryGetBackendCoverage(CoverageBackfillData backfillData, IEnumerable<string> rawCandidates, out string? backendKey, out byte[]? bitmap)
    {
        if (TryGetBackendCoverage(backfillData, rawCandidates, out var match))
        {
            backendKey = match.BackendKey;
            bitmap = match.Bitmap;
            return true;
        }

        backendKey = null;
        bitmap = null;
        return false;
    }

    /// <summary>
    /// Finds backend coverage for a local source path and returns the full path-match metadata.
    /// </summary>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="rawCandidates">Local source path forms emitted by the active coverage tool.</param>
    /// <param name="match">The matched backend coverage entry, when a safe match exists.</param>
    /// <returns>True when one safe unambiguous backend coverage entry matched; otherwise, false.</returns>
    public static bool TryGetBackendCoverage(CoverageBackfillData backfillData, IEnumerable<string> rawCandidates, out CoverageBackfillPathMatch match)
        => TryGetBackendCoverage(backfillData, rawCandidates, allowSuffixMatch: true, out match);

    /// <summary>
    /// Finds backend coverage for a local source path and returns the full path-match metadata.
    /// </summary>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="rawCandidates">Local source path forms emitted by the active coverage tool.</param>
    /// <param name="allowSuffixMatch">Whether to allow unique path-segment suffix fallback after exact matching fails.</param>
    /// <param name="match">The matched backend coverage entry, when a safe match exists.</param>
    /// <returns>True when one safe unambiguous backend coverage entry matched; otherwise, false.</returns>
    public static bool TryGetBackendCoverage(CoverageBackfillData backfillData, IEnumerable<string> rawCandidates, bool allowSuffixMatch, out CoverageBackfillPathMatch match)
        => TryGetBackendCoverage(backfillData, rawCandidates, allowSuffixMatch, out match, out _);

    /// <summary>
    /// Finds backend coverage for a local source path and returns whether matching failed because active backend coverage was ambiguous.
    /// </summary>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="rawCandidates">Local source path forms emitted by the active coverage tool.</param>
    /// <param name="allowSuffixMatch">Whether to allow unique path-segment suffix fallback after exact matching fails.</param>
    /// <param name="match">The matched backend coverage entry, when a safe match exists.</param>
    /// <param name="hasAmbiguousActiveMatch">True when the local path could represent multiple active backend coverage keys.</param>
    /// <returns>True when one safe unambiguous backend coverage entry matched; otherwise, false.</returns>
    public static bool TryGetBackendCoverage(CoverageBackfillData backfillData, IEnumerable<string> rawCandidates, bool allowSuffixMatch, out CoverageBackfillPathMatch match, out bool hasAmbiguousActiveMatch)
    {
        List<string>? normalizedCandidates = null;
        byte[]? matchingBitmap = null;
        string? matchingKey = null;
        string? matchingCandidate = null;
        var matchingHasActiveBits = false;
        var matchingKind = CoverageBackfillPathMatchKind.None;
        match = default;
        hasAmbiguousActiveMatch = false;
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

            if (HasParentDirectorySegment(normalized))
            {
                continue;
            }

            normalizedCandidates ??= new List<string>();
            normalizedCandidates.Add(normalized);
            if (!backfillData.ExecutedLinesByRelativePath.TryGetValue(normalized, out var candidateBitmap))
            {
                continue;
            }

            if (!TryRecordMatch(normalized, candidateBitmap, normalized, CoverageBackfillPathMatchKind.ExactOrdinal, ref matchingKey, ref matchingBitmap, ref matchingCandidate, ref matchingHasActiveBits, ref matchingKind, ref hasAmbiguousActiveMatch))
            {
                return false;
            }
        }

        if (matchingBitmap is not null)
        {
            match = new CoverageBackfillPathMatch(matchingKey!, matchingBitmap, matchingCandidate!, matchingKind);
            return true;
        }

        if (normalizedCandidates is null)
        {
            return false;
        }

        if (AllowCaseInsensitivePathFallback)
        {
            foreach (var candidate in normalizedCandidates)
            {
                foreach (var backendEntry in backfillData.ExecutedLinesByRelativePath)
                {
                    if (!candidate.Equals(backendEntry.Key, FallbackPathComparison))
                    {
                        continue;
                    }

                    if (!TryRecordMatch(backendEntry.Key, backendEntry.Value, candidate, CoverageBackfillPathMatchKind.CaseInsensitiveExact, ref matchingKey, ref matchingBitmap, ref matchingCandidate, ref matchingHasActiveBits, ref matchingKind, ref hasAmbiguousActiveMatch))
                    {
                        return false;
                    }
                }
            }
        }

        if (matchingBitmap is not null)
        {
            match = new CoverageBackfillPathMatch(matchingKey!, matchingBitmap, matchingCandidate!, matchingKind);
            return true;
        }

        if (!allowSuffixMatch)
        {
            return false;
        }

        foreach (var candidate in normalizedCandidates)
        {
            foreach (var backendEntry in backfillData.ExecutedLinesByRelativePath)
            {
                if (!IsUnambiguousSuffixCandidate(candidate, backendEntry.Key))
                {
                    continue;
                }

                if (!TryRecordMatch(backendEntry.Key, backendEntry.Value, candidate, CoverageBackfillPathMatchKind.Suffix, ref matchingKey, ref matchingBitmap, ref matchingCandidate, ref matchingHasActiveBits, ref matchingKind, ref hasAmbiguousActiveMatch))
                {
                    return false;
                }
            }
        }

        if (matchingBitmap is null)
        {
            return false;
        }

        match = new CoverageBackfillPathMatch(matchingKey!, matchingBitmap, matchingCandidate!, matchingKind);
        return true;
    }

    /// <summary>
    /// Records a single backend path match and rejects competing backend keys for the same local source path.
    /// </summary>
    private static bool TryRecordMatch(
        string key,
        byte[] bitmap,
        string normalizedCandidate,
        CoverageBackfillPathMatchKind kind,
        ref string? matchingKey,
        ref byte[]? matchingBitmap,
        ref string? matchingCandidate,
        ref bool matchingHasActiveBits,
        ref CoverageBackfillPathMatchKind matchingKind,
        ref bool hasAmbiguousActiveMatch)
    {
        var hasActiveBits = HasActiveBits(bitmap);
        if (matchingKey is not null && !string.Equals(matchingKey, key, PathComparison))
        {
            if (matchingHasActiveBits && hasActiveBits)
            {
                hasAmbiguousActiveMatch = true;
                matchingBitmap = null;
                return false;
            }

            if (kind == CoverageBackfillPathMatchKind.Suffix &&
                matchingKind == CoverageBackfillPathMatchKind.Suffix &&
                matchingHasActiveBits != hasActiveBits)
            {
                if (IsMoreSpecificPathSuffix(key, matchingKey))
                {
                    if (!hasActiveBits)
                    {
                        hasAmbiguousActiveMatch = true;
                        matchingBitmap = null;
                        return false;
                    }

                    matchingKey = key;
                    matchingBitmap = bitmap;
                    matchingCandidate = normalizedCandidate;
                    matchingHasActiveBits = true;
                    matchingKind = kind;
                    return true;
                }

                if (IsMoreSpecificPathSuffix(matchingKey, key) && !matchingHasActiveBits)
                {
                    hasAmbiguousActiveMatch = true;
                    matchingBitmap = null;
                    return false;
                }
            }

            if (matchingHasActiveBits || !hasActiveBits)
            {
                return true;
            }

            matchingKey = key;
            matchingBitmap = bitmap;
            matchingCandidate = normalizedCandidate;
            matchingHasActiveBits = true;
            matchingKind = kind;
            return true;
        }

        matchingKey = key;
        matchingBitmap = bitmap;
        matchingCandidate = normalizedCandidate;
        matchingHasActiveBits = hasActiveBits;
        matchingKind = kind;
        return true;
    }

    private static bool HasActiveBits(byte[] bitmap)
    {
        foreach (var value in bitmap)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasParentDirectorySegment(string normalizedPath)
    {
        return normalizedPath.Equals("..", StringComparison.Ordinal) ||
               normalizedPath.StartsWith("../", StringComparison.Ordinal) ||
               normalizedPath.EndsWith("/..", StringComparison.Ordinal) ||
               normalizedPath.IndexOf("/../", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Checks whether the local path contains the backend path as a boundary-safe suffix while rejecting basename-only matches.
    /// </summary>
    private static bool IsUnambiguousSuffixCandidate(string localPath, string backendPath)
    {
        if (localPath.IndexOf('/') < 0 || backendPath.IndexOf('/') < 0)
        {
            return false;
        }

        return IsPathSuffix(localPath, backendPath, PathComparison) ||
               (AllowCaseInsensitivePathFallback &&
                IsPathSuffix(localPath, backendPath, FallbackPathComparison));
    }

    private static bool IsMoreSpecificPathSuffix(string path, string lessSpecificPath)
        => path.Length > lessSpecificPath.Length &&
           (IsPathSuffix(path, lessSpecificPath, PathComparison) ||
            (AllowCaseInsensitivePathFallback && IsPathSuffix(path, lessSpecificPath, FallbackPathComparison)));

    /// <summary>
    /// Checks whether <paramref name="candidatePath"/> ends with <paramref name="suffixPath"/> at a path-segment boundary.
    /// </summary>
    private static bool IsPathSuffix(string candidatePath, string suffixPath, StringComparison comparison)
    {
        if (candidatePath.Length < suffixPath.Length ||
            !candidatePath.EndsWith(suffixPath, comparison))
        {
            return false;
        }

        return candidatePath.Length == suffixPath.Length ||
               candidatePath[candidatePath.Length - suffixPath.Length - 1] == '/';
    }
}
