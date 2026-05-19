// <copyright file="CoverletCoverageBackfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Globalization;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Util;

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
        if (modules is not IDictionary modulesDictionary)
        {
            return false;
        }

        foreach (var rawModuleEntry in modulesDictionary)
        {
            if (rawModuleEntry is not DictionaryEntry moduleEntry)
            {
                continue;
            }

            if (moduleEntry.Value is not IDictionary documentsDictionary)
            {
                continue;
            }

            foreach (var rawDocumentEntry in documentsDictionary)
            {
                if (rawDocumentEntry is not DictionaryEntry documentEntry)
                {
                    continue;
                }

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
        var backendFileBitmap = new FileBitmap(backendBitmap);
        try
        {
            if (classes is not IDictionary classesDictionary)
            {
                return 0;
            }

            foreach (var rawClassEntry in classesDictionary)
            {
                if (rawClassEntry is not DictionaryEntry classEntry)
                {
                    continue;
                }

                if (classEntry.Value is not IDictionary methodsDictionary)
                {
                    continue;
                }

                foreach (var rawMethodEntry in methodsDictionary)
                {
                    if (rawMethodEntry is not DictionaryEntry methodEntry)
                    {
                        continue;
                    }

                    if (!TryGetLineHits(methodEntry.Value, out var lineHits))
                    {
                        continue;
                    }

                    var lineKeys = new object[lineHits.Keys.Count];
                    lineHits.Keys.CopyTo(lineKeys, 0);
                    foreach (var lineKey in lineKeys)
                    {
                        if (!TryConvertToInt(lineKey, out var line) ||
                            !IsBackendLineCovered(ref backendFileBitmap, line) ||
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
        }
        finally
        {
            backendFileBitmap.Dispose();
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

    private static byte[]? GetBackendBitmap(CoverageBackfillData backfillData, string sourcePath)
    {
        var pathCandidates = new[]
        {
            sourcePath,
            CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(sourcePath, false)
        };
        return CoverageBackfillPathMatcher.GetBackendBitmap(backfillData, pathCandidates);
    }

    private static bool IsBackendLineCovered(ref FileBitmap bitmap, int line)
    {
        return line > 0 && line <= bitmap.BitCount && bitmap.Get(line);
    }

    private static bool TryConvertToInt(object? value, out int result)
    {
        if (value is null)
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
        {
            result = 0;
            return false;
        }
    }
}
