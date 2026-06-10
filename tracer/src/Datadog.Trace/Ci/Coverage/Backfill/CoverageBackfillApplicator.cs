// <copyright file="CoverageBackfillApplicator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Applies backend skipped-test coverage to local coverage models while keeping local executable lines as the denominator.
/// </summary>
internal static class CoverageBackfillApplicator
{
    /// <summary>
    /// Applies backend skipped-test coverage to Datadog's internal global coverage model.
    /// </summary>
    /// <param name="globalCoverage">Local global coverage data collected from tests that actually ran.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="ciEnvironmentValues">CI environment values used to derive source-root-relative coverage paths.</param>
    /// <returns>Summary of the backfill operation.</returns>
    public static CoverageBackfillResult ApplyToGlobalCoverage(GlobalCoverageInfo? globalCoverage, CoverageBackfillData? backfillData, CIEnvironmentValues ciEnvironmentValues)
    {
        if (globalCoverage is null ||
            backfillData is not { IsPresent: true, IsValid: true })
        {
            return new CoverageBackfillResult(coverageDataEvaluated: false, matchedFiles: 0, updatedFiles: 0);
        }

        var matchedBackendKeys = new List<string>();
        var pathMatchTracker = new CoverageBackfillPathMatchTracker();
        var pendingUpdates = new List<PendingBitmapUpdate>();
        var unsafeBackendKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in globalCoverage.Components)
        {
            foreach (var file in component.Files)
            {
                if (file.Path is null || file.ExecutableBitmap is null)
                {
                    continue;
                }

                if (!TryGetBackendCoverage(backfillData, file.Path, ciEnvironmentValues, pathMatchTracker, out var backendKey, out var backendExecutedBitmap, out var rejectedUnsafeMatch))
                {
                    if (rejectedUnsafeMatch && backendKey.Length > 0)
                    {
                        unsafeBackendKeys.Add(backendKey);
                    }

                    continue;
                }

                matchedBackendKeys.Add(backendKey);
                if (TryGetMergedExecutedBitmap(file, backendExecutedBitmap, out var mergedBitmap))
                {
                    pendingUpdates.Add(new PendingBitmapUpdate(backendKey, file, mergedBitmap));
                }
            }
        }

        var backendFileCount = CountBackendFilesWithCoverage(backfillData);
        var matchedFiles = 0;
        var publishableBackendKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var backendKey in matchedBackendKeys)
        {
            if (!unsafeBackendKeys.Contains(backendKey))
            {
                matchedFiles++;
                publishableBackendKeys.Add(backendKey);
            }
        }

        var updatedFiles = 0;
        foreach (var update in pendingUpdates)
        {
            if (unsafeBackendKeys.Contains(update.BackendKey))
            {
                continue;
            }

            update.File.ExecutedBitmap = update.ExecutedBitmap;
            updatedFiles++;
        }

        if (updatedFiles > 0)
        {
            globalCoverage.ClearData();
        }

        return new CoverageBackfillResult(
            coverageDataEvaluated: true,
            matchedFiles,
            updatedFiles,
            hasBackendCoverage: backendFileCount > 0,
            canPublishCoverage: CanPublishBackfilledCoverage(backfillData, publishableBackendKeys));
    }

    private static bool CanPublishBackfilledCoverage(CoverageBackfillData backfillData, HashSet<string> publishableBackendKeys)
    {
        foreach (var item in backfillData.ExecutedLinesByRelativePath)
        {
            if (HasActiveBits(item.Value) &&
                !publishableBackendKeys.Contains(item.Key))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetMergedExecutedBitmap(FileCoverageInfo file, byte[] backendExecutedBitmap, out byte[] mergedBitmap)
    {
        mergedBitmap = [];
        if (file.ExecutableBitmap is null)
        {
            return false;
        }

        if (!HasActiveBits(backendExecutedBitmap))
        {
            return false;
        }

        var existingExecutedBitmap = file.ExecutedBitmap;
        if (existingExecutedBitmap is null)
        {
            mergedBitmap = CopyBitmap(backendExecutedBitmap);
            return true;
        }

        using (var backendBitmap = new FileBitmap(backendExecutedBitmap))
        {
            using var existingExecutedFileBitmap = new FileBitmap(existingExecutedBitmap);
            var mergedFileBitmap = backendBitmap | existingExecutedFileBitmap;
            var merged = mergedFileBitmap.GetInternalArrayOrToArrayAndDispose();
            if (existingExecutedBitmap.AsSpan().SequenceEqual(merged))
            {
                return false;
            }

            mergedBitmap = merged;
            return true;
        }
    }

    private static byte[] CopyBitmap(byte[] bitmap)
    {
        var copy = new byte[bitmap.Length];
        Array.Copy(bitmap, copy, bitmap.Length);
        return copy;
    }

    /// <summary>
    /// Finds backend coverage for an internal coverage file path using both raw and source-root-relative forms.
    /// </summary>
    private static bool TryGetBackendCoverage(CoverageBackfillData backfillData, string path, CIEnvironmentValues ciEnvironmentValues, CoverageBackfillPathMatchTracker pathMatchTracker, out string backendKey, out byte[] backendBitmap, out bool rejectedUnsafeMatch)
    {
        rejectedUnsafeMatch = false;
        if (!IsPathRootedCrossPlatform(path) &&
            Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            rejectedUnsafeMatch = true;
            backendKey = string.Empty;
            backendBitmap = [];
            return false;
        }

        var relativePath = ciEnvironmentValues.MakeRelativePathFromSourceRoot(path, false);
        if (IsPathRootedCrossPlatform(path))
        {
            if (StringUtil.IsNullOrWhiteSpace(ciEnvironmentValues.SourceRoot) ||
                !IsSafeRelativePathCandidate(relativePath, path) ||
                !CoverageBackfillPathMatcher.TryGetBackendCoverage(backfillData, [relativePath], allowSuffixMatch: false, out var rootedMatch))
            {
                backendKey = string.Empty;
                backendBitmap = [];
                return false;
            }

            return TryRecordPathMatch(rootedMatch, pathMatchTracker, out backendKey, out backendBitmap, out rejectedUnsafeMatch);
        }

        if (CoverageBackfillPathMatcher.TryGetBackendCoverage(backfillData, [path, relativePath], out var match))
        {
            return TryRecordPathMatch(match, pathMatchTracker, out backendKey, out backendBitmap, out rejectedUnsafeMatch);
        }

        backendKey = string.Empty;
        backendBitmap = [];
        return false;
    }

    private static bool TryRecordPathMatch(CoverageBackfillPathMatch match, CoverageBackfillPathMatchTracker pathMatchTracker, out string backendKey, out byte[] backendBitmap, out bool rejectedUnsafeMatch)
    {
        rejectedUnsafeMatch = false;
        if (!pathMatchTracker.TryRecord(match))
        {
            rejectedUnsafeMatch = true;
            backendKey = match.BackendKey;
            backendBitmap = match.Bitmap;
            return false;
        }

        backendKey = match.BackendKey;
        backendBitmap = match.Bitmap;
        return true;
    }

    private static bool IsSafeRelativePathCandidate(string candidate, string originalPath)
    {
        if (StringUtil.IsNullOrWhiteSpace(candidate) ||
            candidate.Equals(originalPath, StringComparison.Ordinal) ||
            IsPathRootedCrossPlatform(candidate) ||
            Uri.TryCreate(candidate, UriKind.Absolute, out _) ||
            candidate.Equals("..", StringComparison.Ordinal) ||
            candidate.StartsWith("../", StringComparison.Ordinal) ||
            candidate.StartsWith("..\\", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsPathRootedCrossPlatform(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return true;
        }

        if (path.StartsWith("\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        return path.Length >= 3 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               path[2] is '\\' or '/';
    }

    private static int CountBackendFilesWithCoverage(CoverageBackfillData backfillData)
    {
        var count = 0;
        foreach (var backendBitmap in backfillData.ExecutedLinesByRelativePath.Values)
        {
            if (HasActiveBits(backendBitmap))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasActiveBits(byte[]? bitmap)
    {
        if (bitmap is null)
        {
            return false;
        }

        foreach (var value in bitmap)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct PendingBitmapUpdate
    {
        public PendingBitmapUpdate(string backendKey, FileCoverageInfo file, byte[] executedBitmap)
        {
            BackendKey = backendKey;
            File = file;
            ExecutedBitmap = executedBitmap;
        }

        public string BackendKey { get; }

        public FileCoverageInfo File { get; }

        public byte[] ExecutedBitmap { get; }
    }
}
