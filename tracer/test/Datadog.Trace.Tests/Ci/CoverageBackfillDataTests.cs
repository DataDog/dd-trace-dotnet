// <copyright file="CoverageBackfillDataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util.Json;
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
    public void EmptyBackendCoverageStillMarksBackfillPathApplied()
    {
        var globalCoverage = new GlobalCoverageInfo();
        var backfill = CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string>());

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill);

        result.Applied.Should().BeTrue();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
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
    public void CoverageBackfillDataRoundTripsThroughJsonCache()
    {
        var coverage = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1100_0000])
            });

        var json = JsonHelper.SerializeObject(coverage);
        var deserialized = JsonHelper.DeserializeObject<CoverageBackfillData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsPresent.Should().BeTrue();
        deserialized.IsValid.Should().BeTrue();
        deserialized.TotalBitmapBytes.Should().Be(1);
        deserialized.ExecutedLinesByRelativePath.Should().ContainKey("src/Calculator.cs");
        deserialized.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
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

    [Fact]
    public void GlobalCoverageBackfillUsesLocalExecutableLinesAsDenominator()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo("src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1111_0000],
                            ExecutedBitmap = [0b_1000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0101_0001])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill);

        result.Applied.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        var file = globalCoverage.Components[0].Files[0];
        file.ExecutedBitmap.Should().Equal([0b_1101_0000]);
        file.Data.Should().Equal(75, 4, 3);
    }

    [Fact]
    public void GlobalCoverageBackfillIgnoresBackendOnlyFiles()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo("src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_1000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Other.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill);

        result.Applied.Should().BeTrue();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.GetTotalPercentage().Should().Be(100);
    }
}
