// <copyright file="CoverageBackfillDataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.Coverage.Backfill;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CoverageBackfillDataTests
{
    [Fact]
    public void MissingBackendCoverageKeepsDistinctMissingState()
    {
        var coverage = CoverageBackfillData.FromBackendCoverage(null);

        coverage.IsPresent.Should().BeFalse();
        coverage.IsValid.Should().BeTrue();
        coverage.ExecutedLinesByRelativePath.Should().BeEmpty();
    }

    [Fact]
    public void EmptyBackendCoverageKeepsDistinctPresentState()
    {
        var coverage = CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string>());

        coverage.IsPresent.Should().BeTrue();
        coverage.IsValid.Should().BeTrue();
        coverage.ExecutedLinesByRelativePath.Should().BeEmpty();
    }

    [Fact]
    public void BackendCoverageNormalizesPathsAndDecodesBitmaps()
    {
        var bitmap = new byte[] { 0b_1000_0000, 0b_0000_0001 };
        var coverage = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["/src\\Calculator.cs"] = Convert.ToBase64String(bitmap)
            });

        coverage.IsPresent.Should().BeTrue();
        coverage.IsValid.Should().BeTrue();
        coverage.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
        coverage.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal(bitmap);
        coverage.TotalBitmapBytes.Should().Be(2);
    }

    [Fact]
    public void DuplicateNormalizedPathsAreOrMerged()
    {
        var coverage = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["/src\\Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        coverage.IsValid.Should().BeTrue();
        coverage.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
    }

    [Fact]
    public void InvalidBitmapMarksCoverageInvalid()
    {
        var coverage = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = "not-base64"
            });

        coverage.IsPresent.Should().BeTrue();
        coverage.IsValid.Should().BeFalse();
        coverage.Error.Should().Contain("Invalid coverage bitmap");
        coverage.ExecutedLinesByRelativePath.Should().BeEmpty();
    }
}
