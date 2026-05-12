// <copyright file="CoverletCoverageBackfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Backfill;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// Mutates Coverlet's in-memory module model with backend ITR skipped-test line coverage before Coverlet computes summaries.
/// </summary>
internal static class CoverletCoverageBackfill
{
    /// <summary>
    /// Applies backend skipped-test coverage to existing Coverlet line hit entries.
    /// </summary>
    /// <param name="modules">Coverlet modules object.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="updatedLines">Number of existing Coverlet line entries changed from not covered to covered.</param>
    /// <returns>True when a valid backend-aware path was used.</returns>
    public static bool TryApply(object? modules, CoverageBackfillData? backfillData, out int updatedLines)
    {
        updatedLines = 0;
        if (modules is null || backfillData is not { IsPresent: true, IsValid: true })
        {
            return false;
        }

        var matchedBackendPath = backfillData.ExecutedLinesByRelativePath.Count == 0;
        foreach (DictionaryEntry moduleEntry in EnumerateDictionary(modules))
        {
            foreach (DictionaryEntry documentEntry in EnumerateDictionary(moduleEntry.Value))
            {
                if (documentEntry.Key is not string documentPath ||
                    GetBackendBitmap(backfillData, documentPath) is not { } backendBitmap)
                {
                    continue;
                }

                matchedBackendPath = true;
                updatedLines += BackfillCoverletDocument(documentEntry.Value, backendBitmap);
            }
        }

        return matchedBackendPath;
    }

    private static int BackfillCoverletDocument(object? classes, byte[] backendBitmap)
    {
        var updatedLines = 0;
        foreach (DictionaryEntry classEntry in EnumerateDictionary(classes))
        {
            foreach (DictionaryEntry methodEntry in EnumerateDictionary(classEntry.Value))
            {
                if (!TryGetLineHits(methodEntry.Value, out var lineHits))
                {
                    continue;
                }

                var lineKeys = new object[lineHits.Keys.Count];
                lineHits.Keys.CopyTo(lineKeys, 0);
                foreach (var lineKey in lineKeys)
                {
                    if (!TryConvertToInt(lineKey, out var line) ||
                        !IsBackendLineCovered(backendBitmap, line) ||
                        !TryConvertToInt(lineHits[lineKey], out var hits) ||
                        hits > 0)
                    {
                        continue;
                    }

                    lineHits[lineKey] = 1;
                    updatedLines++;
                }
            }
        }

        return updatedLines;
    }

    private static bool TryGetLineHits(object? methodValue, out IDictionary lineHits)
    {
        if (methodValue is IDictionary directDictionary && LooksLikeLineHitDictionary(directDictionary))
        {
            lineHits = directDictionary;
            return true;
        }

        var linesProperty = methodValue?.GetType().GetProperty("Lines");
        if (linesProperty?.GetValue(methodValue) is IDictionary linesDictionary && LooksLikeLineHitDictionary(linesDictionary))
        {
            lineHits = linesDictionary;
            return true;
        }

        lineHits = default!;
        return false;
    }

    private static bool LooksLikeLineHitDictionary(IDictionary dictionary)
    {
        foreach (var key in dictionary.Keys)
        {
            if (key is null)
            {
                continue;
            }

            return TryConvertToInt(key, out _) && TryConvertToInt(dictionary[key], out _);
        }

        return true;
    }

    private static IEnumerable<DictionaryEntry> EnumerateDictionary(object? value)
    {
        if (value is not IDictionary dictionary)
        {
            yield break;
        }

        foreach (var key in dictionary.Keys)
        {
            if (key is null)
            {
                continue;
            }

            yield return new DictionaryEntry(key, dictionary[key]);
        }
    }

    private static byte[]? GetBackendBitmap(CoverageBackfillData backfillData, string sourcePath)
    {
        var matchingBitmap = default(byte[]);
        foreach (var candidate in GetPathCandidates(sourcePath))
        {
            if (!backfillData.ExecutedLinesByRelativePath.TryGetValue(candidate, out var bitmap))
            {
                continue;
            }

            if (matchingBitmap is not null && !ReferenceEquals(matchingBitmap, bitmap))
            {
                return null;
            }

            matchingBitmap = bitmap;
        }

        return matchingBitmap;
    }

    private static IEnumerable<string> GetPathCandidates(string sourcePath)
    {
        var rawCandidates = new[]
        {
            sourcePath,
            CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourcePath, false)
        };

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

            yield return normalized;
        }
    }

    private static bool IsBackendLineCovered(byte[] bitmap, int line)
    {
        if (line <= 0)
        {
            return false;
        }

        var index = line - 1;
        var byteIndex = index >> 3;
        if (byteIndex >= bitmap.Length)
        {
            return false;
        }

        var bitMask = (byte)(128 >> (index & 7));
        return (bitmap[byteIndex] & bitMask) != 0;
    }

    private static bool TryConvertToInt(object? value, out int result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case long longValue when longValue <= int.MaxValue && longValue >= int.MinValue:
                result = (int)longValue;
                return true;
            case string stringValue:
                return int.TryParse(stringValue, out result);
            default:
                result = 0;
                return false;
        }
    }
}
