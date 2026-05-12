// <copyright file="CodeCoverageResultAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Aggregates candidate session coverage results and selects the one source allowed to publish session coverage tags.
/// </summary>
internal sealed class CodeCoverageResultAggregator
{
    private readonly Dictionary<CodeCoverageReportSource, Entry> _entries = new();

    /// <summary>
    /// Gets a value indicating whether at least one coverage result has been recorded.
    /// </summary>
    public bool HasResults => _entries.Count > 0;

    /// <summary>
    /// Adds a coverage result produced by one source.
    /// </summary>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether the source used backend ITR coverage backfill.</param>
    /// <param name="executableLines">Optional executable-line count for count-based aggregation.</param>
    /// <param name="coveredLines">Optional covered-line count for count-based aggregation.</param>
    /// <param name="diagnostic">Optional compact diagnostic text for logs.</param>
    public void Add(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string? diagnostic)
    {
        if (percentage < 0)
        {
            return;
        }

        var result = new Entry(
            source,
            percentage.ToValidPercentage(),
            backfilled,
            executableLines,
            coveredLines,
            diagnostic);

        if (!_entries.TryGetValue(source, out var existing))
        {
            _entries[source] = result;
            return;
        }

        _entries[source] = existing.Merge(result);
    }

    /// <summary>
    /// Tries to select the highest-priority coverage source result.
    /// </summary>
    /// <param name="result">Selected result when one exists.</param>
    /// <returns>True if a coverage result was selected.</returns>
    public bool TryGetBestResult(out CodeCoverageAggregationResult result)
    {
        var hasBest = false;
        var best = default(Entry);
        foreach (var entry in _entries.Values)
        {
            if (!hasBest || GetPriority(entry.Source) > GetPriority(best.Source))
            {
                best = entry;
                hasBest = true;
            }
        }

        if (!hasBest)
        {
            result = default;
            return false;
        }

        result = new CodeCoverageAggregationResult(
            best.Source,
            best.Percentage,
            best.Backfilled,
            best.ExecutableLines,
            best.CoveredLines,
            best.Diagnostic);
        return true;
    }

    private static int GetPriority(CodeCoverageReportSource source)
    {
        return source switch
        {
            CodeCoverageReportSource.ExternalXml => 500,
            CodeCoverageReportSource.Coverlet => 450,
            CodeCoverageReportSource.MicrosoftCodeCoverage => 400,
            CodeCoverageReportSource.Unknown => 300,
            CodeCoverageReportSource.DatadogInternal => 100,
            _ => 0,
        };
    }

    private readonly struct Entry
    {
        public Entry(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string? diagnostic)
        {
            Source = source;
            Percentage = percentage;
            Backfilled = backfilled;
            ExecutableLines = executableLines;
            CoveredLines = coveredLines;
            Diagnostic = diagnostic;
        }

        public CodeCoverageReportSource Source { get; }

        public double Percentage { get; }

        public bool Backfilled { get; }

        public double? ExecutableLines { get; }

        public double? CoveredLines { get; }

        public string? Diagnostic { get; }

        public Entry Merge(Entry other)
        {
            var backfilled = Backfilled || other.Backfilled;
            var diagnostic = other.Diagnostic ?? Diagnostic;
            if (ExecutableLines is { } executableLines &&
                CoveredLines is { } coveredLines &&
                other.ExecutableLines is { } otherExecutableLines &&
                other.CoveredLines is { } otherCoveredLines)
            {
                var totalExecutableLines = executableLines + otherExecutableLines;
                var totalCoveredLines = coveredLines + otherCoveredLines;
                var percentage = totalExecutableLines <= 0 ? 0 : Math.Round((totalCoveredLines / totalExecutableLines) * 100, 2).ToValidPercentage();
                return new Entry(Source, percentage, backfilled, totalExecutableLines, totalCoveredLines, diagnostic);
            }

            return new Entry(Source, other.Percentage, backfilled, other.ExecutableLines, other.CoveredLines, diagnostic);
        }
    }
}
