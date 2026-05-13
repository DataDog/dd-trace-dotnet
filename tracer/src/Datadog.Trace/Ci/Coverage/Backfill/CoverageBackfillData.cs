// <copyright file="CoverageBackfillData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.Coverage.Util;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Stores the backend-provided line coverage that can be used to backfill tests skipped by Intelligent Test Runner.
/// </summary>
internal sealed class CoverageBackfillData
{
    /// <summary>
    /// Represents a response where the backend did not include the coverage contract.
    /// </summary>
    public static readonly CoverageBackfillData Missing = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageBackfillData"/> class for JSON deserialization.
    /// </summary>
    public CoverageBackfillData()
    {
        IsPresent = false;
        IsValid = true;
        ExecutedLinesByRelativePath = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    }

    private CoverageBackfillData(bool isPresent, bool isValid, string? error, Dictionary<string, byte[]> executedLinesByRelativePath)
    {
        IsPresent = isPresent;
        IsValid = isValid;
        Error = error;
        ExecutedLinesByRelativePath = executedLinesByRelativePath;
        foreach (var bitmap in executedLinesByRelativePath.Values)
        {
            TotalBitmapBytes += bitmap.Length;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether `meta.coverage` was present in the skippable-tests response.
    /// </summary>
    public bool IsPresent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every backend bitmap was decoded successfully.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the parse error that made the backend coverage unusable, when one exists.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the decoded executed-line bitmaps keyed by normalized repository-relative source path.
    /// </summary>
    public Dictionary<string, byte[]> ExecutedLinesByRelativePath { get; set; }

    /// <summary>
    /// Gets or sets the total number of decoded bitmap bytes.
    /// </summary>
    public int TotalBitmapBytes { get; set; }

    /// <summary>
    /// Creates backend coverage data from the raw `meta.coverage` JSON map.
    /// </summary>
    /// <param name="coverage">Map from repository-relative source path to a base64-encoded bitmap.</param>
    /// <returns>Decoded backend coverage data, including invalid state when parsing fails.</returns>
    public static CoverageBackfillData FromBackendCoverage(IDictionary<string, string>? coverage)
    {
        if (coverage is null)
        {
            return Missing;
        }

        var decodedCoverage = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in coverage)
        {
            string path;
            try
            {
                path = NormalizePath(item.Key);
            }
            catch (Exception ex)
            {
                return new CoverageBackfillData(
                    isPresent: true,
                    isValid: false,
                    error: $"Invalid coverage path: {ex.Message}",
                    new Dictionary<string, byte[]>());
            }

            byte[] bitmap;
            try
            {
                bitmap = Convert.FromBase64String(item.Value);
            }
            catch (Exception ex)
            {
                return new CoverageBackfillData(
                    isPresent: true,
                    isValid: false,
                    error: $"Invalid coverage bitmap for '{path}': {ex.Message}",
                    new Dictionary<string, byte[]>());
            }

            if (decodedCoverage.TryGetValue(path, out var existingBitmap))
            {
                decodedCoverage[path] = OrBitmaps(existingBitmap, bitmap);
            }
            else
            {
                decodedCoverage[path] = bitmap;
            }
        }

        return new CoverageBackfillData(isPresent: true, isValid: true, error: null, decodedCoverage);
    }

    /// <summary>
    /// Merges multiple valid backend coverage maps by OR-ing file bitmaps with the same repository-relative path.
    /// </summary>
    /// <param name="coverageMaps">Coverage maps to merge.</param>
    /// <returns>A valid coverage map, or <see cref="Missing"/> when no valid map was supplied.</returns>
    public static CoverageBackfillData Merge(IEnumerable<CoverageBackfillData> coverageMaps)
    {
        var sawCoverage = false;
        var mergedCoverage = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var coverageMap in coverageMaps)
        {
            if (coverageMap is not { IsPresent: true, IsValid: true })
            {
                continue;
            }

            sawCoverage = true;
            foreach (var item in coverageMap.ExecutedLinesByRelativePath)
            {
                if (mergedCoverage.TryGetValue(item.Key, out var existingBitmap))
                {
                    mergedCoverage[item.Key] = OrBitmaps(existingBitmap, item.Value);
                }
                else
                {
                    mergedCoverage[item.Key] = item.Value;
                }
            }
        }

        return sawCoverage ? new CoverageBackfillData(isPresent: true, isValid: true, error: null, mergedCoverage) : Missing;
    }

    /// <summary>
    /// Normalizes backend coverage paths to the repository-relative format used for local coverage matching.
    /// </summary>
    /// <param name="path">Path from `meta.coverage`.</param>
    /// <returns>A normalized repository-relative path.</returns>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Coverage path cannot be empty.", nameof(path));
        }

        var normalizedPath = path.Replace('\\', '/');
        while (normalizedPath.StartsWith("/", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Substring(1);
        }

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new ArgumentException("Coverage path cannot point to the repository root.", nameof(path));
        }

        return normalizedPath;
    }

    private static byte[] OrBitmaps(byte[] left, byte[] right)
    {
        using var leftBitmap = new FileBitmap(left);
        using var rightBitmap = new FileBitmap(right);
        var mergedBitmap = FileBitmap.Or(leftBitmap, rightBitmap, reuseBufferFromBitmapA: false);
        return mergedBitmap.GetInternalArrayOrToArrayAndDispose();
    }
}
