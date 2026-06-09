// <copyright file="CoverletCoverageBackfill.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// Result of attempting to apply backend skipped-test coverage to one Coverlet result.
/// </summary>
internal enum CoverletCoverageBackfillApplyResult
{
    /// <summary>
    /// The result could not be safely reconciled with backend coverage.
    /// </summary>
    Failed,

    /// <summary>
    /// The current Coverlet result does not represent any backend-covered skipped-test file.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// Backend coverage was safely reconciled with the current Coverlet result.
    /// </summary>
    Applied
}

/// <summary>
/// Mutates Coverlet's in-memory module model with backend ITR skipped-test line coverage before Coverlet computes summaries.
/// </summary>
internal static class CoverletCoverageBackfill
{
    /// <summary>
    /// Caches Coverlet method-value <c>Lines</c> properties so per-method backfill does not repeat reflection lookup.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo> LinesProperties = new();

    /// <summary>
    /// Caches Coverlet method-value <c>Lines</c> fields used by older Coverlet versions.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FieldInfo> LinesFields = new();

    /// <summary>
    /// Applies backend skipped-test coverage to existing Coverlet line hit entries.
    /// </summary>
    /// <param name="modules">Coverlet modules object.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="updatedLines">Number of existing Coverlet line entries changed from not covered to covered.</param>
    /// <returns>True when a valid backend-aware path was used.</returns>
    public static bool TryApply(object? modules, CoverageBackfillData? backfillData, out int updatedLines)
        => TryApplyForCurrentResult(modules, backfillData, CIEnvironmentValues.Instance, out updatedLines) == CoverletCoverageBackfillApplyResult.Applied;

    /// <summary>
    /// Applies backend skipped-test coverage to existing Coverlet line hit entries.
    /// </summary>
    /// <param name="modules">Coverlet modules object.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="ciEnvironmentValues">CI environment values used to derive source-root-relative coverage paths.</param>
    /// <param name="updatedLines">Number of existing Coverlet line entries changed from not covered to covered.</param>
    /// <returns>True when a valid backend-aware path was used.</returns>
    internal static bool TryApply(object? modules, CoverageBackfillData? backfillData, CIEnvironmentValues ciEnvironmentValues, out int updatedLines)
        => TryApplyForCurrentResult(modules, backfillData, ciEnvironmentValues, out updatedLines) == CoverletCoverageBackfillApplyResult.Applied;

    /// <summary>
    /// Applies backend skipped-test coverage and distinguishes unsafe failures from unrelated Coverlet results.
    /// </summary>
    /// <param name="modules">Coverlet modules object.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="ciEnvironmentValues">CI environment values used to derive source-root-relative coverage paths.</param>
    /// <param name="updatedLines">Number of existing Coverlet line entries changed from not covered to covered.</param>
    /// <returns>Detailed result for the current Coverlet result.</returns>
    internal static CoverletCoverageBackfillApplyResult TryApplyForCurrentResult(object? modules, CoverageBackfillData? backfillData, CIEnvironmentValues ciEnvironmentValues, out int updatedLines)
        => TryApplyForCurrentResultCore(modules, backfillData, ciEnvironmentValues, out updatedLines, out _, out _);

    /// <summary>
    /// Applies backend skipped-test coverage and returns validation data that can be merged with other Coverlet results from the same session.
    /// </summary>
    /// <param name="modules">Coverlet modules object.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="ciEnvironmentValues">CI environment values used to derive source-root-relative coverage paths.</param>
    /// <param name="updatedLines">Number of existing Coverlet line entries changed from not covered to covered.</param>
    /// <param name="backfillValidation">Validation data for the backend files represented by this result.</param>
    /// <returns>Detailed result for the current Coverlet result.</returns>
    internal static CoverletCoverageBackfillApplyResult TryApplyForCurrentResult(
        object? modules,
        CoverageBackfillData? backfillData,
        CIEnvironmentValues ciEnvironmentValues,
        out int updatedLines,
        out CodeCoverageBackfillValidation? backfillValidation)
        => TryApplyForCurrentResultCore(modules, backfillData, ciEnvironmentValues, out updatedLines, out backfillValidation, out _);

    /// <summary>
    /// Applies backend skipped-test coverage and returns a rollback handle for temporary partial-result mutation.
    /// </summary>
    /// <param name="modules">Coverlet modules object.</param>
    /// <param name="backfillData">Backend coverage data returned by the skippable-tests endpoint.</param>
    /// <param name="ciEnvironmentValues">CI environment values used to derive source-root-relative coverage paths.</param>
    /// <param name="updatedLines">Number of existing Coverlet line entries changed from not covered to covered.</param>
    /// <param name="backfillValidation">Validation data for the backend files represented by this result.</param>
    /// <param name="rollback">Handle that can revert applied line mutations before Coverlet serializes an unsafe partial result.</param>
    /// <returns>Detailed result for the current Coverlet result.</returns>
    internal static CoverletCoverageBackfillApplyResult TryApplyForCurrentResult(
        object? modules,
        CoverageBackfillData? backfillData,
        CIEnvironmentValues ciEnvironmentValues,
        out int updatedLines,
        out CodeCoverageBackfillValidation? backfillValidation,
        out CoverletCoverageBackfillRollback? rollback)
        => TryApplyForCurrentResultCore(modules, backfillData, ciEnvironmentValues, out updatedLines, out backfillValidation, out rollback);

    private static CoverletCoverageBackfillApplyResult TryApplyForCurrentResultCore(
        object? modules,
        CoverageBackfillData? backfillData,
        CIEnvironmentValues ciEnvironmentValues,
        out int updatedLines,
        out CodeCoverageBackfillValidation? backfillValidation,
        out CoverletCoverageBackfillRollback? rollback)
    {
        updatedLines = 0;
        backfillValidation = null;
        rollback = null;
        if (modules is null || backfillData is not { IsPresent: true, IsValid: true })
        {
            return CoverletCoverageBackfillApplyResult.Failed;
        }

        if (modules is not IDictionary modulesDictionary)
        {
            return CoverletCoverageBackfillApplyResult.Failed;
        }

        List<LineHitUpdate>? pendingUpdates = null;
        var unsafePathMatch = false;
        var pathMatchTracker = new CoverageBackfillPathMatchTracker();
        var matchedBackendPaths = new HashSet<string>(StringComparer.Ordinal);
        var representedBackendLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var expectedCoveredLineCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var localCandidateByBackendPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var unsupportedLineHitDictionary = false;
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

                if (documentEntry.Key is not string documentPath)
                {
                    continue;
                }

                if (!TryGetBackendCoverage(backfillData, documentPath, ciEnvironmentValues, pathMatchTracker, out var backendKey, out var backendBitmap, out var normalizedLocalCandidate, out var rejectedUnsafeMatch))
                {
                    unsafePathMatch |= rejectedUnsafeMatch;
                    continue;
                }

                if (HasActiveBits(backendBitmap))
                {
                    matchedBackendPaths.Add(backendKey);
                    expectedCoveredLineCounts[backendKey] = CountCoveredLines(backendBitmap);
                    localCandidateByBackendPath[backendKey] = normalizedLocalCandidate;
                }

                if (!CollectBackfillUpdates(
                    documentEntry.Value,
                    backendBitmap,
                    GetOrCreateLineSet(representedBackendLines, backendKey),
                    ref pendingUpdates))
                {
                    unsupportedLineHitDictionary = true;
                }
            }
        }

        var requiredBackendLines = GetBackendLinesWithCoverage(backfillData);
        var activeBackendFileCount = requiredBackendLines.Count;
        if (unsafePathMatch ||
            unsupportedLineHitDictionary)
        {
            return CoverletCoverageBackfillApplyResult.Failed;
        }

        if (activeBackendFileCount > 0 && matchedBackendPaths.Count == 0)
        {
            return CoverletCoverageBackfillApplyResult.NotApplicable;
        }

        backfillValidation = CodeCoverageBackfillValidation.Create(activeBackendFileCount, expectedCoveredLineCounts, representedBackendLines, localCandidateByBackendPath, requiredBackendLinesByBackendPath: requiredBackendLines);
        if (!backfillValidation.CanPublish())
        {
            backfillValidation = null;
            return CoverletCoverageBackfillApplyResult.Failed;
        }

        if (pendingUpdates is not null)
        {
            if (!TryApplyUpdates(pendingUpdates, out updatedLines, out var appliedUpdates))
            {
                return CoverletCoverageBackfillApplyResult.Failed;
            }

            if (appliedUpdates is not null && appliedUpdates.Count > 0)
            {
                rollback = new CoverletCoverageBackfillRollback(appliedUpdates);
            }
        }

        return CoverletCoverageBackfillApplyResult.Applied;
    }

    private static bool CollectBackfillUpdates(object? classes, byte[] backendBitmap, HashSet<int> representedBackendLines, ref List<LineHitUpdate>? pendingUpdates)
    {
        var backendFileBitmap = new FileBitmap(backendBitmap);
        try
        {
            if (classes is not IDictionary classesDictionary)
            {
                return true;
            }

            var coveredLocalBackendLines = new HashSet<int>();
            var pendingUpdatesByBackendLine = new Dictionary<int, LineHitUpdate>();
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

                    if (!TryGetLineHits(methodEntry.Value, out var lineHits, out var unsupportedLineHits))
                    {
                        if (unsupportedLineHits)
                        {
                            return false;
                        }

                        continue;
                    }

                    var lineKeys = new List<object>();
                    foreach (var lineKey in lineHits.Keys)
                    {
                        if (lineKey is not null)
                        {
                            lineKeys.Add(lineKey);
                        }
                    }

                    foreach (var lineKey in lineKeys)
                    {
                        if (!TryConvertToInt(lineKey, out var line) ||
                            !IsBackendLineCovered(ref backendFileBitmap, line))
                        {
                            continue;
                        }

                        representedBackendLines.Add(line);

                        if (!TryConvertToInt(lineHits[lineKey], out var hits))
                        {
                            return false;
                        }

                        if (hits > 0)
                        {
                            coveredLocalBackendLines.Add(line);
                            pendingUpdatesByBackendLine.Remove(line);
                            continue;
                        }

                        if (coveredLocalBackendLines.Contains(line) ||
                            pendingUpdatesByBackendLine.ContainsKey(line))
                        {
                            continue;
                        }

                        if (!TryGetCoveredValue(lineHits, lineKey, out var coveredValue))
                        {
                            return false;
                        }

                        pendingUpdatesByBackendLine[line] = new LineHitUpdate(lineHits, lineKey, lineHits[lineKey], coveredValue);
                    }
                }
            }

            if (pendingUpdatesByBackendLine.Count > 0)
            {
                pendingUpdates ??= new List<LineHitUpdate>();
                pendingUpdates.AddRange(pendingUpdatesByBackendLine.Values);
            }

            return true;
        }
        finally
        {
            backendFileBitmap.Dispose();
        }
    }

    private static bool TryGetLineHits(object? methodValue, out IDictionary lineHits, out bool unsupportedLineHits)
    {
        unsupportedLineHits = false;
        if (methodValue is IDictionary directDictionary)
        {
            return TryAcceptLineHitDictionary(directDictionary, out lineHits, out unsupportedLineHits);
        }

        if (TryGetLinesDictionary(methodValue, out var linesDictionary))
        {
            return TryAcceptLineHitDictionary(linesDictionary, out lineHits, out unsupportedLineHits);
        }

        lineHits = default!;
        return false;
    }

    private static bool TryAcceptLineHitDictionary(IDictionary candidate, out IDictionary lineHits, out bool unsupportedLineHits)
    {
        lineHits = default!;
        unsupportedLineHits = false;
        var sawEntry = false;
        foreach (var key in candidate.Keys)
        {
            if (key is null)
            {
                continue;
            }

            sawEntry = true;
            if (!TryConvertToInt(key, out _) || !TryConvertToInt(candidate[key], out _))
            {
                unsupportedLineHits = true;
                return false;
            }
        }

        if (!sawEntry)
        {
            return false;
        }

        lineHits = candidate;
        return true;
    }

    private static bool TryGetCoveredValue(IDictionary lineHits, object lineKey, out object coveredValue)
    {
        coveredValue = 1;
        if (lineHits.IsReadOnly || lineHits.IsFixedSize)
        {
            return false;
        }

        var currentValue = lineHits[lineKey];
        if (currentValue is not int)
        {
            return false;
        }

        var valueType = GetDictionaryValueType(lineHits.GetType());
        if (valueType is null ||
            valueType == typeof(object) ||
            valueType.IsAssignableFrom(typeof(int)))
        {
            return true;
        }

        return false;
    }

    private static Type? GetDictionaryValueType(Type dictionaryType)
    {
        foreach (var interfaceType in dictionaryType.GetInterfaces())
        {
            if (interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                return interfaceType.GetGenericArguments()[1];
            }
        }

        return null;
    }

    private static bool TryApplyUpdates(List<LineHitUpdate> pendingUpdates, out int updatedLines, out List<LineHitUpdate>? appliedUpdates)
    {
        updatedLines = 0;
        appliedUpdates = null;
        try
        {
            foreach (var update in pendingUpdates)
            {
                update.LineHits[update.LineKey] = update.CoveredValue;
                appliedUpdates ??= new List<LineHitUpdate>();
                appliedUpdates.Add(update);
                updatedLines++;
            }

            return true;
        }
        catch (Exception)
        {
            if (appliedUpdates is not null)
            {
                foreach (var update in appliedUpdates)
                {
                    try
                    {
                        update.LineHits[update.LineKey] = update.OriginalValue;
                    }
                    catch (Exception)
                    {
                        // Best effort rollback only; the pre-validation path should make this exceptional.
                    }
                }
            }

            updatedLines = 0;
            return false;
        }
    }

    /// <summary>
    /// Gets a Coverlet method wrapper's <c>Lines</c> dictionary without allowing unsupported wrapper shapes to escape the try-based backfill path.
    /// </summary>
    /// <param name="methodValue">Coverlet method value to inspect.</param>
    /// <param name="linesDictionary">The resolved line-hit dictionary when available.</param>
    /// <returns>True when a line-hit dictionary was resolved.</returns>
    private static bool TryGetLinesDictionary(object? methodValue, out IDictionary linesDictionary)
    {
        var linesProperty = GetLinesProperty(methodValue);
        if (linesProperty is not null)
        {
            try
            {
                if (linesProperty.GetValue(methodValue) is IDictionary dictionary)
                {
                    linesDictionary = dictionary;
                    return true;
                }
            }
            catch (Exception)
            {
                // Unsupported Coverlet method shapes are ignored by the Try API.
            }
        }

        var linesField = GetLinesField(methodValue);
        if (linesField is null)
        {
            linesDictionary = default!;
            return false;
        }

        try
        {
            if (linesField.GetValue(methodValue) is IDictionary dictionary)
            {
                linesDictionary = dictionary;
                return true;
            }
        }
        catch (Exception)
        {
            // Unsupported Coverlet method shapes are ignored by the Try API.
        }

        linesDictionary = default!;
        return false;
    }

    /// <summary>
    /// Gets and caches the Coverlet <c>Lines</c> property for method values that expose line hits through a wrapper object.
    /// </summary>
    /// <param name="methodValue">Coverlet method value to inspect.</param>
    /// <returns>The cached <c>Lines</c> property when present.</returns>
    private static PropertyInfo? GetLinesProperty(object? methodValue)
    {
        if (methodValue is null)
        {
            return null;
        }

        var type = methodValue.GetType();
        if (LinesProperties.TryGetValue(type, out var cachedProperty))
        {
            return cachedProperty;
        }

        var property = type.GetProperty("Lines");
        if (property is not null)
        {
            LinesProperties[type] = property;
        }

        return property;
    }

    /// <summary>
    /// Gets and caches the Coverlet <c>Lines</c> field for method values that expose line hits through a public field.
    /// </summary>
    /// <param name="methodValue">Coverlet method value to inspect.</param>
    /// <returns>The cached <c>Lines</c> field when present.</returns>
    private static FieldInfo? GetLinesField(object? methodValue)
    {
        if (methodValue is null)
        {
            return null;
        }

        var type = methodValue.GetType();
        if (LinesFields.TryGetValue(type, out var cachedField))
        {
            return cachedField;
        }

        var field = type.GetField("Lines");
        if (field is not null)
        {
            LinesFields[type] = field;
        }

        return field;
    }

    private static bool TryGetBackendCoverage(CoverageBackfillData backfillData, string sourcePath, CIEnvironmentValues ciEnvironmentValues, CoverageBackfillPathMatchTracker pathMatchTracker, out string backendKey, out byte[] backendBitmap, out string normalizedLocalCandidate, out bool rejectedUnsafeMatch)
    {
        rejectedUnsafeMatch = false;
        if (!IsPathRootedCrossPlatform(sourcePath) &&
            Uri.TryCreate(sourcePath, UriKind.Absolute, out _))
        {
            rejectedUnsafeMatch = true;
            backendKey = string.Empty;
            backendBitmap = [];
            normalizedLocalCandidate = string.Empty;
            return false;
        }

        var sourceRootRelativePath = ciEnvironmentValues.MakeRelativePathFromSourceRoot(sourcePath, false);
        if (IsPathRootedCrossPlatform(sourcePath))
        {
            if (StringUtil.IsNullOrWhiteSpace(ciEnvironmentValues.SourceRoot) ||
                !IsSafeRelativePathCandidate(sourceRootRelativePath, sourcePath))
            {
                rejectedUnsafeMatch = true;
                backendKey = string.Empty;
                backendBitmap = [];
                normalizedLocalCandidate = string.Empty;
                return false;
            }

            if (!CoverageBackfillPathMatcher.TryGetBackendCoverage(backfillData, [sourceRootRelativePath], allowSuffixMatch: false, out var rootedMatch, out var hasAmbiguousActiveRootedMatch))
            {
                rejectedUnsafeMatch = hasAmbiguousActiveRootedMatch;
                backendKey = string.Empty;
                backendBitmap = [];
                normalizedLocalCandidate = string.Empty;
                return false;
            }

            return TryRecordPathMatch(rootedMatch, pathMatchTracker, out backendKey, out backendBitmap, out normalizedLocalCandidate, out rejectedUnsafeMatch);
        }

        var pathCandidates = new[] { sourcePath, sourceRootRelativePath };
        if (CoverageBackfillPathMatcher.TryGetBackendCoverage(backfillData, pathCandidates, allowSuffixMatch: true, out var match, out var hasAmbiguousActiveMatch))
        {
            return TryRecordPathMatch(match, pathMatchTracker, out backendKey, out backendBitmap, out normalizedLocalCandidate, out rejectedUnsafeMatch);
        }

        rejectedUnsafeMatch = hasAmbiguousActiveMatch;
        backendKey = string.Empty;
        backendBitmap = [];
        normalizedLocalCandidate = string.Empty;
        return false;
    }

    private static bool TryRecordPathMatch(CoverageBackfillPathMatch match, CoverageBackfillPathMatchTracker pathMatchTracker, out string backendKey, out byte[] backendBitmap, out string normalizedLocalCandidate, out bool rejectedUnsafeMatch)
    {
        rejectedUnsafeMatch = false;
        if (!pathMatchTracker.TryRecord(match))
        {
            rejectedUnsafeMatch = true;
            backendKey = string.Empty;
            backendBitmap = [];
            normalizedLocalCandidate = string.Empty;
            return false;
        }

        backendKey = match.BackendKey;
        backendBitmap = match.Bitmap;
        normalizedLocalCandidate = match.NormalizedLocalCandidate;
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

    private static HashSet<int> GetOrCreateLineSet(Dictionary<string, HashSet<int>> representedBackendLines, string backendKey)
    {
        if (!representedBackendLines.TryGetValue(backendKey, out var lineSet))
        {
            lineSet = new HashSet<int>();
            representedBackendLines[backendKey] = lineSet;
        }

        return lineSet;
    }

    private static Dictionary<string, HashSet<int>> GetBackendLinesWithCoverage(CoverageBackfillData backfillData)
    {
        var requiredBackendLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var item in backfillData.ExecutedLinesByRelativePath)
        {
            var coveredLines = GetCoveredLines(item.Value);
            if (coveredLines.Count > 0)
            {
                requiredBackendLines[item.Key] = coveredLines;
            }
        }

        return requiredBackendLines;
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

    private static bool IsBackendLineCovered(ref FileBitmap bitmap, int line)
    {
        return line > 0 && line <= bitmap.BitCount && bitmap.Get(line);
    }

    /// <summary>
    /// Counts covered backend lines in a file bitmap so validation can describe the matched backend coverage.
    /// </summary>
    /// <param name="bitmap">Backend coverage bitmap.</param>
    /// <returns>Number of covered backend lines.</returns>
    private static int CountCoveredLines(ref FileBitmap bitmap)
    {
        var coveredLines = 0;
        for (var line = 1; line <= bitmap.BitCount; line++)
        {
            if (bitmap.Get(line))
            {
                coveredLines++;
            }
        }

        return coveredLines;
    }

    private static int CountCoveredLines(byte[] bitmap)
    {
        var fileBitmap = new FileBitmap(bitmap);
        try
        {
            return CountCoveredLines(ref fileBitmap);
        }
        finally
        {
            fileBitmap.Dispose();
        }
    }

    private static HashSet<int> GetCoveredLines(byte[] bitmap)
    {
        var fileBitmap = new FileBitmap(bitmap);
        try
        {
            var coveredLines = new HashSet<int>();
            for (var line = 1; line <= fileBitmap.BitCount; line++)
            {
                if (fileBitmap.Get(line))
                {
                    coveredLines.Add(line);
                }
            }

            return coveredLines;
        }
        finally
        {
            fileBitmap.Dispose();
        }
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

    /// <summary>
    /// Stores a deferred Coverlet line-hit mutation until backend line reconciliation succeeds.
    /// </summary>
    internal readonly struct LineHitUpdate
    {
        public LineHitUpdate(IDictionary lineHits, object lineKey, object? originalValue, object coveredValue)
        {
            LineHits = lineHits;
            LineKey = lineKey;
            OriginalValue = originalValue;
            CoveredValue = coveredValue;
        }

        public IDictionary LineHits { get; }

        public object LineKey { get; }

        public object? OriginalValue { get; }

        public object CoveredValue { get; }
    }

    /// <summary>
    /// Reverts Coverlet line-hit mutations that were applied only to calculate a temporary coverage result.
    /// </summary>
    internal sealed class CoverletCoverageBackfillRollback
    {
        private readonly List<LineHitUpdate> _appliedUpdates;
        private bool _rolledBack;

        internal CoverletCoverageBackfillRollback(List<LineHitUpdate> appliedUpdates)
        {
            _appliedUpdates = appliedUpdates;
        }

        public bool TryRollback()
        {
            if (_rolledBack)
            {
                return true;
            }

            var succeeded = true;
            for (var i = _appliedUpdates.Count - 1; i >= 0; i--)
            {
                var update = _appliedUpdates[i];
                try
                {
                    update.LineHits[update.LineKey] = update.OriginalValue;
                }
                catch (Exception)
                {
                    succeeded = false;
                }
            }

            _rolledBack = true;
            return succeeded;
        }
    }
}
