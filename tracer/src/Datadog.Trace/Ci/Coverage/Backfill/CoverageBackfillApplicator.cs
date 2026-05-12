// <copyright file="CoverageBackfillApplicator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;

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
    /// <returns>Summary of the backfill operation.</returns>
    public static CoverageBackfillResult ApplyToGlobalCoverage(GlobalCoverageInfo? globalCoverage, CoverageBackfillData? backfillData)
    {
        if (globalCoverage is null ||
            backfillData is not { IsPresent: true, IsValid: true })
        {
            return new CoverageBackfillResult(applied: false, matchedFiles: 0, updatedFiles: 0);
        }

        var matchedFiles = 0;
        var updatedFiles = 0;
        foreach (var component in globalCoverage.Components)
        {
            foreach (var file in component.Files)
            {
                if (file.Path is null ||
                    file.ExecutableBitmap is null ||
                    !backfillData.ExecutedLinesByRelativePath.TryGetValue(NormalizeLocalPath(file.Path), out var backendExecutedBitmap))
                {
                    continue;
                }

                matchedFiles++;
                if (ApplyBackendBitmap(file, backendExecutedBitmap))
                {
                    updatedFiles++;
                }
            }
        }

        return new CoverageBackfillResult(applied: true, matchedFiles, updatedFiles);
    }

    private static bool ApplyBackendBitmap(FileCoverageInfo file, byte[] backendExecutedBitmap)
    {
        if (file.ExecutableBitmap is null)
        {
            return false;
        }

        using var backendBitmap = new FileBitmap(backendExecutedBitmap);
        using var executableBitmap = new FileBitmap(file.ExecutableBitmap);
        using var maskedBackendBitmap = backendBitmap & executableBitmap;
        if (!maskedBackendBitmap.HasActiveBits())
        {
            return false;
        }

        var before = file.ExecutedBitmap;
        if (before is null)
        {
            file.AggregateExecutedBitmap(maskedBackendBitmap.GetInternalArrayOrToArrayAndDispose());
            return true;
        }

        using var beforeBitmap = new FileBitmap(before);
        using var mergedBitmap = FileBitmap.Or(maskedBackendBitmap, beforeBitmap, reuseBufferFromBitmapA: false);
        var merged = mergedBitmap.GetInternalArrayOrToArrayAndDispose();
        if (AreEqual(before, merged))
        {
            return false;
        }

        file.ExecutedBitmap = merged;
        file.ClearCachedData();
        return true;
    }

    private static string NormalizeLocalPath(string path)
    {
        var relativePath = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(path, false);
        return CoverageBackfillData.NormalizePath(relativePath);
    }

    private static bool AreEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
