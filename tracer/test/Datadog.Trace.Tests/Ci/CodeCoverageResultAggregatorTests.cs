// <copyright file="CodeCoverageResultAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CodeCoverageResultAggregatorTests
{
    [Fact]
    public void EmptyAggregatorHasNoBestResult()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.HasResults.Should().BeFalse();
        aggregator.HasPublishableResult.Should().BeFalse();
        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out _).Should().BeFalse();
    }

    [Fact]
    public void ExternalCoverageWinsOverInternalCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 10, backfilled: true, executableLines: 10, coveredLines: 1, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.ExternalXml, percentage: 75, backfilled: false, executableLines: 4, coveredLines: 3, diagnostic: null);

        aggregator.HasPublishableResult.Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.ExternalXml);
        result.Percentage.Should().Be(75);
        result.Backfilled.Should().BeFalse();
    }

    [Fact]
    public void HigherPrioritySourceWinsWhenPercentagesTie()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.MicrosoftCodeCoverage, percentage: 80, backfilled: false, executableLines: 10, coveredLines: 8, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 80, backfilled: false, executableLines: 10, coveredLines: 8, diagnostic: null);

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(80);
    }

    [Fact]
    public void CoverletXmlFallbackWinsOverLateCoverletIpcResult()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "late-ipc");
        aggregator.Add(CodeCoverageReportSource.CoverletXmlFallback, percentage: 100, backfilled: true, executableLines: 4, coveredLines: 4, diagnostic: "xml-fallback");

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
        result.Percentage.Should().Be(100);
        result.Backfilled.Should().BeTrue();
        result.Diagnostic.Should().Be("xml-fallback");
    }

    [Fact]
    public void CoverletXmlFallbackKeepsBackfilledSignalFromCoverletResult()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 4, coveredLines: 4, diagnostic: "coverlet", backfillValidated: true);
        aggregator.Add(CodeCoverageReportSource.CoverletXmlFallback, percentage: 100, backfilled: false, executableLines: 4, coveredLines: 4, diagnostic: "xml-fallback", backfillValidated: true);

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
        result.Percentage.Should().Be(100);
        result.Backfilled.Should().BeTrue();
        result.Diagnostic.Should().Be("xml-fallback");
    }

    [Fact]
    public void SuppressedUnvalidatedSourceKeepsValidatedCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: "stale-coverlet");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: "validated-coverlet", backfillValidated: true);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 0, backfilled: false, executableLines: 2, coveredLines: 0, diagnostic: "late-stale-coverlet");

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(100);
        result.Diagnostic.Should().Be("validated-coverlet");
        result.BackfillValidated.Should().BeTrue();
    }

    [Fact]
    public void SuppressedUnvalidatedSourceKeepsValidatedRawCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: "validated-raw-coverlet", backfillValidated: true);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(50);
        result.Backfilled.Should().BeFalse();
        result.BackfillValidated.Should().BeTrue();
    }

    [Fact]
    public void SuppressedUnvalidatedBackfilledSourceKeepsRawCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: "raw-coverlet");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: "unvalidated-backfilled-coverlet");
        aggregator.SuppressUnvalidatedBackfilled(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 75, backfilled: true, executableLines: 4, coveredLines: 3, diagnostic: "late-unvalidated-backfilled-coverlet");

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(50);
        result.Diagnostic.Should().Be("raw-coverlet");
        result.Backfilled.Should().BeFalse();
    }

    [Fact]
    public void SuppressedUnvalidatedBackfilledSourceKeepsValidatedBackfilledCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: "validated-backfilled-coverlet", backfillValidated: true);
        aggregator.SuppressUnvalidatedBackfilled(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 75, backfilled: true, executableLines: 4, coveredLines: 3, diagnostic: "late-unvalidated-backfilled-coverlet");

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(100);
        result.Diagnostic.Should().Be("validated-backfilled-coverlet");
        result.Backfilled.Should().BeTrue();
        result.BackfillValidated.Should().BeTrue();
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRejectsNotApplicableCoverageWhenSameSourceCountsAreAmbiguous()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "not-applicable-coverlet", backfillNotApplicable: true);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "validated-coverlet", backfillValidated: true);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 0, backfilled: false, executableLines: 1, coveredLines: 0, diagnostic: "stale-coverlet");
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRemovesNotApplicableCoverageWithoutValidatedCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "not-applicable-coverlet", backfillNotApplicable: true);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedOnlySourceHasNoPublishableCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: "stale-coverlet");
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);

        aggregator.HasResults.Should().BeFalse();
        aggregator.HasPublishableResult.Should().BeFalse();
        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out _).Should().BeFalse();
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRestoresLateNotApplicableCoverageWhenValidationArrivesLater()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 4, coveredLines: 2, diagnostic: "late-not-applicable-coverlet", backfillNotApplicable: true);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var resultBeforeValidation).Should().BeTrue();
        resultBeforeValidation.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "late-validated-coverlet", backfillValidated: true);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRejectsPartialBackfilledCoverageWhenSameSourceCountsAreAmbiguous()
    {
        var aggregator = new CodeCoverageResultAggregator();
        var requiredBackendLines = new Dictionary<string, int[]>
        {
            ["src/A.cs"] = [1],
            ["src/B.cs"] = [2]
        };
        var firstValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 2, "src/A.cs", expectedCoveredLines: 1, representedLines: [1], requiredBackendLines: requiredBackendLines);
        var secondValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 2, "src/B.cs", expectedCoveredLines: 1, representedLines: [2], requiredBackendLines: requiredBackendLines);

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: true, executableLines: 2, coveredLines: 1, diagnostic: "partial-a", backfillValidation: firstValidation);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeTrue();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "partial-b", backfillValidation: secondValidation);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRejectsPartialBackfilledCoverageWhenMergedCountsAreAmbiguous()
    {
        var aggregator = new CodeCoverageResultAggregator();
        var firstRequiredBackendLines = new Dictionary<string, int[]>
        {
            ["src/A.cs"] = [1],
            ["src/C.cs"] = [3]
        };
        var secondRequiredBackendLines = new Dictionary<string, int[]>
        {
            ["src/B.cs"] = [2],
            ["src/D.cs"] = [4]
        };
        var firstValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 2, "src/A.cs", expectedCoveredLines: 1, representedLines: [1], requiredBackendLines: firstRequiredBackendLines);
        var secondValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 2, "src/B.cs", expectedCoveredLines: 1, representedLines: [2], requiredBackendLines: secondRequiredBackendLines);

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: true, executableLines: 2, coveredLines: 1, diagnostic: "partial-a", backfillValidation: firstValidation);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "partial-b", backfillValidation: secondValidation);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRejectsConflictingMergedBackfillValidation()
    {
        var aggregator = new CodeCoverageResultAggregator();
        var firstValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 1, "src/Calculator.cs", expectedCoveredLines: 1, representedLines: [1], localCandidate: "repo-a/src/Calculator.cs");
        var secondValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 1, "src/Calculator.cs", expectedCoveredLines: 1, representedLines: [1], localCandidate: "repo-b/src/Calculator.cs");

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "partial-a", backfillValidation: firstValidation);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 1, coveredLines: 1, diagnostic: "partial-b", backfillValidation: secondValidation);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRejectsMergedBackfillValidationWithSameLocalCandidateForDifferentBackendPaths()
    {
        var aggregator = new CodeCoverageResultAggregator();
        var firstRequiredBackendLines = new Dictionary<string, int[]>
        {
            ["src/A.cs"] = [1],
            ["src/B.cs"] = [2]
        };
        var secondRequiredBackendLines = new Dictionary<string, int[]>
        {
            ["src/A.cs"] = [1],
            ["src/B.cs"] = [2]
        };
        var firstValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 2, "src/A.cs", expectedCoveredLines: 1, representedLines: [1], localCandidate: "repo/shared/Calculator.cs", requiredBackendLines: firstRequiredBackendLines);
        var secondValidation = CreateBackfillValidation(requiredBackendFilesWithCoverage: 2, "src/B.cs", expectedCoveredLines: 1, representedLines: [2], localCandidate: "repo/shared/Calculator.cs", requiredBackendLines: secondRequiredBackendLines);

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: true, executableLines: null, coveredLines: null, diagnostic: "partial-a", backfillValidation: firstValidation);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: null, coveredLines: null, diagnostic: "partial-b", backfillValidation: secondValidation);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void SuppressedUnvalidatedSourceRemovesAndRejectsOnlyUnvalidatedCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: "stale-coverlet");
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.Coverlet);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: "late-stale-coverlet");

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
        result.Diagnostic.Should().Be("internal");
    }

    [Fact]
    public void KnownSourceWinsOverUnmappedSourcePriority()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add((CodeCoverageReportSource)999, percentage: 100, backfilled: false, executableLines: 10, coveredLines: 10, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.Unknown, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: null);

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Unknown);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void MultipleCountBasedResultsForSameSourceFailClosedToFallback()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.MicrosoftCodeCoverage, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.MicrosoftCodeCoverage, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: null);

        aggregator.HasResult(CodeCoverageReportSource.MicrosoftCodeCoverage).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void MultipleCoverletResultsFailClosedToFallback()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: null);

        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    [Fact]
    public void MultipleCountBasedResultsForOnlyOneSourceHaveNoPublishableCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "partial-a");
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: "partial-b");

        aggregator.HasResults.Should().BeTrue();
        aggregator.HasPublishableResult.Should().BeFalse();
        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out _).Should().BeFalse();
    }

    [Fact]
    public void ReplacedCountBasedResultPublishesAsAlreadyMerged()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.CoverletXmlFallback, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: "partial-a");
        aggregator.Add(CodeCoverageReportSource.CoverletXmlFallback, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: "partial-b");
        aggregator.Replace(CodeCoverageReportSource.CoverletXmlFallback, percentage: 75, backfilled: true, executableLines: 4, coveredLines: 3, diagnostic: "merged");

        aggregator.HasResult(CodeCoverageReportSource.CoverletXmlFallback).Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
        result.Percentage.Should().Be(75);
        result.Backfilled.Should().BeTrue();
        result.ExecutableLines.Should().Be(4);
        result.CoveredLines.Should().Be(3);
        result.Diagnostic.Should().Be("merged");
    }

    [Fact]
    public void ReplacedValidatedResultDoesNotRestoreSupersededPendingEntries()
    {
        var aggregator = new CodeCoverageResultAggregator();
        var partialValidation = CreateBackfillValidation(
            requiredBackendFilesWithCoverage: 2,
            "src/A.cs",
            expectedCoveredLines: 1,
            representedLines: [1],
            requiredBackendLines: new Dictionary<string, int[]>
            {
                ["src/A.cs"] = [1],
                ["src/B.cs"] = [2]
            });

        aggregator.Add(CodeCoverageReportSource.CoverletXmlFallback, percentage: 50, backfilled: true, executableLines: 2, coveredLines: 1, diagnostic: "partial-a", backfillValidation: partialValidation);
        aggregator.SuppressUnvalidated(CodeCoverageReportSource.CoverletXmlFallback);
        aggregator.Replace(CodeCoverageReportSource.CoverletXmlFallback, percentage: 75, backfilled: true, executableLines: 4, coveredLines: 3, diagnostic: "merged", backfillValidated: true);

        aggregator.HasResult(CodeCoverageReportSource.CoverletXmlFallback).Should().BeTrue();
        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.CoverletXmlFallback);
        result.Percentage.Should().Be(75);
        result.ExecutableLines.Should().Be(4);
        result.CoveredLines.Should().Be(3);
        result.Diagnostic.Should().Be("merged");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameSourceMergePreservesCountsWhenOnlyOneEntryHasCounts(bool countedResultFirst)
    {
        var aggregator = new CodeCoverageResultAggregator();

        if (countedResultFirst)
        {
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 40, backfilled: false, executableLines: 10, coveredLines: 4, diagnostic: "counted");
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 90, backfilled: false, executableLines: null, coveredLines: null, diagnostic: "without-counts");
        }
        else
        {
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 90, backfilled: false, executableLines: null, coveredLines: null, diagnostic: "without-counts");
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 40, backfilled: false, executableLines: 10, coveredLines: 4, diagnostic: "counted");
        }

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(40);
        result.ExecutableLines.Should().Be(10);
        result.CoveredLines.Should().Be(4);
        result.Diagnostic.Should().Be(countedResultFirst ? "without-counts" : "counted");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameSourceMergePreservesCompleteCountsWhenOtherEntryHasPartialCounts(bool completeCountsFirst)
    {
        var aggregator = new CodeCoverageResultAggregator();

        if (completeCountsFirst)
        {
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 40, backfilled: false, executableLines: 10, coveredLines: 4, diagnostic: "complete");
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 90, backfilled: true, executableLines: 12, coveredLines: null, diagnostic: "partial");
        }
        else
        {
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 90, backfilled: true, executableLines: 12, coveredLines: null, diagnostic: "partial");
            aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 40, backfilled: false, executableLines: 10, coveredLines: 4, diagnostic: "complete");
        }

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(40);
        result.Backfilled.Should().BeTrue();
        result.ExecutableLines.Should().Be(10);
        result.CoveredLines.Should().Be(4);
        result.Diagnostic.Should().Be(completeCountsFirst ? "partial" : "complete");
    }

    [Fact]
    public void SameSourceMergeUsesLatestPercentageWhenNoEntryHasCounts()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.ExternalXml, percentage: 20, backfilled: false, executableLines: null, coveredLines: null, diagnostic: "first");
        aggregator.Add(CodeCoverageReportSource.ExternalXml, percentage: 60, backfilled: true, executableLines: null, coveredLines: null, diagnostic: "second");

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.ExternalXml);
        result.Percentage.Should().Be(60);
        result.Backfilled.Should().BeTrue();
        result.ExecutableLines.Should().BeNull();
        result.CoveredLines.Should().BeNull();
        result.Diagnostic.Should().Be("second");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void SameSourceMergeAggregatesBackfilledFlag(bool firstBackfilled, bool secondBackfilled)
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 25, backfilled: firstBackfilled, executableLines: null, coveredLines: null, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: secondBackfilled, executableLines: null, coveredLines: null, diagnostic: null);

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.Coverlet);
        result.Percentage.Should().Be(50);
        result.Backfilled.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidPercentagesAreRejected(double percentage)
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.Coverlet, percentage, backfilled: true, executableLines: 10, coveredLines: 5, diagnostic: null);

        aggregator.HasResults.Should().BeFalse();
        aggregator.HasPublishableResult.Should().BeFalse();
        aggregator.HasResult(CodeCoverageReportSource.Coverlet).Should().BeFalse();
        aggregator.TryGetBestResult(out _).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentAddSmokeTest()
    {
        const int workers = 8;
        const int iterations = 100;
        var aggregator = new CodeCoverageResultAggregator();
        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 25, backfilled: false, executableLines: 4, coveredLines: 1, diagnostic: "internal");

        await Task.WhenAll(
            Enumerable.Range(0, workers)
                      .Select(worker => Task.Run(
                          () =>
                          {
                              for (var i = 0; i < iterations; i++)
                              {
                                  aggregator.Add(CodeCoverageReportSource.Coverlet, percentage: 50, backfilled: worker == 0, executableLines: 2, coveredLines: 1, diagnostic: null);
                                  aggregator.TryGetBestResult(out _).Should().BeTrue();
                              }
                          })));

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.DatadogInternal);
        result.Percentage.Should().Be(25);
    }

    private static CodeCoverageBackfillValidation CreateBackfillValidation(int requiredBackendFilesWithCoverage, string backendPath, int expectedCoveredLines, int[] representedLines, string localCandidate = null, string[] requiredBackendPaths = null, Dictionary<string, int[]> requiredBackendLines = null)
    {
        var requiredBackendLineSets = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        if (requiredBackendLines is not null)
        {
            foreach (var item in requiredBackendLines)
            {
                requiredBackendLineSets[item.Key] = new HashSet<int>(item.Value);
            }
        }
        else
        {
            requiredBackendLineSets[backendPath] = new HashSet<int>(representedLines);
        }

        return CodeCoverageBackfillValidation.Create(
            requiredBackendFilesWithCoverage,
            new Dictionary<string, int>
            {
                [backendPath] = expectedCoveredLines
            },
            new Dictionary<string, HashSet<int>>
            {
                [backendPath] = new(representedLines)
            },
            new Dictionary<string, string>
            {
                [backendPath] = localCandidate ?? backendPath
            },
            requiredBackendPaths,
            requiredBackendLineSets);
    }
}
