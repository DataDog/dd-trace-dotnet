// <copyright file="CoverageBackfillDataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Configuration;
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

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeFalse();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
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

    [Theory]
    [InlineData("\\src\\Calculator.cs", "src/Calculator.cs")]
    [InlineData("/src/Calculator.cs", "src/Calculator.cs")]
    [InlineData("src/Calculator.cs", "src/Calculator.cs")]
    public void NormalizePathTrimsRootAndNormalizesSeparators(string path, string expected)
    {
        CoverageBackfillData.NormalizePath(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("\\")]
    public void NormalizePathRejectsEmptyOrRootOnlyPaths(string path)
    {
        var act = () => CoverageBackfillData.NormalizePath(path);

        act.Should().Throw<ArgumentException>();
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
    public void MergeOrsOnlyValidPresentCoverageMaps()
    {
        var first = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });
        var second = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000]),
                ["src/Other.cs"] = Convert.ToBase64String([0b_0000_0001])
            });
        var invalid = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Ignored.cs"] = "not-base64"
            });

        var merged = CoverageBackfillData.Merge([first, CoverageBackfillData.Missing, invalid, second]);

        merged.IsPresent.Should().BeTrue();
        merged.IsValid.Should().BeTrue();
        merged.TotalBitmapBytes.Should().Be(2);
        merged.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
        merged.ExecutedLinesByRelativePath["src/Other.cs"].Should().Equal([0b_0000_0001]);
        merged.ExecutedLinesByRelativePath.Should().NotContainKey("src/Ignored.cs");
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
    public void TotalBitmapBytesIsDerivedFromCurrentBitmapData()
    {
        var coverage = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1100_0000])
            });

        coverage.TotalBitmapBytes.Should().Be(1);

        coverage.ExecutedLinesByRelativePath["src/Other.cs"] = [0b_1000_0000, 0b_0100_0000];

        coverage.TotalBitmapBytes.Should().Be(3);
    }

    [Fact]
    public void CoverageBackfillDataIgnoresSerializedTotalBitmapBytes()
    {
        const string json = """
                            {
                              "IsPresent": true,
                              "IsValid": true,
                              "Error": null,
                              "ExecutedLinesByRelativePath": {
                                "src/Calculator.cs": "wA=="
                              },
                              "TotalBitmapBytes": 999
                            }
                            """;

        var deserialized = JsonHelper.DeserializeObject<CoverageBackfillData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsPresent.Should().BeTrue();
        deserialized.IsValid.Should().BeTrue();
        deserialized.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
        deserialized.TotalBitmapBytes.Should().Be(1);
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
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0101_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        var file = globalCoverage.Components[0].Files[0];
        file.ExecutedBitmap.Should().Equal([0b_1101_0000]);
        file.Data.Should().Equal(75, 4, 3);
    }

    [Fact]
    public void GlobalCoverageBackfillRefreshesCachedParentCoverageData()
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
        globalCoverage.GetTotalPercentage().Should().Be(25);
        globalCoverage.Components[0].GetTotalPercentage().Should().Be(25);

        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0101_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.UpdatedFiles.Should().Be(1);
        result.CanPublishCoverage.Should().BeTrue();
        globalCoverage.Components[0].Files[0].Data.Should().Equal(75, 4, 3);
        globalCoverage.Components[0].GetTotalPercentage().Should().Be(75);
        globalCoverage.GetTotalPercentage().Should().Be(75);
    }

    [Fact]
    public void GlobalCoverageBackfillOrsBackendLinesWithoutExecutableMask()
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
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1100_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1100_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillKeepsBackendBitmapLength()
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
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000, 0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1000_0000, 0b_1000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillOrsBackendCoverageIntoDuplicateLocalFiles()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("ComponentA")
                {
                    Files =
                    {
                        new FileCoverageInfo("src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                },
                new ComponentCoverageInfo("ComponentB")
                {
                    Files =
                    {
                        new FileCoverageInfo("src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_0100_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1100_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.MatchedFiles.Should().Be(2);
        result.UpdatedFiles.Should().Be(2);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1100_0000]);
        globalCoverage.Components[1].Files[0].ExecutedBitmap.Should().Equal([0b_1100_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillMatchesUniqueCaseInsensitiveSuffixOnlyOnWindows()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo("repo/SRC/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        if (FrameworkDescription.Instance.IsWindows())
        {
            result.MatchedFiles.Should().Be(1);
            result.CanPublishCoverage.Should().BeTrue();
            result.UpdatedFiles.Should().Be(1);
            globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1000_0000]);
        }
        else
        {
            result.MatchedFiles.Should().Be(0);
            result.CanPublishCoverage.Should().BeTrue();
            result.UpdatedFiles.Should().Be(0);
            globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
        }
    }

    [Fact]
    public void GlobalCoverageBackfillDoesNotSuffixMatchAbsolutePathOutsideSourceRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-global-source-root-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(tempRoot, "repo");
        var outsideSourcePath = Path.Combine(tempRoot, "outside", "src", "Calculator.cs");
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo(outsideSourcePath)
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });
        var ciEnvironmentValues = CreateGitHubCiValues(sourceRoot);

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, ciEnvironmentValues);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillDoesNotSuffixMatchAbsolutePathWhenSourceRootIsMissing()
    {
        var outsideSourcePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-global-outside-{Guid.NewGuid():N}", "src", "Calculator.cs");
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo(outsideSourcePath)
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });
        var ciEnvironmentValues = CreateGitHubCiValuesWithoutSourceRoot();

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, ciEnvironmentValues);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
    }

    [Theory]
    [InlineData("../shared/src/Calculator.cs")]
    [InlineData("shared/../src/Calculator.cs")]
    [InlineData("shared\\..\\src\\Calculator.cs")]
    [InlineData("shared/src/..")]
    public void GlobalCoverageBackfillDoesNotSuffixMatchRelativePathWithParentDirectorySegment(string localPath)
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo(localPath)
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillIgnoresAmbiguousNonExactBackendKeyMatches()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo("repo-a/src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        },
                        new FileCoverageInfo("repo-b/src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
        globalCoverage.Components[0].Files[1].ExecutedBitmap.Should().Equal([0b_0000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillIgnoresBackendKeyWhenExactAndDistinctSuffixLocalFilesConflict()
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
                            ExecutedBitmap = [0b_0000_0000]
                        },
                        new FileCoverageInfo("repo-copy/src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
        globalCoverage.Components[0].Files[1].ExecutedBitmap.Should().Equal([0b_0000_0000]);
    }

    [Fact]
    public void PathMatcherRejectsAmbiguousCaseInsensitiveSuffixes()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["/repo/SRC/CALCULATOR.cs"]);

        bitmap.Should().BeNull();
    }

    [Fact]
    public void PathMatcherKeepsActiveSuffixWhenInactiveSuffixAlsoMatches()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["workspace/service-a/src/Calculator.cs"]);

        bitmap.Should().Equal([0b_1000_0000]);
    }

    [Fact]
    public void PathMatcherReplacesInactiveSuffixWhenActiveSuffixAlsoMatches()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0000_0000]),
                ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["workspace/service-a/src/Calculator.cs"]);

        bitmap.Should().Equal([0b_1000_0000]);
    }

    [Fact]
    public void PathMatcherRejectsActiveSuffixWhenInactiveSuffixIsMoreSpecific()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_0000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["workspace/service-a/src/Calculator.cs"]);

        bitmap.Should().BeNull();
    }

    [Fact]
    public void PathMatcherRejectsLaterActiveSuffixWhenEarlierInactiveSuffixIsMoreSpecific()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_0000_0000]),
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["workspace/service-a/src/Calculator.cs"]);

        bitmap.Should().BeNull();
    }

    [Fact]
    public void PathMatcherStillRejectsMultipleActiveSuffixMatches()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["workspace/service-a/src/Calculator.cs"]);

        bitmap.Should().BeNull();
    }

    [Fact]
    public void GlobalCoverageBackfillMatchesBackendPathByUnambiguousSuffix()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo("workspace/repo/src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillRejectsAbsoluteUriPathBeforeSuffixMatch()
    {
        var globalCoverage = new GlobalCoverageInfo
        {
            Components =
            {
                new ComponentCoverageInfo("Component")
                {
                    Files =
                    {
                        new FileCoverageInfo("file:///tmp/repo/src/Calculator.cs")
                        {
                            ExecutableBitmap = [0b_1000_0000],
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeFalse();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_0000_0000]);
    }

    [Fact]
    public void PathMatcherMatchesMoreSpecificLocalCandidateToBackendSuffix()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["repo/src/Calculator.cs"]);

        bitmap.Should().Equal([0b_1000_0000]);
    }

    [Theory]
    [InlineData("../shared/src/Calculator.cs")]
    [InlineData("shared/../src/Calculator.cs")]
    [InlineData("shared\\..\\src\\Calculator.cs")]
    [InlineData("shared/src/..")]
    public void PathMatcherRejectsParentDirectorySegments(string localPath)
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, [localPath]);

        bitmap.Should().BeNull();
    }

    [Fact]
    public void PathMatcherDoesNotMatchLessSpecificLocalCandidateToMoreSpecificBackendKey()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["ProjectB/src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["src/Calculator.cs"]);

        bitmap.Should().BeNull();
    }

    [Fact]
    public void PathMatcherUsesCaseInsensitiveExactPathOnlyOnWindows()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["Program.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["program.cs"]);

        if (FrameworkDescription.Instance.IsWindows())
        {
            bitmap.Should().Equal([0b_1000_0000]);
        }
        else
        {
            bitmap.Should().BeNull();
        }
    }

    [Fact]
    public void PathMatcherRejectsAmbiguousCaseInsensitiveExactPaths()
    {
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["Program.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["program.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        var bitmap = CoverageBackfillPathMatcher.GetBackendBitmap(backfill, ["PROGRAM.cs"]);

        bitmap.Should().BeNull();
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

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.MatchedFiles.Should().Be(0);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.GetTotalPercentage().Should().Be(100);
    }

    [Fact]
    public void GlobalCoverageBackfillAppliesMatchedBackendCoverageAndIgnoresBackendOnlyFiles()
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
                            ExecutedBitmap = [0b_0000_0000]
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Other.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillCreatesExecutedBitmapForMatchedCoverageAndIgnoresBackendOnlyFiles()
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
                            ExecutedBitmap = null
                        }
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Other.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.HasBackendCoverage.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(1);
        globalCoverage.Components[0].Files[0].ExecutedBitmap.Should().Equal([0b_1000_0000]);
    }

    [Fact]
    public void GlobalCoverageBackfillMarksBackfilledWhenMatchedFileDoesNotChange()
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
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        var result = CoverageBackfillApplicator.ApplyToGlobalCoverage(globalCoverage, backfill, CIEnvironmentValues.Instance);

        result.Applied.Should().BeTrue();
        result.CanPublishCoverage.Should().BeTrue();
        result.Backfilled.Should().BeTrue();
        result.MatchedFiles.Should().Be(1);
        result.UpdatedFiles.Should().Be(0);
        globalCoverage.GetTotalPercentage().Should().Be(100);
    }

    private static CIEnvironmentValues CreateGitHubCiValues(string sourceRoot)
        => CIEnvironmentValues.Create(
            new Dictionary<string, string>
            {
                [PlatformKeys.Ci.GitHub.Sha] = "abc123",
                [PlatformKeys.Ci.GitHub.Workspace] = sourceRoot,
                [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet"
            });

    private static CIEnvironmentValues CreateGitHubCiValuesWithoutSourceRoot()
        => CIEnvironmentValues.Create(
            new Dictionary<string, string>
            {
                [PlatformKeys.Ci.GitHub.Sha] = "abc123",
                [PlatformKeys.Ci.GitHub.Repository] = "DataDog/dd-trace-dotnet"
            });
}
