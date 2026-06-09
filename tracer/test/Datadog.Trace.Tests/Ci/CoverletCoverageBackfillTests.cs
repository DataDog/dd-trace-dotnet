// <copyright file="CoverletCoverageBackfillTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CoverletCoverageBackfillTests
{
    [Fact]
    public void MutatesExistingCoverletLineHitsOnly()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 0,
            [3] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["workspace/service-a/src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0110_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(2);
        lineHits[1].Should().Be(1);
        lineHits[2].Should().Be(1);
        lineHits[3].Should().Be(1);
        lineHits.Should().NotContainKey(4);
    }

    [Fact]
    public void MutatesOneDuplicateLocalEntryForSameBackendLine()
    {
        var firstLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var secondLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = firstLineHits,
                        ["Subtract"] = secondLineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(1);
        (firstLineHits[1] + secondLineHits[1]).Should().Be(1);
    }

    [Fact]
    public void DoesNotMutateDuplicateLocalEntryWhenBackendLineIsAlreadyCovered()
    {
        var coveredLineHits = new Dictionary<int, int>
        {
            [1] = 1
        };
        var duplicateLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = coveredLineHits,
                        ["Subtract"] = duplicateLineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(0);
        coveredLineHits[1].Should().Be(1);
        duplicateLineHits[1].Should().Be(0);
    }

    [Fact]
    public void FailsWhenBackendPathDoesNotMatchCoverletDocuments()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Other.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        lineHits[2].Should().Be(0);
    }

    [Fact]
    public void ReturnsNotApplicableWhenNoActiveBackendPathMatchesCoverletDocuments()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = CreateCoverletDocument(lineHits)
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Other.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(modules, backfill, CIEnvironmentValues.Instance, out var updatedLines)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.NotApplicable);

        updatedLines.Should().Be(0);
        lineHits[2].Should().Be(0);
    }

    [Fact]
    public void MatchesBackendPathByUnambiguousSuffix()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0,
            [2] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["workspace/repo/src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(1);
        lineHits[1].Should().Be(0);
        lineHits[2].Should().Be(1);
    }

    [Fact]
    public void DoesNotSuffixMatchAbsoluteDocumentPathOutsideSourceRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-source-root-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(tempRoot, "repo");
        var outsideSourcePath = Path.Combine(tempRoot, "outside", "src", "Calculator.cs");
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                [outsideSourcePath] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
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

        CoverletCoverageBackfill.TryApply(modules, backfill, ciEnvironmentValues, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void DoesNotSuffixMatchAbsoluteDocumentPathWhenSourceRootIsMissing()
    {
        var outsideSourcePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverlet-outside-{Guid.NewGuid():N}", "src", "Calculator.cs");
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                [outsideSourcePath] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
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

        CoverletCoverageBackfill.TryApply(modules, backfill, ciEnvironmentValues, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void DoesNotSuffixMatchAbsoluteUriDocumentPath()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["file:///tmp/repo/src/Calculator.cs"] = CreateCoverletDocument(lineHits)
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });
        var ciEnvironmentValues = CreateGitHubCiValues("/tmp/repo");

        CoverletCoverageBackfill.TryApply(modules, backfill, ciEnvironmentValues, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Theory]
    [InlineData("../shared/src/Calculator.cs")]
    [InlineData("shared/../src/Calculator.cs")]
    [InlineData("shared\\..\\src\\Calculator.cs")]
    [InlineData("shared/src/..")]
    public void DoesNotSuffixMatchRelativeDocumentPathWithParentDirectorySegment(string documentPath)
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                [documentPath] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void ExactMatchWinsOverAdditionalSuffixCandidates()
    {
        var lineHits = new Dictionary<int, int>
        {
            [2] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000]),
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(modules, backfill, CIEnvironmentValues.Instance, out var updatedLines)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);

        updatedLines.Should().Be(1);
        lineHits[2].Should().Be(1);
    }

    [Fact]
    public void FailsWithoutMutatingWhenNonExactBackendKeyMatchesMultipleDocuments()
    {
        var firstLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var secondLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var documents = new OrderedDictionary
        {
            ["/repo-a/src/Calculator.cs"] = CreateCoverletDocument(firstLineHits),
            ["/repo-b/src/Calculator.cs"] = CreateCoverletDocument(secondLineHits)
        };
        var modules = new OrderedDictionary
        {
            ["Calculator.dll"] = documents
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        firstLineHits[1].Should().Be(0);
        secondLineHits[1].Should().Be(0);
    }

    [Fact]
    public void AppliesRepresentableCoverletLinesWhenSomeBackendLinesAreMissingFromDocument()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1100_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(1);
        lineHits[1].Should().Be(1);
    }

    [Fact]
    public void AppliesMatchedCoverletDocumentAndIgnoresBackendOnlyFiles()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
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

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(1);
        lineHits[1].Should().Be(1);
    }

    [Fact]
    public void AppliesPartialCoverletResultsAndValidatesWhenBackendCoverageIsComplete()
    {
        var firstLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var secondLineHits = new Dictionary<int, int>
        {
            [2] = 0
        };
        var firstModules = new Dictionary<string, object>
        {
            ["First.dll"] = new Dictionary<string, object>
            {
                ["src/A.cs"] = CreateCoverletDocument(firstLineHits)
            }
        };
        var secondModules = new Dictionary<string, object>
        {
            ["Second.dll"] = new Dictionary<string, object>
            {
                ["src/B.cs"] = CreateCoverletDocument(secondLineHits)
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/A.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/B.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(firstModules, backfill, CIEnvironmentValues.Instance, out var firstUpdatedLines, out var firstValidation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);
        CoverletCoverageBackfill.TryApplyForCurrentResult(secondModules, backfill, CIEnvironmentValues.Instance, out var secondUpdatedLines, out var secondValidation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);
        var mergedValidation = CodeCoverageBackfillValidation.Merge(firstValidation, secondValidation);

        firstUpdatedLines.Should().Be(1);
        secondUpdatedLines.Should().Be(1);
        firstLineHits[1].Should().Be(1);
        secondLineHits[2].Should().Be(1);
        firstValidation.Should().NotBeNull();
        firstValidation!.CanPublish().Should().BeTrue();
        secondValidation.Should().NotBeNull();
        secondValidation!.CanPublish().Should().BeTrue();
        mergedValidation.Should().NotBeNull();
        mergedValidation!.CanPublish().Should().BeTrue();
    }

    [Fact]
    public void PartialCoverletBackfillCanBeRolledBackBeforeCoverletSerializesXml()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/A.cs"] = CreateCoverletDocument(lineHits)
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/A.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/B.cs"] = Convert.ToBase64String([0b_0100_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(modules, backfill, CIEnvironmentValues.Instance, out var updatedLines, out var validation, out var rollback)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);

        updatedLines.Should().Be(1);
        lineHits[1].Should().Be(1);
        validation.Should().NotBeNull();
        validation!.CanPublish().Should().BeTrue();
        rollback.Should().NotBeNull();
        rollback!.TryRollback().Should().BeTrue();
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void MergedPartialCoverletValidationAllowsDifferentBackendSets()
    {
        var firstLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var secondLineHits = new Dictionary<int, int>
        {
            [2] = 0
        };
        var firstModules = new Dictionary<string, object>
        {
            ["First.dll"] = new Dictionary<string, object>
            {
                ["src/A.cs"] = CreateCoverletDocument(firstLineHits)
            }
        };
        var secondModules = new Dictionary<string, object>
        {
            ["Second.dll"] = new Dictionary<string, object>
            {
                ["src/B.cs"] = CreateCoverletDocument(secondLineHits)
            }
        };
        var firstBackfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/A.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/C.cs"] = Convert.ToBase64String([0b_0010_0000])
            });
        var secondBackfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/B.cs"] = Convert.ToBase64String([0b_0100_0000]),
                ["src/D.cs"] = Convert.ToBase64String([0b_0001_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(firstModules, firstBackfill, CIEnvironmentValues.Instance, out var firstUpdatedLines, out var firstValidation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);
        CoverletCoverageBackfill.TryApplyForCurrentResult(secondModules, secondBackfill, CIEnvironmentValues.Instance, out var secondUpdatedLines, out var secondValidation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);
        var mergedValidation = CodeCoverageBackfillValidation.Merge(firstValidation, secondValidation);

        firstUpdatedLines.Should().Be(1);
        secondUpdatedLines.Should().Be(1);
        firstValidation.Should().NotBeNull();
        firstValidation!.CanPublish().Should().BeTrue();
        secondValidation.Should().NotBeNull();
        secondValidation!.CanPublish().Should().BeTrue();
        mergedValidation.Should().NotBeNull();
        mergedValidation!.CanPublish().Should().BeTrue();
    }

    [Fact]
    public void MergedPartialCoverletValidationFailsWhenSameBackendPathUsesDifferentLocalSuffixMatches()
    {
        var firstLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var secondLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var firstModules = new Dictionary<string, object>
        {
            ["First.dll"] = new Dictionary<string, object>
            {
                ["repo-a/src/Calculator.cs"] = CreateCoverletDocument(firstLineHits)
            }
        };
        var secondModules = new Dictionary<string, object>
        {
            ["Second.dll"] = new Dictionary<string, object>
            {
                ["repo-b/src/Calculator.cs"] = CreateCoverletDocument(secondLineHits)
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(firstModules, backfill, CIEnvironmentValues.Instance, out var firstUpdatedLines, out var firstValidation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);
        CoverletCoverageBackfill.TryApplyForCurrentResult(secondModules, backfill, CIEnvironmentValues.Instance, out var secondUpdatedLines, out var secondValidation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);
        var mergedValidation = CodeCoverageBackfillValidation.Merge(firstValidation, secondValidation);

        firstUpdatedLines.Should().Be(1);
        secondUpdatedLines.Should().Be(1);
        firstValidation.Should().NotBeNull();
        firstValidation!.CanPublish().Should().BeTrue();
        secondValidation.Should().NotBeNull();
        secondValidation!.CanPublish().Should().BeTrue();
        mergedValidation.Should().NotBeNull();
        mergedValidation!.CanPublish().Should().BeFalse();
    }

    [Fact]
    public void AppliesRepresentableMatchedCoverletLinesAndIgnoresMissingBackendLines()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/A.cs"] = CreateCoverletDocument(lineHits)
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/A.cs"] = Convert.ToBase64String([0b_1100_0000]),
                ["src/B.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApplyForCurrentResult(modules, backfill, CIEnvironmentValues.Instance, out var updatedLines, out var validation)
                               .Should()
                               .Be(CoverletCoverageBackfillApplyResult.Applied);

        updatedLines.Should().Be(1);
        validation.Should().NotBeNull();
        validation!.CanPublish().Should().BeTrue();
        lineHits[1].Should().Be(1);
    }

    [Fact]
    public void AppliesRepresentableCoverletLinesWhenLaterDocumentMissesSomeBackendLines()
    {
        var calculatorLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var otherLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var documents = new OrderedDictionary
        {
            ["src/Calculator.cs"] = CreateCoverletDocument(calculatorLineHits),
            ["src/Other.cs"] = CreateCoverletDocument(otherLineHits)
        };
        var modules = new OrderedDictionary
        {
            ["Calculator.dll"] = documents
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                ["src/Other.cs"] = Convert.ToBase64String([0b_1100_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(2);
        calculatorLineHits[1].Should().Be(1);
        otherLineHits[1].Should().Be(1);
    }

    [Fact]
    public void MutatesLineHitsFromLinesProperty()
    {
        var method = new MethodWithLines(
            new Dictionary<int, int>
            {
                [1] = 0
            });
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = method
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(1);
        method.Lines[1].Should().Be(1);
    }

    [Fact]
    public void MutatesLineHitsFromLinesField()
    {
        var method = new MethodWithLinesField(
            new Dictionary<int, int>
            {
                [23] = 0
            });
        var modules = new Dictionary<string, object>
        {
            ["Samples.XUnitTests.dll"] = new Dictionary<string, object>
            {
                ["tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs"] = new Dictionary<string, object>
                {
                    ["Samples.XUnitTests.TestSuite"] = new Dictionary<string, object>
                    {
                        ["SimplePassTest"] = method
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs"] = Convert.ToBase64String([0, 0, 0b_0000_0010])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(1);
        method.Lines[23].Should().Be(1);
    }

    [Fact]
    public void RejectsMixedLineHitDictionary()
    {
        var lineHits = new Dictionary<int, object>
        {
            [1] = 0,
            [2] = new object()
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void FailsWithoutMutatingWhenLaterLineHitDictionaryCannotAcceptIntegerWrites()
    {
        var firstLineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var secondLineHits = new Dictionary<int, long>
        {
            [1] = 0
        };
        var modules = new OrderedDictionary
        {
            ["Calculator.dll"] = new OrderedDictionary
            {
                ["src/Calculator.cs"] = CreateCoverletDocument(firstLineHits),
                ["src/Other.cs"] = new Dictionary<string, object>
                {
                    ["Other"] = new Dictionary<string, object>
                    {
                        ["Add"] = secondLineHits
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

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeFalse();

        updatedLines.Should().Be(0);
        firstLineHits[1].Should().Be(0);
        secondLineHits[1].Should().Be(0);
    }

    [Fact]
    public void EmptyBackendCoverageReturnsTrueWithoutUpdatingLines()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string>());

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void BackendCoverageWithNoActiveLinesReturnsTrueWithoutMatchingPath()
    {
        var lineHits = new Dictionary<int, int>
        {
            [1] = 0
        };
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = lineHits
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Other.cs"] = Convert.ToBase64String([0])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(0);
        lineHits[1].Should().Be(0);
    }

    [Fact]
    public void ThrowingLinesPropertyDoesNotEscapeTryApply()
    {
        var modules = new Dictionary<string, object>
        {
            ["Calculator.dll"] = new Dictionary<string, object>
            {
                ["src/Calculator.cs"] = new Dictionary<string, object>
                {
                    ["Calculator"] = new Dictionary<string, object>
                    {
                        ["Add"] = new MethodWithThrowingLines()
                    }
                }
            }
        };
        var backfill = CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
            });

        CoverletCoverageBackfill.TryApply(modules, backfill, out var updatedLines).Should().BeTrue();

        updatedLines.Should().Be(0);
    }

    private static Dictionary<string, object> CreateCoverletDocument(Dictionary<int, int> lineHits)
    {
        return new Dictionary<string, object>
        {
            ["Calculator"] = new Dictionary<string, object>
            {
                ["Add"] = lineHits
            }
        };
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

    private sealed class MethodWithLines
    {
        public MethodWithLines(Dictionary<int, int> lines)
        {
            Lines = lines;
        }

        public Dictionary<int, int> Lines { get; }
    }

    private sealed class MethodWithLinesField
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Coverlet exposes Lines as a public field in some supported versions.")]
        public readonly Dictionary<int, int> Lines;

        public MethodWithLinesField(Dictionary<int, int> lines)
        {
            Lines = lines;
        }
    }

    private sealed class MethodWithThrowingLines
    {
        public Dictionary<int, int> Lines => throw new InvalidOperationException("Lines are unavailable.");
    }
}
