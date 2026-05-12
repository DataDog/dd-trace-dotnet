// <copyright file="CodeCoverageResultAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Coverage;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CodeCoverageResultAggregatorTests
{
    [Fact]
    public void ExternalCoverageWinsOverInternalCoverage()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.DatadogInternal, percentage: 10, backfilled: true, executableLines: 10, coveredLines: 1, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.ExternalXml, percentage: 75, backfilled: false, executableLines: 4, coveredLines: 3, diagnostic: null);

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.ExternalXml);
        result.Percentage.Should().Be(75);
        result.Backfilled.Should().BeFalse();
    }

    [Fact]
    public void MultipleCountBasedResultsForSameSourceAreCombined()
    {
        var aggregator = new CodeCoverageResultAggregator();

        aggregator.Add(CodeCoverageReportSource.MicrosoftCodeCoverage, percentage: 50, backfilled: false, executableLines: 2, coveredLines: 1, diagnostic: null);
        aggregator.Add(CodeCoverageReportSource.MicrosoftCodeCoverage, percentage: 100, backfilled: true, executableLines: 2, coveredLines: 2, diagnostic: null);

        aggregator.TryGetBestResult(out var result).Should().BeTrue();
        result.Source.Should().Be(CodeCoverageReportSource.MicrosoftCodeCoverage);
        result.Percentage.Should().Be(75);
        result.Backfilled.Should().BeTrue();
        result.ExecutableLines.Should().Be(4);
        result.CoveredLines.Should().Be(3);
    }
}
