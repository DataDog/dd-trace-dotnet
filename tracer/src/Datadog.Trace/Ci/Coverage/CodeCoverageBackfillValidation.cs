// <copyright file="CodeCoverageBackfillValidation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Carries enough backend coverage reconciliation state to validate multiple partial coverage producer results together.
/// </summary>
internal sealed class CodeCoverageBackfillValidation
{
    private static readonly StringComparer LocalCandidateComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Gets or sets the number of backend files that contain skipped-test covered lines.
    /// </summary>
    public int RequiredBackendFilesWithCoverage { get; set; }

    /// <summary>
    /// Gets or sets the expected skipped-test covered line count by normalized backend path.
    /// </summary>
    public Dictionary<string, int> ExpectedCoveredLinesByBackendPath { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the normalized backend paths that contain skipped-test covered lines.
    /// </summary>
    public HashSet<string> RequiredBackendPathsWithCoverage { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the skipped-test covered lines reported by backend path.
    /// </summary>
    public Dictionary<string, int[]> RequiredBackendLinesByBackendPath { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the skipped-test covered lines represented by this producer result, keyed by normalized backend path.
    /// </summary>
    public Dictionary<string, int[]> RepresentedBackendLinesByBackendPath { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the local coverage path candidate used for each normalized backend path.
    /// </summary>
    public Dictionary<string, string> LocalCandidateByBackendPath { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether one backend path matched multiple distinct local candidates.
    /// </summary>
    public bool UnsafePathMatch { get; set; }

    /// <summary>
    /// Creates a validation snapshot from a coverage producer's matched backend coverage.
    /// </summary>
    /// <param name="requiredBackendFilesWithCoverage">Total backend files that contain skipped-test covered lines.</param>
    /// <param name="expectedCoveredLinesByBackendPath">Expected skipped-test covered line count for matched backend files.</param>
    /// <param name="representedBackendLinesByBackendPath">Skipped-test covered lines represented by the coverage result.</param>
    /// <param name="localCandidateByBackendPath">Local coverage path candidate selected for each matched backend path.</param>
    /// <param name="requiredBackendPathsWithCoverage">Backend paths reported with skipped-test covered lines.</param>
    /// <param name="requiredBackendLinesByBackendPath">Backend lines reported with skipped-test coverage.</param>
    /// <returns>Validation snapshot for aggregation.</returns>
    internal static CodeCoverageBackfillValidation Create(
        int requiredBackendFilesWithCoverage,
        IReadOnlyDictionary<string, int> expectedCoveredLinesByBackendPath,
        IReadOnlyDictionary<string, HashSet<int>> representedBackendLinesByBackendPath,
        IReadOnlyDictionary<string, string>? localCandidateByBackendPath = null,
        IReadOnlyCollection<string>? requiredBackendPathsWithCoverage = null,
        IReadOnlyDictionary<string, HashSet<int>>? requiredBackendLinesByBackendPath = null)
    {
        var validation = new CodeCoverageBackfillValidation
        {
            RequiredBackendFilesWithCoverage = Math.Max(requiredBackendFilesWithCoverage, Math.Max(requiredBackendPathsWithCoverage?.Count ?? 0, requiredBackendLinesByBackendPath?.Count ?? 0)),
            ExpectedCoveredLinesByBackendPath = new Dictionary<string, int>(StringComparer.Ordinal),
            RequiredBackendPathsWithCoverage = new HashSet<string>(StringComparer.Ordinal),
            RequiredBackendLinesByBackendPath = new Dictionary<string, int[]>(StringComparer.Ordinal),
            RepresentedBackendLinesByBackendPath = new Dictionary<string, int[]>(StringComparer.Ordinal),
            LocalCandidateByBackendPath = new Dictionary<string, string>(StringComparer.Ordinal)
        };

        if (requiredBackendLinesByBackendPath is not null)
        {
            foreach (var item in requiredBackendLinesByBackendPath)
            {
                validation.RequiredBackendLinesByBackendPath[item.Key] = ToSortedArray(item.Value);
                validation.RequiredBackendPathsWithCoverage.Add(item.Key);
            }
        }

        if (requiredBackendPathsWithCoverage is not null)
        {
            foreach (var backendPath in requiredBackendPathsWithCoverage)
            {
                validation.RequiredBackendPathsWithCoverage.Add(backendPath);
            }
        }

        foreach (var item in expectedCoveredLinesByBackendPath)
        {
            if (item.Value > 0)
            {
                validation.ExpectedCoveredLinesByBackendPath[item.Key] = item.Value;
            }
        }

        foreach (var item in representedBackendLinesByBackendPath)
        {
            validation.RepresentedBackendLinesByBackendPath[item.Key] = ToSortedArray(item.Value);
        }

        if (localCandidateByBackendPath is not null)
        {
            AddLocalCandidates(validation.LocalCandidateByBackendPath, validation, localCandidateByBackendPath);
        }

        return validation;
    }

    /// <summary>
    /// Merges two validation snapshots from coverage results belonging to the same source.
    /// </summary>
    /// <param name="first">First validation snapshot.</param>
    /// <param name="second">Second validation snapshot.</param>
    /// <returns>A merged validation snapshot, or null when neither input contains validation data.</returns>
    internal static CodeCoverageBackfillValidation? Merge(CodeCoverageBackfillValidation? first, CodeCoverageBackfillValidation? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        var merged = new CodeCoverageBackfillValidation
        {
            RequiredBackendFilesWithCoverage = Math.Max(first.RequiredBackendFilesWithCoverage, second.RequiredBackendFilesWithCoverage),
            ExpectedCoveredLinesByBackendPath = CopyExpectedLines(first),
            RequiredBackendPathsWithCoverage = CopyRequiredBackendPaths(first),
            RequiredBackendLinesByBackendPath = new Dictionary<string, int[]>(StringComparer.Ordinal),
            RepresentedBackendLinesByBackendPath = new Dictionary<string, int[]>(StringComparer.Ordinal),
            LocalCandidateByBackendPath = new Dictionary<string, string>(StringComparer.Ordinal),
            UnsafePathMatch = first.UnsafePathMatch || second.UnsafePathMatch
        };

        AddLocalCandidates(merged.LocalCandidateByBackendPath, merged, first.LocalCandidateByBackendPath);
        AddExpectedLines(merged.ExpectedCoveredLinesByBackendPath, second);
        AddRequiredBackendPaths(merged.RequiredBackendPathsWithCoverage, second);
        var requiredLines = CopyRequiredBackendLines(first);
        AddRequiredBackendLines(requiredLines, second);
        foreach (var item in requiredLines)
        {
            merged.RequiredBackendLinesByBackendPath[item.Key] = ToSortedArray(item.Value);
            merged.RequiredBackendPathsWithCoverage.Add(item.Key);
        }

        if (merged.RequiredBackendPathsWithCoverage.Count > 0)
        {
            merged.RequiredBackendFilesWithCoverage = Math.Max(merged.RequiredBackendFilesWithCoverage, merged.RequiredBackendPathsWithCoverage.Count);
        }

        AddLocalCandidates(merged.LocalCandidateByBackendPath, merged, second.LocalCandidateByBackendPath);

        var representedLines = CopyRepresentedLines(first);
        AddRepresentedLines(representedLines, second);
        foreach (var item in representedLines)
        {
            merged.RepresentedBackendLinesByBackendPath[item.Key] = ToSortedArray(item.Value);
        }

        return merged.HasData() ? merged : null;
    }

    /// <summary>
    /// Gets whether this snapshot contains backend validation data.
    /// </summary>
    /// <returns>True when at least one backend path or required-file count was recorded.</returns>
    internal bool HasData()
        => RequiredBackendFilesWithCoverage > 0 ||
           ExpectedCoveredLinesByBackendPath.Count > 0 ||
           RequiredBackendPathsWithCoverage.Count > 0 ||
           RequiredBackendLinesByBackendPath.Count > 0 ||
           RepresentedBackendLinesByBackendPath.Count > 0 ||
           LocalCandidateByBackendPath.Count > 0 ||
           UnsafePathMatch;

    /// <summary>
    /// Gets whether matched backend coverage was applied without path ambiguity and represents every required backend-covered line.
    /// </summary>
    /// <returns>True when this snapshot can validate publishing this source after ITR skipped tests.</returns>
    internal bool CanPublish()
    {
        return !UnsafePathMatch &&
               HasCompleteRequiredBackendLineCoverage(this) &&
               HasCompleteRequiredBackendPathCoverage(this) &&
               HasExpectedCoveredLineCounts(this) &&
               HasRequiredBackendFileCount(this);
    }

    private static bool HasCompleteRequiredBackendLineCoverage(CodeCoverageBackfillValidation validation)
    {
        foreach (var item in validation.RequiredBackendLinesByBackendPath)
        {
            if (!validation.RepresentedBackendLinesByBackendPath.TryGetValue(item.Key, out var representedLines) ||
                representedLines.Length == 0)
            {
                return false;
            }

            foreach (var requiredLine in item.Value)
            {
                if (Array.BinarySearch(representedLines, requiredLine) < 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasCompleteRequiredBackendPathCoverage(CodeCoverageBackfillValidation validation)
    {
        foreach (var backendPath in validation.RequiredBackendPathsWithCoverage)
        {
            if (!HasRepresentedLines(validation, backendPath))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExpectedCoveredLineCounts(CodeCoverageBackfillValidation validation)
    {
        foreach (var item in validation.ExpectedCoveredLinesByBackendPath)
        {
            if (item.Value <= 0)
            {
                continue;
            }

            if (!validation.RepresentedBackendLinesByBackendPath.TryGetValue(item.Key, out var representedLines) ||
                representedLines.Length < item.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasRequiredBackendFileCount(CodeCoverageBackfillValidation validation)
    {
        if (validation.RequiredBackendFilesWithCoverage <= 0)
        {
            return true;
        }

        var representedBackendFilesWithCoverage = 0;
        foreach (var item in validation.RepresentedBackendLinesByBackendPath)
        {
            if (item.Value.Length > 0)
            {
                representedBackendFilesWithCoverage++;
            }
        }

        return representedBackendFilesWithCoverage >= validation.RequiredBackendFilesWithCoverage;
    }

    private static bool HasRepresentedLines(CodeCoverageBackfillValidation validation, string backendPath)
        => validation.RepresentedBackendLinesByBackendPath.TryGetValue(backendPath, out var representedLines) &&
           representedLines.Length > 0;

    private static Dictionary<string, int> CopyExpectedLines(CodeCoverageBackfillValidation validation)
    {
        var expectedLines = new Dictionary<string, int>(StringComparer.Ordinal);
        AddExpectedLines(expectedLines, validation);
        return expectedLines;
    }

    private static HashSet<string> CopyRequiredBackendPaths(CodeCoverageBackfillValidation validation)
    {
        var requiredBackendPaths = new HashSet<string>(StringComparer.Ordinal);
        AddRequiredBackendPaths(requiredBackendPaths, validation);
        return requiredBackendPaths;
    }

    private static Dictionary<string, HashSet<int>> CopyRequiredBackendLines(CodeCoverageBackfillValidation validation)
    {
        var requiredBackendLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        AddRequiredBackendLines(requiredBackendLines, validation);
        return requiredBackendLines;
    }

    private static void AddExpectedLines(Dictionary<string, int> expectedLines, CodeCoverageBackfillValidation validation)
    {
        foreach (var item in validation.ExpectedCoveredLinesByBackendPath)
        {
            if (item.Value <= 0)
            {
                continue;
            }

            if (!expectedLines.TryGetValue(item.Key, out var existing) ||
                item.Value > existing)
            {
                expectedLines[item.Key] = item.Value;
            }
        }
    }

    private static void AddRequiredBackendPaths(HashSet<string> requiredBackendPaths, CodeCoverageBackfillValidation validation)
    {
        foreach (var backendPath in validation.RequiredBackendPathsWithCoverage)
        {
            requiredBackendPaths.Add(backendPath);
        }
    }

    private static void AddRequiredBackendLines(Dictionary<string, HashSet<int>> requiredBackendLines, CodeCoverageBackfillValidation validation)
    {
        foreach (var item in validation.RequiredBackendLinesByBackendPath)
        {
            if (!requiredBackendLines.TryGetValue(item.Key, out var lines))
            {
                lines = [];
                requiredBackendLines[item.Key] = lines;
            }

            foreach (var line in item.Value)
            {
                lines.Add(line);
            }
        }
    }

    private static Dictionary<string, HashSet<int>> CopyRepresentedLines(CodeCoverageBackfillValidation validation)
    {
        var representedLines = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        AddRepresentedLines(representedLines, validation);
        return representedLines;
    }

    private static void AddRepresentedLines(Dictionary<string, HashSet<int>> representedLines, CodeCoverageBackfillValidation validation)
    {
        foreach (var item in validation.RepresentedBackendLinesByBackendPath)
        {
            if (!representedLines.TryGetValue(item.Key, out var lines))
            {
                lines = [];
                representedLines[item.Key] = lines;
            }

            foreach (var line in item.Value)
            {
                lines.Add(line);
            }
        }
    }

    private static void AddLocalCandidates(Dictionary<string, string> localCandidates, CodeCoverageBackfillValidation target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var item in source)
        {
            if (localCandidates.TryGetValue(item.Key, out var existingCandidate) &&
                !LocalCandidateComparer.Equals(existingCandidate, item.Value))
            {
                target.UnsafePathMatch = true;
                continue;
            }

            if (TryGetBackendPathByLocalCandidate(localCandidates, item.Value, out var existingBackendPath) &&
                !StringComparer.Ordinal.Equals(existingBackendPath, item.Key))
            {
                target.UnsafePathMatch = true;
                continue;
            }

            localCandidates[item.Key] = item.Value;
        }
    }

    private static bool TryGetBackendPathByLocalCandidate(Dictionary<string, string> localCandidates, string localCandidate, out string backendPath)
    {
        foreach (var item in localCandidates)
        {
            if (LocalCandidateComparer.Equals(item.Value, localCandidate))
            {
                backendPath = item.Key;
                return true;
            }
        }

        backendPath = string.Empty;
        return false;
    }

    private static int CountDistinct(int[] lines)
    {
        var uniqueLines = new HashSet<int>();
        foreach (var line in lines)
        {
            uniqueLines.Add(line);
        }

        return uniqueLines.Count;
    }

    private static int[] ToSortedArray(HashSet<int> lines)
    {
        var sortedLines = new int[lines.Count];
        lines.CopyTo(sortedLines);
        Array.Sort(sortedLines);
        return sortedLines;
    }

    private bool RepresentsExpectedCoveredLines(string backendPath, int expectedCoveredLines)
    {
        return expectedCoveredLines > 0 &&
               RepresentedBackendLinesByBackendPath.TryGetValue(backendPath, out var representedLines) &&
               CountDistinct(representedLines) >= expectedCoveredLines;
    }

    private bool RepresentsRequiredBackendLines(string backendPath, int[] requiredLines)
    {
        if (requiredLines.Length == 0 ||
            !RepresentedBackendLinesByBackendPath.TryGetValue(backendPath, out var representedLines))
        {
            return false;
        }

        var representedLineSet = new HashSet<int>(representedLines);
        foreach (var requiredLine in requiredLines)
        {
            if (!representedLineSet.Contains(requiredLine))
            {
                return false;
            }
        }

        return true;
    }
}
