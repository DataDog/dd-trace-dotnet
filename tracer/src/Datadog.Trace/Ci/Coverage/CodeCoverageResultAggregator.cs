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
    // Guards all reads and writes to _entries so result aggregation is safe across IPC and in-process coverage callbacks.
    private readonly object _entriesLock = new();
    private readonly List<Entry> _entries = [];
    // Some backfill entries are not publishable alone after suppression, but can be restored if
    // same-source validation arrives later or multiple partial entries validate together.
    private readonly List<Entry> _pendingSuppressedBackfillEntries = [];
    private readonly HashSet<CodeCoverageReportSource> _suppressedSources = new();
    private readonly HashSet<CodeCoverageReportSource> _suppressedUnvalidatedSources = new();
    private readonly HashSet<CodeCoverageReportSource> _suppressedUnvalidatedBackfilledSources = new();

    /// <summary>
    /// Gets a value indicating whether at least one coverage result has been recorded.
    /// </summary>
    public bool HasResults
    {
        get
        {
            lock (_entriesLock)
            {
                return _entries.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether at least one recorded coverage result can currently publish session coverage tags.
    /// </summary>
    public bool HasPublishableResult => TryGetBestResult(out _);

    /// <summary>
    /// Gets whether the best currently publishable result was produced by the supplied source.
    /// </summary>
    /// <param name="source">Coverage source to compare with the selected result.</param>
    /// <returns>True when the supplied source would currently publish session coverage tags.</returns>
    public bool HasBestPublishableResult(CodeCoverageReportSource source)
    {
        return TryGetBestResult(out var result) &&
               result.Source == source;
    }

    /// <summary>
    /// Gets whether a coverage result has already been recorded for the supplied source.
    /// </summary>
    /// <param name="source">Coverage source to check.</param>
    /// <returns>True when at least one result has been recorded for the source.</returns>
    public bool HasResult(CodeCoverageReportSource source)
    {
        lock (_entriesLock)
        {
            return TryGetMergedEntry(source, includePending: false, out var mergedEntry) &&
                   IsPublishableEntry(mergedEntry);
        }
    }

    /// <summary>
    /// Suppresses a coverage source after it is known to be unsafe for publication.
    /// </summary>
    /// <param name="source">Coverage source to remove and suppress.</param>
    public void Suppress(CodeCoverageReportSource source)
    {
        lock (_entriesLock)
        {
            _entries.RemoveAll(entry => entry.Source == source);
            _pendingSuppressedBackfillEntries.RemoveAll(entry => entry.Source == source);
            _suppressedSources.Add(source);
        }
    }

    /// <summary>
    /// Suppresses coverage entries for a source unless the producer already validated backend ITR coverage.
    /// </summary>
    /// <param name="source">Coverage source whose unvalidated results should be removed and suppressed.</param>
    public void SuppressUnvalidated(CodeCoverageReportSource source)
    {
        lock (_entriesLock)
        {
            var hasValidatedResult = HasValidatedResult(source);
            if (!hasValidatedResult)
            {
                foreach (var entry in _entries)
                {
                    if (entry.Source == source && !entry.BackfillValidated && CanContributeToLaterValidation(entry))
                    {
                        _pendingSuppressedBackfillEntries.Add(entry);
                    }
                }
            }

            _entries.RemoveAll(entry => entry.Source == source && !entry.BackfillValidated && (!hasValidatedResult || !CanContributeToLaterValidation(entry)));
            _suppressedUnvalidatedSources.Add(source);
        }
    }

    /// <summary>
    /// Suppresses backfilled coverage entries for a source unless the producer already validated backend ITR coverage.
    /// </summary>
    /// <param name="source">Coverage source whose unvalidated backfilled results should be removed and suppressed.</param>
    public void SuppressUnvalidatedBackfilled(CodeCoverageReportSource source)
    {
        lock (_entriesLock)
        {
            var hasValidatedResult = HasValidatedResult(source);
            if (!hasValidatedResult)
            {
                foreach (var entry in _entries)
                {
                    if (entry.Source == source &&
                        entry.Backfilled &&
                        !entry.BackfillValidated &&
                        CanContributeToLaterValidation(entry))
                    {
                        _pendingSuppressedBackfillEntries.Add(entry);
                    }
                }
            }

            _entries.RemoveAll(entry =>
                entry.Source == source &&
                entry.Backfilled &&
                !entry.BackfillValidated &&
                (!hasValidatedResult || !CanContributeToLaterValidation(entry)));
            _suppressedUnvalidatedBackfilledSources.Add(source);
        }
    }

    /// <summary>
    /// Adds a coverage result produced by one source.
    /// </summary>
    /// <param name="source">Coverage source that produced the result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether the source used backend ITR coverage backfill.</param>
    /// <param name="executableLines">Optional executable-line count for count-based aggregation.</param>
    /// <param name="coveredLines">Optional covered-line count for count-based aggregation.</param>
    /// <param name="diagnostic">Optional compact diagnostic text for logs.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    public void Add(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string? diagnostic, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null)
    {
        if (percentage < 0 || double.IsNaN(percentage) || double.IsInfinity(percentage))
        {
            return;
        }

        var result = new Entry(
            source,
            percentage.ToValidPercentage(),
            backfilled,
            executableLines,
            coveredLines,
            diagnostic,
            backfillValidated,
            backfillNotApplicable,
            backfillValidation);

        lock (_entriesLock)
        {
            if (_suppressedSources.Contains(source))
            {
                return;
            }

            if (!result.BackfillValidated &&
                _suppressedUnvalidatedSources.Contains(source))
            {
                if (CanContributeToLaterValidation(result))
                {
                    if (HasValidatedResult(source))
                    {
                        _entries.Add(result);
                    }
                    else
                    {
                        _pendingSuppressedBackfillEntries.Add(result);
                        RestorePendingSuppressedBackfillEntriesIfValidated(source);
                    }
                }

                return;
            }

            if (result.Backfilled &&
                !result.BackfillValidated &&
                _suppressedUnvalidatedBackfilledSources.Contains(source))
            {
                if (CanContributeToLaterValidation(result))
                {
                    if (HasValidatedResult(source))
                    {
                        _entries.Add(result);
                    }
                    else
                    {
                        _pendingSuppressedBackfillEntries.Add(result);
                        RestorePendingSuppressedBackfillEntriesIfValidated(source);
                    }
                }

                return;
            }

            _entries.Add(result);
            RestorePendingSuppressedBackfillEntriesIfValidated(source);
        }
    }

    /// <summary>
    /// Replaces all previously recorded results for one source with a merged result that already represents them.
    /// </summary>
    /// <param name="source">Coverage source that produced the merged result.</param>
    /// <param name="percentage">Line coverage percentage reported by the source.</param>
    /// <param name="backfilled">Whether the source used backend ITR coverage backfill.</param>
    /// <param name="executableLines">Optional executable-line count for count-based aggregation.</param>
    /// <param name="coveredLines">Optional covered-line count for count-based aggregation.</param>
    /// <param name="diagnostic">Optional compact diagnostic text for logs.</param>
    /// <param name="backfillValidated">Whether backend ITR coverage was reconciled without unsafe path ambiguity for this result.</param>
    /// <param name="backfillNotApplicable">Whether backend ITR coverage was evaluated and did not apply to this producer result.</param>
    /// <param name="backfillValidation">Backend ITR coverage validation data that can be merged with other same-source results.</param>
    public void Replace(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string? diagnostic, bool backfillValidated = false, bool backfillNotApplicable = false, CodeCoverageBackfillValidation? backfillValidation = null)
    {
        if (percentage < 0 || double.IsNaN(percentage) || double.IsInfinity(percentage))
        {
            return;
        }

        var result = new Entry(
            source,
            percentage.ToValidPercentage(),
            backfilled,
            executableLines,
            coveredLines,
            diagnostic,
            backfillValidated,
            backfillNotApplicable,
            backfillValidation);

        lock (_entriesLock)
        {
            if (_suppressedSources.Contains(source))
            {
                return;
            }

            if (!result.BackfillValidated &&
                _suppressedUnvalidatedSources.Contains(source))
            {
                if (CanContributeToLaterValidation(result))
                {
                    if (HasValidatedResult(source))
                    {
                        _entries.RemoveAll(entry => entry.Source == source);
                        _pendingSuppressedBackfillEntries.RemoveAll(entry => entry.Source == source);
                        _entries.Add(result);
                    }
                    else
                    {
                        _entries.RemoveAll(entry => entry.Source == source);
                        _pendingSuppressedBackfillEntries.RemoveAll(entry => entry.Source == source);
                        _pendingSuppressedBackfillEntries.Add(result);
                        RestorePendingSuppressedBackfillEntriesIfValidated(source);
                    }
                }

                return;
            }

            if (result.Backfilled &&
                !result.BackfillValidated &&
                _suppressedUnvalidatedBackfilledSources.Contains(source))
            {
                if (CanContributeToLaterValidation(result))
                {
                    if (HasValidatedResult(source))
                    {
                        _entries.RemoveAll(entry => entry.Source == source);
                        _pendingSuppressedBackfillEntries.RemoveAll(entry => entry.Source == source);
                        _entries.Add(result);
                    }
                    else
                    {
                        _entries.RemoveAll(entry => entry.Source == source);
                        _pendingSuppressedBackfillEntries.RemoveAll(entry => entry.Source == source);
                        _pendingSuppressedBackfillEntries.Add(result);
                        RestorePendingSuppressedBackfillEntriesIfValidated(source);
                    }
                }

                return;
            }

            _entries.RemoveAll(entry => entry.Source == source);
            _pendingSuppressedBackfillEntries.RemoveAll(entry => entry.Source == source);
            _entries.Add(result);
            RestorePendingSuppressedBackfillEntriesIfValidated(source);
        }
    }

    /// <summary>
    /// Tries to select the highest-priority coverage source result.
    /// </summary>
    /// <param name="result">Selected result when one exists.</param>
    /// <returns>True if a coverage result was selected.</returns>
    public bool TryGetBestResult(out CodeCoverageAggregationResult result)
    {
        lock (_entriesLock)
        {
            var mergedEntries = new Dictionary<CodeCoverageReportSource, Entry>();
            foreach (var entry in _entries)
            {
                if (!mergedEntries.TryGetValue(entry.Source, out var existing))
                {
                    mergedEntries[entry.Source] = entry;
                    continue;
                }

                mergedEntries[entry.Source] = existing.Merge(entry);
            }

            var hasBest = false;
            var best = default(Entry);
            foreach (var entry in mergedEntries.Values)
            {
                if (!IsPublishableEntry(entry))
                {
                    continue;
                }

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
                ShouldPublishBackfilled(best, mergedEntries) || best.Backfilled,
                best.ExecutableLines,
                best.CoveredLines,
                best.Diagnostic,
                backfillValidated: best.BackfillValidated,
                backfillNotApplicable: best.BackfillNotApplicable,
                backfillValidation: best.BackfillValidation);
            return true;
        }
    }

    private static int GetPriority(CodeCoverageReportSource source)
    {
        return source switch
        {
            CodeCoverageReportSource.ExternalXml => 500,
            CodeCoverageReportSource.CoverletXmlFallback => 475,
            CodeCoverageReportSource.Coverlet => 450,
            CodeCoverageReportSource.MicrosoftCodeCoverage => 400,
            CodeCoverageReportSource.Unknown => 300,
            CodeCoverageReportSource.DatadogInternal => 100,
            _ => 0,
        };
    }

    private static bool CanContributeToLaterValidation(Entry entry)
        => entry.BackfillNotApplicable || entry.BackfillValidation is not null;

    private static bool ShouldPublishBackfilled(Entry selectedEntry, Dictionary<CodeCoverageReportSource, Entry> mergedEntries)
    {
        return selectedEntry.Source == CodeCoverageReportSource.CoverletXmlFallback &&
               mergedEntries.TryGetValue(CodeCoverageReportSource.Coverlet, out var coverletEntry) &&
               coverletEntry.Backfilled;
    }

    private bool IsPublishableEntry(Entry entry)
        => !entry.CountAggregationAmbiguous &&
           (!_suppressedUnvalidatedSources.Contains(entry.Source) || entry.BackfillValidated) &&
           (!_suppressedUnvalidatedBackfilledSources.Contains(entry.Source) || !entry.Backfilled || entry.BackfillValidated);

    private bool HasValidatedResult(CodeCoverageReportSource source)
    {
        return TryGetMergedEntry(source, includePending: false, out var mergedEntry) &&
               mergedEntry.BackfillValidated;
    }

    private bool HasValidatedResultIncludingPending(CodeCoverageReportSource source)
    {
        return TryGetMergedEntry(source, includePending: true, out var mergedEntry) &&
               mergedEntry.BackfillValidated;
    }

    private bool TryGetMergedEntry(CodeCoverageReportSource source, bool includePending, out Entry mergedEntry)
    {
        var hasMergedEntry = false;
        mergedEntry = default;
        foreach (var entry in _entries)
        {
            if (entry.Source != source)
            {
                continue;
            }

            mergedEntry = hasMergedEntry ? mergedEntry.Merge(entry) : entry;
            hasMergedEntry = true;
        }

        if (!includePending)
        {
            return hasMergedEntry;
        }

        foreach (var entry in _pendingSuppressedBackfillEntries)
        {
            if (entry.Source != source)
            {
                continue;
            }

            mergedEntry = hasMergedEntry ? mergedEntry.Merge(entry) : entry;
            hasMergedEntry = true;
        }

        return hasMergedEntry;
    }

    private void RestorePendingSuppressedBackfillEntriesIfValidated(CodeCoverageReportSource source)
    {
        if (!HasValidatedResultIncludingPending(source))
        {
            return;
        }

        for (var i = 0; i < _pendingSuppressedBackfillEntries.Count;)
        {
            var entry = _pendingSuppressedBackfillEntries[i];
            if (entry.Source != source)
            {
                i++;
                continue;
            }

            _entries.Add(entry);
            _pendingSuppressedBackfillEntries.RemoveAt(i);
        }
    }

    private readonly struct Entry
    {
        private readonly bool _backfillValidated;

        public Entry(CodeCoverageReportSource source, double percentage, bool backfilled, double? executableLines, double? coveredLines, string? diagnostic, bool backfillValidated, bool backfillNotApplicable, CodeCoverageBackfillValidation? backfillValidation, bool countAggregationAmbiguous = false)
        {
            Source = source;
            Percentage = percentage;
            Backfilled = backfilled;
            ExecutableLines = executableLines;
            CoveredLines = coveredLines;
            Diagnostic = diagnostic;
            BackfillNotApplicable = backfillNotApplicable;
            BackfillValidation = backfillValidation;
            CountAggregationAmbiguous = countAggregationAmbiguous;
            _backfillValidated = backfillValidated;
        }

        public CodeCoverageReportSource Source { get; }

        public double Percentage { get; }

        public bool Backfilled { get; }

        public bool BackfillValidated => _backfillValidated || BackfillValidation?.CanPublish() == true;

        public bool BackfillNotApplicable { get; }

        public bool CountAggregationAmbiguous { get; }

        public CodeCoverageBackfillValidation? BackfillValidation { get; }

        public double? ExecutableLines { get; }

        public double? CoveredLines { get; }

        public string? Diagnostic { get; }

        public Entry Merge(Entry other)
        {
            var backfilled = Backfilled || other.Backfilled;
            var backfillNotApplicable = BackfillNotApplicable || other.BackfillNotApplicable;
            var backfillValidation = CodeCoverageBackfillValidation.Merge(BackfillValidation, other.BackfillValidation);
            var backfillValidated = backfillValidation is not null ? backfillValidation.CanPublish() : BackfillValidated || other.BackfillValidated;
            var diagnostic = other.Diagnostic ?? Diagnostic;
            var countAggregationAmbiguous = CountAggregationAmbiguous || other.CountAggregationAmbiguous;
            var hasCounts = HasCounts();
            var otherHasCounts = other.HasCounts();
            if (hasCounts && otherHasCounts)
            {
                return new Entry(Source, other.Percentage, backfilled, null, null, diagnostic, backfillValidated, backfillNotApplicable, backfillValidation, countAggregationAmbiguous: true);
            }

            if (otherHasCounts)
            {
                return new Entry(Source, other.Percentage, backfilled, other.ExecutableLines, other.CoveredLines, diagnostic, backfillValidated, backfillNotApplicable, backfillValidation, countAggregationAmbiguous);
            }

            if (hasCounts)
            {
                return new Entry(Source, Percentage, backfilled, ExecutableLines, CoveredLines, diagnostic, backfillValidated, backfillNotApplicable, backfillValidation, countAggregationAmbiguous);
            }

            return new Entry(Source, other.Percentage, backfilled, other.ExecutableLines, other.CoveredLines, diagnostic, backfillValidated, backfillNotApplicable, backfillValidation, countAggregationAmbiguous);
        }

        private bool HasCounts()
        {
            return ExecutableLines.HasValue && CoveredLines.HasValue;
        }
    }
}
