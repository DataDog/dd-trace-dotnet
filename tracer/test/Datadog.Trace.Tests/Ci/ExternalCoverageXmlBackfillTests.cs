// <copyright file="ExternalCoverageXmlBackfillTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
public class ExternalCoverageXmlBackfillTests
{
    private static readonly object SourceRootOverrideLock = new();
    private static readonly object CurrentDirectoryLock = new();

    [Fact]
    public void CoberturaReportIsRewrittenWithBackendCoveredLines()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <methods>
                        <method name="Add" line-rate="0">
                          <lines>
                            <line number="2" hits="0" />
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<line number="2" hits="1" />""");
            finalXml.Should().Contain("lines-covered=\"2\"");
            finalXml.Should().Contain("""<method name="Add" line-rate="1">""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenBackendCoveredLineHasDuplicateClassLines()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="2" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                    <class name="CalculatorB" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaMethodLineMirroringIsLimitedToCurrentClass()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="2" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                    <class name="CalculatorB" filename="src/Calculator.cs" line-rate="0">
                      <methods>
                        <method name="Leaked" line-rate="0">
                          <lines>
                            <line number="1" hits="0" />
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(50);
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<line number="1" hits="1" />""");
            finalXml.Should().Contain("""<method name="Leaked" line-rate="0">""");
            finalXml.Should().Contain("""<line number="1" hits="0" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresNonPositiveLineNumbers()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="2" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="0" hits="0" />
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<line number="1" hits="1" />""");
            finalXml.Should().Contain("lines-valid=\"1\"");
            finalXml.Should().Contain("lines-covered=\"1\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresNonExactBackendKeyThatMatchesMultipleLocalFiles()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="2" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="repo-a/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                    <class name="CalculatorB" filename="repo-b/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresLocalPathThatOnlyMatchesMultipleActiveBackendKeysBySuffix()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="repo/src/common/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    ["src/common/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                    ["common/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
                });

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresInactiveBackendSuffixThatIsMoreSpecificThanActiveSuffix()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="workspace/service-a/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                    ["service-a/src/Calculator.cs"] = Convert.ToBase64String([0b_0000_0000])
                });

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ExpectedCoverletXmlFormatsDoNotFallThroughToMicrosoftLineXml()
    {
        var filePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var autoResult).Should().BeTrue();
            autoResult.Diagnostic.Should().Be("microsoft-line");

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfillData: null, applyBackfill: false, out _).Should().BeFalse();
            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfillData: null, applyBackfill: false, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void SourceLessLooseLineXmlIsNotMicrosoftLineBackfillableOrPublishable()
    {
        var filePath = WriteTempCoverageFile(
            """
            <report>
              <line number="1" hits="1" />
            </report>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out _).Should().BeFalse();
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("aggregate-only or unsupported");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void SourceLessLooseLineXmlDoesNotShadowMicrosoftAggregateCoverage()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="0" lines_not_covered="1">
                  <line number="99" hits="1" />
                </module>
              </modules>
            </results>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("aggregate-only or unsupported");

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();
            result.Percentage.Should().Be(50);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            result.Diagnostic.Should().Be("microsoft-aggregate");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineXmlWithSourcePathIsLineBackfillableAndPublishable()
    {
        var filePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
              </file>
            </report>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            result.Diagnostic.Should().Be("microsoft-line");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsCoberturaAbsoluteUriSourcePath()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="file:///tmp/repo/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("source paths");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsCoberturaAbsoluteUriSourceRoot()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>file:///tmp/repo</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("source paths");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsOpenCoverAbsoluteUriSourcePath()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="file:///tmp/repo/src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("source paths");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsMicrosoftAbsoluteUriSourcePath()
    {
        var filePath = WriteTempCoverageFile(
            """
            <report>
              <file path="file:///tmp/repo/src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("source paths");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverReportIsRewrittenWithBackendCoveredSequencePoints()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
                      <Methods>
                        <Method sequenceCoverage="50">
                          <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
                          <SequencePoints>
                            <SequencePoint vc="1" sl="1" fileid="1" />
                            <SequencePoint vc="0" sl="2" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<SequencePoint vc="1" sl="2" fileid="1" />""");
            finalXml.Should().Contain("sequenceCoverage=\"100\"");
            finalXml.Should().Contain("""<Method sequenceCoverage="100">""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("file:///tmp/repo/src/Calculator.cs")]
    [InlineData("file:/C:/tmp/repo/src/Calculator.cs")]
    [InlineData("file:C:/tmp/repo/src/Calculator.cs")]
    public void OpenCoverBackfillFailsClosedWhenFilePathIsAbsoluteUri(string sourcePath)
    {
        var originalContents =
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="SOURCE_PATH" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """.Replace("SOURCE_PATH", sourcePath);
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [SkippableFact]
    public void OpenCoverBackfillMatchesWindowsRootedSourceRootPathByExactRelativePath()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        lock (SourceRootOverrideLock)
        {
            var sourceRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-source-root-{Guid.NewGuid():N}");
            var sourcePath = Path.Combine(sourceRoot, "src", "Calculator.cs");
            var filePath = WriteTempCoverageFile(
                """
                <CoverageSession>
                  <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                  <Modules>
                    <Module>
                      <Files>
                        <File uid="1" fullPath="SOURCE_PATH" />
                      </Files>
                      <Classes>
                        <Class>
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <Methods>
                            <Method sequenceCoverage="0">
                              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                              <SequencePoints>
                                <SequencePoint vc="0" sl="1" fileid="1" />
                              </SequencePoints>
                            </Method>
                          </Methods>
                        </Class>
                      </Classes>
                    </Module>
                  </Modules>
                </CoverageSession>
                """.Replace("SOURCE_PATH", sourcePath));

            try
            {
                using var sourceRootOverride = new SourceRootOverride(sourceRoot);
                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

                result.Backfilled.Should().BeTrue();
                File.ReadAllText(filePath).Should().Contain("""<SequencePoint vc="1" sl="1" fileid="1" />""");
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void OpenCoverBackfillUsesModuleLocalFileUidMaps()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/A.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/B.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            var backfill = BackfillForLineMap(
                new Dictionary<string, int>
                {
                    ["src/A.cs"] = 1,
                    ["src/B.cs"] = 1
                });

            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<File uid="1" fullPath="src/A.cs" />""");
            finalXml.Should().Contain("""<File uid="1" fullPath="src/B.cs" />""");
            finalXml.Should().Contain("visitedSequencePoints=\"2\" sequenceCoverage=\"100\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillRefreshesVisitedMethodAndClassCounts()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" numClasses="1" visitedClasses="0" numMethods="1" visitedMethods="0" />
              <Modules>
                <Module>
                  <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" numClasses="1" visitedClasses="0" numMethods="1" visitedMethods="0" />
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" numClasses="1" visitedClasses="0" numMethods="1" visitedMethods="0" />
                      <Methods>
                        <Method visited="false" sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" numClasses="0" visitedClasses="0" numMethods="1" visitedMethods="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                          <MethodPoint vc="0" sl="1" fileid="1" />
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            var finalDoc = new XmlDocument();
            finalDoc.LoadXml(finalXml);
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Summary"), "numClasses", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Summary"), "visitedClasses", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Summary"), "numMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Summary"), "visitedMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Summary"), "numClasses", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Summary"), "visitedClasses", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Summary"), "numMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Summary"), "visitedMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Summary"), "numClasses", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Summary"), "visitedClasses", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Summary"), "numMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Summary"), "visitedMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Methods/Method/Summary"), "numMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Methods/Method/Summary"), "visitedMethods", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Methods/Method"), "visited", "true");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Methods/Method/MethodPoint"), "vc", "1");
            AssertXmlAttribute(finalDoc.SelectSingleNode("/CoverageSession/Modules/Module/Classes/Class/Methods/Method/SequencePoints/SequencePoint"), "vc", "1");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillAllowsDuplicateSingleLineSequencePoints()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="2" fileid="1" />
                            <SequencePoint vc="0" sl="2" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            CountOccurrences(File.ReadAllText(filePath), """<SequencePoint vc="1" sl="2" fileid="1" />""").Should().Be(2);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillUsesSequencePointEndLineRangeWhenFullyCovered()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="10" el="12" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 10, 11, 12);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<SequencePoint vc="1" sl="10" el="12" fileid="1" />""");
            finalXml.Should().Contain("sequenceCoverage=\"100\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillFailsClosedWhenSequencePointRangeIsPartiallyCovered()
    {
        var originalContents =
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="10" el="12" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 11);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverResultUsesLineCountsForSequencePointRanges()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
                      <Methods>
                        <Method sequenceCoverage="50">
                          <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
                          <SequencePoints>
                            <SequencePoint vc="1" sl="10" el="12" fileid="1" />
                            <SequencePoint vc="0" sl="20" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();

            result.Percentage.Should().Be(75);
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(3);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillFailsClosedWhenRangesRepresentSameBackendLine()
    {
        var originalContents =
            """
            <CoverageSession>
              <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="2" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" el="3" fileid="1" />
                            <SequencePoint vc="0" sl="2" el="4" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 1, 2, 3, 4);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillFailsClosedWhenInvalidSequencePointRangesArePresent()
    {
        var originalContents =
            """
            <CoverageSession>
              <Summary numSequencePoints="4" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="4" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="4" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="0" fileid="1" />
                            <SequencePoint vc="0" sl="-1" fileid="1" />
                            <SequencePoint vc="0" sl="2" el="1" fileid="1" />
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillIgnoresSequencePointWhenEndLineIsMalformed()
    {
        const string CoverageXml =
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method sequenceCoverage="0">
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="10" el="bad" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 10);

            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresBackendPathThatDoesNotMatchReport()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Other.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(50);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(1);
            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Contain("""<line number="2" hits="0" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresRelativeFilenameThatWouldOnlyMatchBackendPathBySuffix()
    {
        var originalContents =
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("integrations/Samples.XUnitTests/TestSuite.cs", line: 23);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillFailsClosedWhenRelativeFilePathWouldOnlyMatchBackendPathBySuffix()
    {
        var originalContents =
            """
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="workspace/repo/src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessOpenCover(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillIgnoresRelativeFilePathThatWouldOnlyMatchBackendPathBySuffix()
    {
        var originalContents =
            """
            <report>
              <file path="workspace/repo/src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresRelativeFilenameThatWouldOnlyMatchBackendPathWithAdditionalPrefix()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("ProjectB/src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();
            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="0" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("file:///tmp/repo/src/Calculator.cs")]
    [InlineData("file:/C:/tmp/repo/src/Calculator.cs")]
    [InlineData("file:C:/tmp/repo/src/Calculator.cs")]
    public void CoberturaBackfillFailsClosedWhenFilenameIsAbsoluteUri(string sourcePath)
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="SOURCE_PATH" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """.Replace("SOURCE_PATH", sourcePath);
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenFilenameIsAbsoluteUriEvenWithSourceRoot()
    {
        const string OriginalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="file:///tmp/repo/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(OriginalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(OriginalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenSourceRootIsAbsoluteUri()
    {
        const string OriginalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>file:///tmp/repo</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(OriginalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(OriginalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaReportUsesDeterministicSourceRootForRelativeFilename()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo/src</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            lock (SourceRootOverrideLock)
            {
                using var sourceRootOverride = new SourceRootOverride(Path.Combine(Path.GetDirectoryName(filePath)!, "repo"));
                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

                result.Percentage.Should().Be(100);
                result.CoveredLines.Should().Be(1);
                result.Backfilled.Should().BeTrue();
                result.Rewritten.Should().BeTrue();
                File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
            }
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaReportUsesCurrentDirectorySourceRootForSubdirectoryFilename()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>.</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("./src/Calculator.cs")]
    [InlineData("../src/Calculator.cs")]
    [InlineData("src/./Calculator.cs")]
    [InlineData("src/../Calculator.cs")]
    public void CoberturaBackfillFailsClosedWhenCurrentDirectorySourceRootFilenameContainsRelativeDotSegment(string filename)
    {
        var originalContents =
            $$"""
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>.</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="{{filename}}" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [SkippableFact]
    public void CoberturaReportUsesUnixRootSourceRootForFilesystemRootRelativeFilename()
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        lock (SourceRootOverrideLock)
        {
            var repositoryRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-cobertura-root-{Guid.NewGuid():N}");
            var sourceFile = Path.Combine(repositoryRoot, "src", "Calculator.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
            File.WriteAllText(sourceFile, string.Empty);
            var filename = Path.GetFullPath(sourceFile).TrimStart('/');
            var filePath = WriteTempCoverageFile(
                $$"""
                <coverage line-rate="0" lines-valid="1" lines-covered="0">
                  <sources>
                    <source>/</source>
                  </sources>
                  <packages>
                    <package name="sample" line-rate="0">
                      <classes>
                        <class name="Calculator" filename="{{filename}}" line-rate="0">
                          <lines>
                            <line number="1" hits="0" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);

            try
            {
                using var sourceRootOverride = new SourceRootOverride(repositoryRoot);
                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

                result.Percentage.Should().Be(100);
                result.Backfilled.Should().BeTrue();
                File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
            }
            finally
            {
                File.Delete(filePath);
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }

    [SkippableTheory]
    [InlineData("../src/Calculator.cs")]
    [InlineData("./src/Calculator.cs")]
    public void CoberturaBackfillFailsClosedWhenRootSourceRootFilenameContainsRelativeDotSegment(string filename)
    {
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        var originalContents =
            $$"""
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>/</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="{{filename}}" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenRelativeFilenameHasAmbiguousSourceRoots()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo/src-a</source>
                <source>repo/src-b</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src-b/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenSubdirectoryFilenameHasAmbiguousPeerSourceRoots()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo/src-a</source>
                <source>repo/src-b</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="common/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src-b/common/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillDoesNotUseCurrentDirectoryToDisambiguateSourceRoots()
    {
        lock (CurrentDirectoryLock)
        {
            var previousCurrentDirectory = Environment.CurrentDirectory;
            var rootDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-cobertura-cwd-{Guid.NewGuid():N}");
            var reportDirectory = Path.Combine(rootDirectory, "reports");
            var currentDirectory = Path.Combine(rootDirectory, "cwd");
            var filePath = Path.Combine(reportDirectory, "coverage.cobertura.xml");
            var originalContents =
                """
                <coverage line-rate="0" lines-valid="1" lines-covered="0">
                  <sources>
                    <source>repo-a</source>
                    <source>repo-b</source>
                  </sources>
                  <packages>
                    <package name="sample" line-rate="0">
                      <classes>
                        <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                          <lines>
                            <line number="1" hits="0" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;

            try
            {
                Directory.CreateDirectory(reportDirectory);
                Directory.CreateDirectory(Path.Combine(currentDirectory, "repo-a", "src"));
                File.WriteAllText(Path.Combine(currentDirectory, "repo-a", "src", "Calculator.cs"), string.Empty);
                File.WriteAllText(filePath, originalContents);
                Directory.SetCurrentDirectory(currentDirectory);

                var backfill = BackfillForLine("repo-a/src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

                File.ReadAllText(filePath).Should().Be(originalContents);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);
                TryDeleteDirectory(rootDirectory);
            }
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenSubdirectoryFilenameHasAmbiguousSourceRoots()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo</source>
                <source>repo/service</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresUnixRootedFilenameThatWouldOnlyMatchBySuffix()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresWindowsRootedFilenameThatWouldOnlyMatchBySuffix()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="\src\Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenSourceRootCandidateEscapesSourceRoot()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo-a</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="../src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenSourceRootsContainSafeAndEscapingCandidates()
    {
        lock (SourceRootOverrideLock)
        {
            var repositoryRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-cobertura-sources-{Guid.NewGuid():N}");
            var safeSourceRoot = Path.Combine(repositoryRoot, "src");
            var unsafeSourceRoot = Path.Combine(repositoryRoot, "other");
            var filePath = string.Empty;
            try
            {
                Directory.CreateDirectory(safeSourceRoot);
                Directory.CreateDirectory(unsafeSourceRoot);
                using var sourceRootOverride = new SourceRootOverride(repositoryRoot);
                var originalContents =
                    $"""
                    <coverage line-rate="0" lines-valid="1" lines-covered="0">
                      <sources>
                        <source>{safeSourceRoot}</source>
                        <source>{unsafeSourceRoot}</source>
                      </sources>
                      <packages>
                        <package name="sample" line-rate="0">
                          <classes>
                            <class name="Calculator" filename="../src/Calculator.cs" line-rate="0">
                              <lines>
                                <line number="1" hits="0" />
                              </lines>
                            </class>
                          </classes>
                        </package>
                      </packages>
                    </coverage>
                    """;
                filePath = WriteTempCoverageFile(originalContents);

                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcessCobertura(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

                File.ReadAllText(filePath).Should().Be(originalContents);
            }
            finally
            {
                TryDeleteFile(filePath);
                TryDeleteDirectory(repositoryRoot);
            }
        }
    }

    [Fact]
    public void CoberturaBackfillFailsClosedWhenRelativeFilenameContainsParentDirectorySegmentWithoutSources()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="repo-a/../src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [SkippableFact]
    public void CoberturaSourceRootsDeduplicateCaseOnWindows()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo</source>
                <source>REPO</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("repo/src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Backfilled.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaReportUsesSingleSourceRootForSubdirectoryFilename()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>repo</source>
              </sources>
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            lock (SourceRootOverrideLock)
            {
                using var sourceRootOverride = new SourceRootOverride(Path.Combine(Path.GetDirectoryName(filePath)!, "repo"));
                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

                result.Percentage.Should().Be(100);
                result.Backfilled.Should().BeTrue();
                File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
            }
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ValidationStateAllowsBackendCoverageSplitAcrossCoberturaReports()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Other" filename="src/Other.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLineMap(
                new Dictionary<string, int>
                {
                    ["src/Calculator.cs"] = 1,
                    ["src/Other.cs"] = 1
                });
            var validationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, validationState, out var firstResult).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, validationState, out var secondResult).Should().BeTrue();

            validationState.CanPublish().Should().BeTrue();
            firstResult.Backfilled.Should().BeTrue();
            secondResult.Backfilled.Should().BeTrue();
            File.ReadAllText(firstFilePath).Should().Contain("""<line number="1" hits="1" />""");
            File.ReadAllText(secondFilePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void ValidationStateAllowsSameBackendLineAcrossSeparateCoberturaReports()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorB" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);
            var firstValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var secondValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var mergedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, firstValidationState, out var firstResult).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, secondValidationState, out var secondResult).Should().BeTrue();
            mergedValidationState.Merge(firstValidationState);
            mergedValidationState.Merge(secondValidationState);

            mergedValidationState.CanPublish().Should().BeTrue();
            firstResult.Backfilled.Should().BeTrue();
            secondResult.Backfilled.Should().BeTrue();
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void StrictValidationStateFailsClosedWhenMergedReportsRepresentSameBackendLine()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);
            var firstValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var secondValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var mergedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState(rejectDuplicateRepresentedBackendLines: true);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(firstFilePath, backfill, applyBackfill: true, firstValidationState, out var firstResult).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcessMicrosoft(secondFilePath, backfill, applyBackfill: true, secondValidationState, out var secondResult).Should().BeTrue();
            mergedValidationState.Merge(firstValidationState);
            mergedValidationState.Merge(secondValidationState);

            firstResult.Backfilled.Should().BeTrue();
            secondResult.Backfilled.Should().BeTrue();
            mergedValidationState.CanPublish().Should().BeFalse();
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void ValidationStateAllowsReportsWhenBackendPathOnlyMatchesDifferentLocalCandidatesBySuffix()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="repo-a/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorB" filename="repo-b/src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);
            var firstValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var secondValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var mergedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, firstValidationState, out var firstResult).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, secondValidationState, out var secondResult).Should().BeTrue();
            mergedValidationState.Merge(firstValidationState);
            mergedValidationState.Merge(secondValidationState);

            mergedValidationState.CanPublish().Should().BeTrue();
            AssertProcessedWithoutBackfill(firstResult);
            AssertProcessedWithoutBackfill(secondResult);
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void ValidationStateAllowsReportRelativeSuffixMatchesFromDifferentAttachmentDirectoriesWhenNoSafeMatchExists()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstReportDirectory = Path.Combine(workspacePath, "first", "src");
        var secondReportDirectory = Path.Combine(workspacePath, "second", "src");
        var firstFilePath = Path.Combine(firstReportDirectory, "coverage.cobertura.xml");
        var secondFilePath = Path.Combine(secondReportDirectory, "coverage.cobertura.xml");
        var firstContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var secondContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorB" filename="Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        try
        {
            Directory.CreateDirectory(firstReportDirectory);
            Directory.CreateDirectory(secondReportDirectory);
            File.WriteAllText(firstFilePath, firstContents);
            File.WriteAllText(secondFilePath, secondContents);

            var backfill = BackfillForLine("src/Calculator.cs", line: 1);
            var firstValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var secondValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var mergedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, firstValidationState, out var firstResult).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, secondValidationState, out var secondResult).Should().BeTrue();
            mergedValidationState.Merge(firstValidationState);
            mergedValidationState.Merge(secondValidationState);

            mergedValidationState.CanPublish().Should().BeTrue();
            firstResult.Backfilled.Should().BeFalse();
            secondResult.Backfilled.Should().BeFalse();
        }
        finally
        {
            TryDeleteDirectory(workspacePath);
        }
    }

    [Fact]
    public void ValidationStateIgnoresInactiveBackendPathConflicts()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="InactiveA" filename="repo-a/src/Inactive.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="2" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="InactiveB" filename="repo-b/src/Inactive.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    ["src/Inactive.cs"] = Convert.ToBase64String([0]),
                    ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000])
                });
            var firstValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var secondValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var mergedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, firstValidationState, out _).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, secondValidationState, out var secondResult).Should().BeTrue();
            mergedValidationState.Merge(firstValidationState);
            mergedValidationState.Merge(secondValidationState);

            mergedValidationState.CanPublish().Should().BeTrue();
            secondResult.Backfilled.Should().BeTrue();
            File.ReadAllText(secondFilePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [SkippableFact]
    public void ValidationStateTreatsLocalCandidateCaseAsEquivalentOnWindows()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorA" filename="SRC/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="CalculatorB" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 1, 2);
            var validationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, validationState, out _).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, validationState, out _).Should().BeTrue();

            validationState.CanPublish().Should().BeTrue();
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MicrosoftLineReportIsRewrittenWithBackendCoveredLines()
    {
        var filePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
                <line number="2" hits="0" />
              </file>
            </report>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            result.Diagnostic.Should().Be("microsoft-line");
            File.ReadAllText(filePath).Should().Contain("""<line number="2" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillFailsClosedWhenRelativePathContainsCurrentDirectorySegment()
    {
        var originalContents =
            """
            <report>
              <file path="src/./Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/./Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("file:///tmp/repo/src/Calculator.cs")]
    [InlineData("file:/C:/tmp/repo/src/Calculator.cs")]
    [InlineData("file:C:/tmp/repo/src/Calculator.cs")]
    public void MicrosoftLineBackfillFailsClosedWhenSourcePathIsAbsoluteUri(string sourcePath)
    {
        var originalContents =
            """
            <report>
              <file path="SOURCE_PATH">
                <line number="1" hits="0" />
              </file>
            </report>
            """.Replace("SOURCE_PATH", sourcePath);
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillRefreshesModuleAggregateLineCounts()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="0" lines_partially_covered="0" lines_not_covered="2">
                  <source_file path="src/Calculator.cs">
                    <line number="1" hits="1" />
                    <line number="2" hits="0" />
                  </source_file>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<module lines_covered="2" lines_partially_covered="0" lines_not_covered="0">""");
            finalXml.Should().Contain("""<line number="2" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillRefreshesSourceContainerAggregateLineCounts()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module>
                  <source_file path="src/Calculator.cs" lines_covered="1" lines_partially_covered="0" lines_not_covered="1">
                    <line number="1" hits="1" />
                    <line number="2" hits="0" />
                  </source_file>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<source_file path="src/Calculator.cs" lines_covered="2" lines_partially_covered="0" lines_not_covered="0">""");
            finalXml.Should().Contain("""<line number="2" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftRangeBackfillUsesSourceFileMap()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="0" lines_not_covered="1">
                  <functions>
                    <function lines_covered="1" lines_partially_covered="0" lines_not_covered="1">
                      <ranges>
                        <range source_id="0" covered="yes" start_line="1" start_column="1" end_line="1" end_column="10" />
                        <range source_id="0" covered="no" start_line="2" start_column="1" end_line="2" end_column="10" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Calculator.cs" />
                  </source_files>
                </module>
              </modules>
            </results>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<range source_id="0" covered="yes" start_line="2" start_column="1" end_line="2" end_column="10" />""");
            finalXml.Should().Contain("""<module lines_covered="2" lines_partially_covered="0" lines_not_covered="0">""");
            finalXml.Should().Contain("""<function lines_covered="2" lines_partially_covered="0" lines_not_covered="0">""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftRangeBackfillFailsClosedWhenAnyModuleHasConflictingSourceFileId()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="0" lines_partially_covered="0" lines_not_covered="1">
                  <functions>
                    <function lines_covered="0" lines_partially_covered="0" lines_not_covered="1">
                      <ranges>
                        <range source_id="0" covered="no" start_line="1" end_line="1" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Calculator.cs" />
                  </source_files>
                </module>
                <module lines_covered="0" lines_partially_covered="0" lines_not_covered="1">
                  <functions>
                    <function lines_covered="0" lines_partially_covered="0" lines_not_covered="1">
                      <ranges>
                        <range source_id="0" covered="no" start_line="1" end_line="1" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Other.cs" />
                    <source_file id="0" path="src/Ambiguous.cs" />
                  </source_files>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var originalXml = File.ReadAllText(filePath);
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftRangeBackfillDeduplicatesOverlappingSourceLines()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="0" lines_partially_covered="0" lines_not_covered="4">
                  <functions>
                    <function lines_covered="0" lines_partially_covered="0" lines_not_covered="4">
                      <ranges>
                        <range source_id="0" covered="no" start_line="10" end_line="12" />
                        <range source_id="0" covered="no" start_line="11" end_line="13" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Calculator.cs" />
                  </source_files>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 10, 11, 12, 13);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(4);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<module lines_covered="4" lines_partially_covered="0" lines_not_covered="0">""");
            finalXml.Should().Contain("""<function lines_covered="4" lines_partially_covered="0" lines_not_covered="0">""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftRangeBackfillFailsClosedWhenOverlappingRangeWouldDowngradeBackfilledLine()
    {
        const string CoverageXml =
            """
            <results>
              <modules>
                <module lines_covered="0" lines_partially_covered="0" lines_not_covered="4">
                  <functions>
                    <function lines_covered="0" lines_partially_covered="0" lines_not_covered="4">
                      <ranges>
                        <range source_id="0" covered="no" start_line="10" end_line="12" />
                        <range source_id="0" covered="no" start_line="11" end_line="13" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Calculator.cs" />
                  </source_files>
                </module>
              </modules>
            </results>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 10, 11, 12);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftRangeBackfillFailsClosedWhenEndLineIsMalformed()
    {
        const string CoverageXml =
            """
            <results>
              <modules>
                <module lines_covered="0" lines_partially_covered="0" lines_not_covered="1">
                  <functions>
                    <function lines_covered="0" lines_partially_covered="0" lines_not_covered="1">
                      <ranges>
                        <range source_id="0" covered="no" start_line="10" end_line="bad" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Calculator.cs" />
                  </source_files>
                </module>
              </modules>
            </results>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 10);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftRangeBackfillPreservesUnrelatedPartialLines()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="1" lines_not_covered="1">
                  <functions>
                    <function lines_covered="1" lines_partially_covered="1" lines_not_covered="1">
                      <ranges>
                        <range source_id="0" covered="yes" start_line="1" />
                        <range source_id="0" covered="no" start_line="2" />
                        <range source_id="0" covered="partial" start_line="3" />
                      </ranges>
                    </function>
                  </functions>
                  <source_files>
                    <source_file id="0" path="src/Calculator.cs" />
                  </source_files>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(66.67);
            result.ExecutableLines.Should().Be(3);
            result.CoveredLines.Should().Be(2);
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<range source_id="0" covered="partial" start_line="3" />""");
            finalXml.Should().Contain("""<module lines_covered="2" lines_partially_covered="1" lines_not_covered="0">""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillUsesSourceFileNames()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>1</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>0</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                      <Lines>
                        <LnStart>2</LnStart>
                        <LnEnd>2</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("<Coverage>0</Coverage>");
            finalXml.Should().Contain("<LinesCovered>2</LinesCovered>");
            finalXml.Should().Contain("<LinesNotCovered>0</LinesNotCovered>");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillFailsClosedWhenLineEndIsMalformed()
    {
        const string CoverageXml =
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>10</LnStart>
                        <LnEnd>bad</LnEnd>
                        <Coverage>0</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 10);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlSourceFileIdsAreScopedPerModule()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Other.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """);

        try
        {
            var backfill = CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                    ["src/Other.cs"] = Convert.ToBase64String([0b_1000_0000])
                });

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            File.ReadAllText(filePath).Should().Contain("<SourceFileName>src/Calculator.cs</SourceFileName>");
            File.ReadAllText(filePath).Should().Contain("<SourceFileName>src/Other.cs</SourceFileName>");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlFailsClosedOnConflictingSourceFileIdWithinModule()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Other.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillFailsClosedWhenAnyModuleHasConflictingSourceFileId()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>1</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Other.cs</SourceFileName>
                </SourceFileNames>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Ambiguous.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """);

        try
        {
            var originalXml = File.ReadAllText(filePath);
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillUsesCoverageDsPrivLineRanges()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>3</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>10</LnStart>
                        <LnEnd>12</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 10, 11, 12);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(3);
            result.CoveredLines.Should().Be(3);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("<Coverage>0</Coverage>");
            finalXml.Should().Contain("<LinesCovered>3</LinesCovered>");
            finalXml.Should().Contain("<LinesNotCovered>0</LinesNotCovered>");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillDeduplicatesOverlappingLineRanges()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>4</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>10</LnStart>
                        <LnEnd>12</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                      <Lines>
                        <LnStart>11</LnStart>
                        <LnEnd>13</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 10, 11, 12, 13);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(4);
            result.Backfilled.Should().BeTrue();
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("<LinesCovered>4</LinesCovered>");
            finalXml.Should().Contain("<LinesNotCovered>0</LinesNotCovered>");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillFailsClosedWhenOverlappingRangeWouldDowngradeBackfilledLine()
    {
        const string CoverageXml =
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>4</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>10</LnStart>
                        <LnEnd>12</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                      <Lines>
                        <LnStart>11</LnStart>
                        <LnEnd>13</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 10, 11, 12);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillFailsClosedWhenCoverageDsPrivRangeIsPartiallyCovered()
    {
        const string CoverageXml =
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>3</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>10</LnStart>
                        <LnEnd>12</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 11);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillIgnoresBackendLineOutsideCoverageDsPrivRange()
    {
        const string CoverageXml =
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>0</LinesPartiallyCovered>
                <LinesNotCovered>3</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>10</LnStart>
                        <LnEnd>12</LnEnd>
                        <Coverage>2</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 13);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillRefreshesModuleAggregateWithExistingPartialLineCount()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="1" lines_not_covered="1">
                  <source_file path="src/Calculator.cs">
                    <line number="1" hits="1" />
                    <line number="2" hits="0" />
                    <line number="3" hits="0" />
                  </source_file>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(66.67);
            result.ExecutableLines.Should().Be(3);
            result.CoveredLines.Should().Be(2);
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<module lines_covered="2" lines_partially_covered="0" lines_not_covered="1">""");
            finalXml.Should().Contain("""<line number="2" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillPreservesGenericCoveredPartialLineWhenRefreshingAggregate()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="1" lines_not_covered="1">
                  <source_file path="src/Calculator.cs">
                    <line number="1" covered="true" />
                    <line number="2" covered="false" />
                    <line number="3" covered="partial" />
                  </source_file>
                </module>
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(66.67);
            result.ExecutableLines.Should().Be(3);
            result.CoveredLines.Should().Be(2);
            var finalXml = File.ReadAllText(filePath);
            finalXml.Should().Contain("""<module lines_covered="2" lines_partially_covered="1" lines_not_covered="0">""");
            finalXml.Should().Contain("""<line number="2" covered="true" />""");
            finalXml.Should().Contain("""<line number="3" covered="partial" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillFailsClosedForPartialCoveredAttribute()
    {
        const string CoverageXml =
            """
            <results>
              <modules>
                <module lines_covered="0" lines_partially_covered="1" lines_not_covered="0">
                  <source_file path="src/Calculator.cs">
                    <line number="1" covered="partial" />
                  </source_file>
                </module>
              </modules>
            </results>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftCoverageXmlBackfillFailsClosedForPartialCoverageElement()
    {
        const string CoverageXml =
            """
            <CoverageDSPriv>
              <Module>
                <LinesCovered>0</LinesCovered>
                <LinesPartiallyCovered>1</LinesPartiallyCovered>
                <LinesNotCovered>0</LinesNotCovered>
                <NamespaceTable>
                  <Class>
                    <Method>
                      <Lines>
                        <LnStart>1</LnStart>
                        <LnEnd>1</LnEnd>
                        <Coverage>1</Coverage>
                        <SourceFileID>0</SourceFileID>
                      </Lines>
                    </Method>
                  </Class>
                </NamespaceTable>
                <SourceFileNames>
                  <SourceFileID>0</SourceFileID>
                  <SourceFileName>src/Calculator.cs</SourceFileName>
                </SourceFileNames>
              </Module>
            </CoverageDSPriv>
            """;
        var filePath = WriteTempCoverageFile(CoverageXml);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(CoverageXml);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillIgnoresNonPositiveLineNumbers()
    {
        var filePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="0" hits="0" />
                <line number="1" hits="0" />
              </file>
            </report>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcessMicrosoft(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillMatchesRootedSourceRootPathByExactRelativePath()
    {
        lock (SourceRootOverrideLock)
        {
            var sourceRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-source-root-{Guid.NewGuid():N}");
            var filePath = string.Empty;
            try
            {
                Directory.CreateDirectory(sourceRoot);
                using var sourceRootOverride = new SourceRootOverride(sourceRoot);
                var rootedSourcePath = Path.Combine(sourceRoot, "src", "Calculator.cs");
                filePath = WriteTempCoverageFile(
                    $"""
                    <report>
                      <file path="{rootedSourcePath}">
                        <line number="1" hits="0" />
                      </file>
                    </report>
                    """);

                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

                result.Backfilled.Should().BeTrue();
                File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
            }
            finally
            {
                TryDeleteFile(filePath);
                TryDeleteDirectory(sourceRoot);
            }
        }
    }

    [Fact]
    public void MicrosoftLineBackfillIgnoresRootedSourceRootPathThatWouldOnlyMatchBySuffix()
    {
        lock (SourceRootOverrideLock)
        {
            var sourceRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-source-root-{Guid.NewGuid():N}");
            var filePath = string.Empty;
            try
            {
                Directory.CreateDirectory(sourceRoot);
                using var sourceRootOverride = new SourceRootOverride(sourceRoot);
                var rootedSourcePath = Path.Combine(sourceRoot, "repo-a", "src", "Calculator.cs");
                var originalContents =
                    $"""
                    <report>
                      <file path="{rootedSourcePath}">
                        <line number="1" hits="0" />
                      </file>
                    </report>
                    """;
                filePath = WriteTempCoverageFile(originalContents);

                var backfill = BackfillForLine("src/Calculator.cs", line: 1);

                ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

                AssertProcessedWithoutBackfill(result);
                File.ReadAllText(filePath).Should().Be(originalContents);
            }
            finally
            {
                TryDeleteFile(filePath);
                TryDeleteDirectory(sourceRoot);
            }
        }
    }

    [Fact]
    public void AggregateOnlyMicrosoftXmlDoesNotBackfill()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="0" lines_not_covered="1" />
              </modules>
            </results>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();
            result.Percentage.Should().Be(50);
            result.Backfilled.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AggregateOnlyCoberturaXmlStillPublishesNormalCoverageWhenBackfillIsNotRequired()
    {
        var filePath = WriteTempCoverageFile("""<coverage line-rate="0.25" lines-valid="4" lines-covered="1" />""");

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();

            result.Percentage.Should().Be(25);
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            result.Diagnostic.Should().Be("cobertura-aggregate");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AggregateOnlyCoberturaLineRatePublishesPercentageWithoutCountsWhenBackfillIsNotRequired()
    {
        var filePath = WriteTempCoverageFile("""<coverage line-rate="0.25" />""");

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();

            result.Percentage.Should().Be(25);
            result.ExecutableLines.Should().BeNull();
            result.CoveredLines.Should().BeNull();
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            result.Diagnostic.Should().Be("cobertura-aggregate");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AggregateOnlyOpenCoverXmlStillPublishesNormalCoverageWhenBackfillIsNotRequired()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="4" visitedSequencePoints="1" sequenceCoverage="25" />
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();

            result.Percentage.Should().Be(25);
            result.ExecutableLines.Should().Be(4);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            result.Diagnostic.Should().Be("opencover-aggregate");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AggregateOnlyOpenCoverSequenceCoveragePublishesPercentageWithoutCountsWhenBackfillIsNotRequired()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary sequenceCoverage="25" />
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();

            result.Percentage.Should().Be(25);
            result.ExecutableLines.Should().BeNull();
            result.CoveredLines.Should().BeNull();
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            result.Diagnostic.Should().Be("opencover-aggregate");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MergedLineCoverageUnionsOverlappingCoberturaReports()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="src/Calculator.cs">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="src/Calculator.cs">
                      <lines>
                        <line number="23" hits="0" />
                        <line number="26" hits="1" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

            executableLines.Should().Be(2);
            coveredLines.Should().Be(2);
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MergedLineCoverageUsesValidatedBackendIdentityAcrossReportDirectories()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-{Guid.NewGuid():N}");
        var firstDirectory = Path.Combine(rootDirectory, "first");
        var secondDirectory = Path.Combine(rootDirectory, "second");
        var firstFilePath = string.Empty;
        var secondFilePath = string.Empty;
        try
        {
            Directory.CreateDirectory(firstDirectory);
            Directory.CreateDirectory(secondDirectory);
            firstFilePath = Path.Combine(firstDirectory, "coverage.cobertura.xml");
            secondFilePath = Path.Combine(secondDirectory, "coverage.cobertura.xml");
            var firstReportXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="0" />
                            <line number="2" hits="0" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            var secondReportXml =
                """
                <coverage>
                  <packages>
                    <package>
                      <classes>
                        <class filename="src/Calculator.cs">
                          <lines>
                            <line number="1" hits="0" />
                            <line number="2" hits="1" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
            File.WriteAllText(firstFilePath, firstReportXml);
            File.WriteAllText(secondFilePath, secondReportXml);
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);
            var firstValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var secondValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();
            var mergedValidationState = new ExternalCoverageXmlBackfill.CoverageBackfillValidationState();

            ExternalCoverageXmlBackfill.TryProcess(firstFilePath, backfill, applyBackfill: true, firstValidationState, out _).Should().BeTrue();
            ExternalCoverageXmlBackfill.TryProcess(secondFilePath, backfill, applyBackfill: true, secondValidationState, out _).Should().BeTrue();
            mergedValidationState.Merge(firstValidationState);
            mergedValidationState.Merge(secondValidationState);

            mergedValidationState.CanPublish().Should().BeTrue();
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], mergedValidationState, out var executableLines, out var coveredLines).Should().BeTrue();
            executableLines.Should().Be(2);
            coveredLines.Should().Be(2);
        }
        finally
        {
            TryDeleteFile(firstFilePath);
            TryDeleteFile(secondFilePath);
            TryDeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public void MergedLineCoverageUnionsOpenCoverRangeLinesBySourceLine()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="1" sl="10" el="12" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="0" sl="10" el="10" fileid="1" />
                            <SequencePoint vc="0" sl="11" el="11" fileid="1" />
                            <SequencePoint vc="0" sl="12" el="12" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

            executableLines.Should().Be(3);
            coveredLines.Should().Be(3);
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MergedLineCoverageFailsWhenAnyCoberturaReportContributesNoExecutableLines()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="src/Calculator.cs">
                      <lines>
                        <line number="23" hits="1" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5" />
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out _, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MergedLineCoverageFailsWhenAnyOpenCoverReportContributesNoExecutableLines()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="1" sequenceCoverage="100" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="1" sl="23" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="1" sequenceCoverage="100" />
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out _, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MergedLineCoverageFailsWhenAnyMicrosoftReportContributesNoExecutableLines()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
              </file>
            </report>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="0" lines_not_covered="1" />
              </modules>
            </results>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out _, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MergedLineCoverageAllowsFullyOverlappingReports()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="src/Calculator.cs">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <coverage>
              <packages>
                <package>
                  <classes>
                    <class filename="src/Calculator.cs">
                      <lines>
                        <line number="23" hits="1" />
                        <line number="26" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

            executableLines.Should().Be(2);
            coveredLines.Should().Be(1);
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Theory]
    [InlineData("cobertura")]
    [InlineData("opencover")]
    [InlineData("microsoft")]
    public void MergedLineCoverageSeparatesSameRelativePathsFromDifferentReportDirectories(string format)
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-merge-{Guid.NewGuid():N}");
        var firstReportDirectory = Path.Combine(workspacePath, "first");
        var secondReportDirectory = Path.Combine(workspacePath, "second");
        var firstFilePath = Path.Combine(firstReportDirectory, "coverage.xml");
        var secondFilePath = Path.Combine(secondReportDirectory, "coverage.xml");

        try
        {
            Directory.CreateDirectory(firstReportDirectory);
            Directory.CreateDirectory(secondReportDirectory);
            File.WriteAllText(firstFilePath, CreateRelativeMergedLineCoverageReport(format, covered: true));
            File.WriteAllText(secondFilePath, CreateRelativeMergedLineCoverageReport(format, covered: false));

            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

            executableLines.Should().Be(2);
            coveredLines.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(workspacePath);
        }
    }

    [Fact]
    public void MergedLineCoverageTreatsSourceRootRelativeAndRootedPathsAsSameLine()
    {
        lock (SourceRootOverrideLock)
        {
            var sourceRoot = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-source-root-{Guid.NewGuid():N}");
            var rootedSourcePath = Path.Combine(sourceRoot, "src", "Calculator.cs");
            var firstFilePath = string.Empty;
            var secondFilePath = string.Empty;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(rootedSourcePath)!);
                using var sourceRootOverride = new SourceRootOverride(sourceRoot);
                firstFilePath = Path.Combine(sourceRoot, "coverage.xml");
                var firstReportContents =
                    """
                    <report>
                      <file path="src/Calculator.cs">
                        <line number="1" hits="1" />
                      </file>
                    </report>
                    """;
                File.WriteAllText(firstFilePath, firstReportContents);
                secondFilePath = WriteTempCoverageFile(
                    $"""
                     <report>
                       <file path="{rootedSourcePath}">
                         <line number="1" hits="0" />
                       </file>
                     </report>
                     """);

                ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

                executableLines.Should().Be(1);
                coveredLines.Should().Be(1);
            }
            finally
            {
                TryDeleteFile(firstFilePath);
                TryDeleteFile(secondFilePath);
                TryDeleteDirectory(sourceRoot);
            }
        }
    }

    [Fact]
    public void MergedLineCoverageUnionsOverlappingMicrosoftReports()
    {
        var firstFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
                <line number="2" hits="0" />
              </file>
            </report>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
                <line number="2" hits="1" />
              </file>
            </report>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

            executableLines.Should().Be(2);
            coveredLines.Should().Be(2);
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void MergedLineCoverageTreatsMicrosoftPathCaseAsEquivalentOnWindows()
    {
        if (!FrameworkDescription.Instance.IsWindows())
        {
            return;
        }

        var firstFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="SRC/Calculator.cs">
                <line number="1" hits="1" />
              </file>
            </report>
            """);
        var secondFilePath = WriteTempCoverageFile(
            """
            <report>
              <file path="src/calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryReadMergedLineCoverage([firstFilePath, secondFilePath], out var executableLines, out var coveredLines).Should().BeTrue();

            executableLines.Should().Be(1);
            coveredLines.Should().Be(1);
        }
        finally
        {
            File.Delete(firstFilePath);
            File.Delete(secondFilePath);
        }
    }

    [Fact]
    public void CoberturaBackfillRewritesCoverletClassAndMethodLineEntriesWithoutDoubleCounting()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <methods>
                        <method name="Add">
                          <lines>
                            <line number="2" hits="0" />
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            var contents = File.ReadAllText(filePath);
            CountOccurrences(contents, """<line number="2" hits="1" />""").Should().Be(2);
            contents.Should().Contain("lines-covered=\"2\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillAppliesMatchedCoverageAndIgnoresBackendOnlyFiles()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = CoverageBackfillData.FromBackendCoverage(
                new Dictionary<string, string>
                {
                    ["src/Calculator.cs"] = Convert.ToBase64String([0b_1000_0000]),
                    ["src/Other.cs"] = Convert.ToBase64String([0b_1000_0000])
                });

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillAppliesRepresentableLinesWhenMatchedReportPathMissesBackendLines()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 1, 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresBackendLineThatOnlyExistsInMethodLines()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <methods>
                        <method name="Add">
                          <lines>
                            <line number="2" hits="0" />
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillIgnoresRequiredLineWhenHitsAttributeIsMalformed()
    {
        var originalContents =
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="2" hits="bad" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillAppliesRepresentableLinesWhenMatchedReportPathMissesBackendLines()
    {
        var originalContents =
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method>
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 1, 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<SequencePoint vc="1" sl="1" fileid="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverBackfillIgnoresRequiredLineWhenVisitAttributeIsMalformed()
    {
        var originalContents =
            """
            <CoverageSession>
              <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                      <Methods>
                        <Method>
                          <Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
                          <SequencePoints>
                            <SequencePoint vc="bad" sl="2" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillAppliesRepresentableLinesWhenMatchedReportPathMissesBackendLines()
    {
        var originalContents =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </report>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLines("src/Calculator.cs", 1, 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineBackfillFailsClosedWhenRequiredHitAttributeIsMalformed()
    {
        var originalContents =
            """
            <report>
              <file path="src/Calculator.cs">
                <line number="2" hits="bad" />
              </file>
            </report>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("false", "true")]
    [InlineData("no", "yes")]
    public void MicrosoftLineBackfillPreservesCoveredAttributeConvention(string originalValue, string expectedValue)
    {
        var filePath = WriteTempCoverageFile(
            $$"""
            <report>
              <file path="src/Calculator.cs">
                <line number="1" covered="true" />
                <line number="2" covered="{{originalValue}}" />
              </file>
            </report>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain($"""<line number="2" covered="{expectedValue}" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void DtdCoverageFileDoesNotLoad()
    {
        var filePath = WriteTempCoverageFile(
            """
            <!DOCTYPE coverage [
              <!ENTITY external SYSTEM "file:///etc/passwd">
            ]>
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs">
                      <lines>
                        <line number="1" hits="&external;" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out _).Should().BeFalse();
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("could not be inspected");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void EmptyBackendCoverageDoesNotMarkReportBackfilled()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string>());

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(50);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
            File.ReadAllText(filePath).Should().Contain("""<line number="2" hits="0" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MatchedBackendCoverageWithNoLineChangesMarksReportBackfilled()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="1" lines-valid="1" lines-covered="1">
              <packages>
                <package name="sample" line-rate="1">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="1">
                      <lines>
                        <line number="1" hits="1" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(1);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeFalse();
            File.ReadAllText(filePath).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaLineWithoutNumberDoesNotRepresentRequiredBackendLine()
    {
        var originalContents =
            """
            <coverage line-rate="1" lines-valid="1" lines-covered="1">
              <packages>
                <package name="sample" line-rate="1">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="1">
                      <lines>
                        <line number="1" hits="1" />
                        <line hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void OpenCoverMalformedSequencePointAttributesDoNotThrow()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
                      <Methods>
                        <Method>
                          <Summary numSequencePoints="2" visitedSequencePoints="1" sequenceCoverage="50" />
                          <SequencePoints>
                            <SequencePoint vc="1" sl="1" fileid="1" />
                            <SequencePoint vc="not-a-number" sl="not-a-number" fileid="not-a-number" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out var result).Should().BeTrue();

            result.Percentage.Should().Be(50);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeFalse();
            result.Rewritten.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MicrosoftLineWithoutHitAttributeDoesNotRepresentRequiredBackendLine()
    {
        var originalContents =
            """
            <report>
              <metadata>
                <line number="99" hits="0" />
              </metadata>
              <file path="src/Calculator.cs">
                <line number="1" hits="1" />
                <line number="2" />
              </file>
            </report>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            AssertProcessedWithoutBackfill(result);
            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NamespacedMicrosoftLineReportIsRewrittenWithBackendCoveredLines()
    {
        var filePath = WriteTempCoverageFile(
            """
            <m:report xmlns:m="urn:microsoft-code-coverage">
              <m:file path="src/Calculator.cs">
                <m:line number="1" hits="1" />
                <m:line number="2" hits="0" />
              </m:file>
            </m:report>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            result.Diagnostic.Should().Be("microsoft-line");
            File.ReadAllText(filePath).Should().Contain("number=\"2\" hits=\"1\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NamespacedCoberturaReportIsRewrittenWithBackendCoveredLines()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage xmlns="urn:cobertura" line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.ExecutableLines.Should().Be(2);
            result.CoveredLines.Should().Be(2);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="2" hits="1" />""");
            File.ReadAllText(filePath).Should().Contain("lines-covered=\"2\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NamespacedOpenCoverReportIsRejectedAsUnsupported()
    {
        var filePath = WriteTempCoverageFile(
            """
            <oc:CoverageSession xmlns:oc="urn:opencover">
              <oc:Summary numSequencePoints="1" visitedSequencePoints="0" sequenceCoverage="0" />
              <oc:Modules>
                <oc:Module>
                  <oc:Files>
                    <oc:File uid="1" fullPath="src/Calculator.cs" />
                  </oc:Files>
                  <oc:Classes>
                    <oc:Class>
                      <oc:Methods>
                        <oc:Method>
                          <oc:SequencePoints>
                            <oc:SequencePoint vc="0" sl="1" fileid="1" />
                          </oc:SequencePoints>
                        </oc:Method>
                      </oc:Methods>
                    </oc:Class>
                  </oc:Classes>
                </oc:Module>
              </oc:Modules>
            </oc:CoverageSession>
            """);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("aggregate-only or unsupported");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Utf16BomCoberturaReportCanBeBackfilled()
    {
        var filePath = WriteTempCoverageFile(
            """
            <?xml version="1.0" encoding="utf-16"?>
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <packages>
                <package name="sample" line-rate="0">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """,
            Encoding.Unicode);

        try
        {
            var backfill = BackfillForLine("src/Calculator.cs", line: 1);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();

            var rewrittenBytes = File.ReadAllBytes(filePath);
            rewrittenBytes.Should().StartWith(Encoding.Unicode.GetPreamble());
            File.ReadAllText(filePath, Encoding.Unicode).Should().Contain("""<line number="1" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [SkippableFact]
    public void SaveFailurePreservesOriginalReport()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var originalContents =
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            using var lockedFile = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void SaveReplacementFailurePreservesOriginalReport()
    {
        var originalContents =
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            ExternalCoverageXmlBackfill.BeforeReplaceXmlDocumentForTests = path =>
            {
                if (string.Equals(path, Path.GetFullPath(filePath), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Replacement failed.");
                }
            };
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            ExternalCoverageXmlBackfill.BeforeReplaceXmlDocumentForTests = null;
            ExternalCoverageXmlBackfill.ReplaceXmlDocumentForTests = null;
            File.Delete(filePath);
        }
    }

    [Fact]
    public void PartialSaveReplacementFailureRestoresOriginalReportFromBackup()
    {
        var originalContents =
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="src/Calculator.cs" line-rate="0.5">
                      <lines>
                        <line number="1" hits="1" />
                        <line number="2" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var filePath = WriteTempCoverageFile(originalContents);

        try
        {
            ExternalCoverageXmlBackfill.ReplaceXmlDocumentForTests = (_, destinationPath, backupPath) =>
            {
                File.Move(destinationPath, backupPath);
                throw new IOException("Replacement failed after preserving the original report.");
            };
            var backfill = BackfillForLine("src/Calculator.cs", line: 2);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();

            File.ReadAllText(filePath).Should().Be(originalContents);
        }
        finally
        {
            ExternalCoverageXmlBackfill.BeforeReplaceXmlDocumentForTests = null;
            ExternalCoverageXmlBackfill.ReplaceXmlDocumentForTests = null;
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Verifies that the report processor tolerates coverage paths that disappear before they are inspected.
    /// </summary>
    [Fact]
    public void MissingCoverageFileDoesNotThrow()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "coverage.xml");

        ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out _).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that malformed XML is treated as an unsupported report instead of escaping the Try API.
    /// </summary>
    [Fact]
    public void MalformedCoverageFileDoesNotThrow()
    {
        var filePath = WriteTempCoverageFile("<coverage>");

        try
        {
            ExternalCoverageXmlBackfill.TryProcess(filePath, backfillData: null, applyBackfill: false, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsAggregateOnlyXml()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <modules>
                <module lines_covered="1" lines_partially_covered="0" lines_not_covered="1" />
              </modules>
            </results>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("aggregate-only");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsOpenCoverSequencePointWithoutResolvableFile()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="99" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("aggregate-only");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionAcceptsOpenCoverWithFileMapAndSequencePoint()
    {
        var filePath = WriteTempCoverageFile(
            """
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="src/Calculator.cs" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeTrue();
            reason.Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("../src/Calculator.cs")]
    [InlineData("./src/Calculator.cs")]
    public void BackfillableReportDetectionRejectsOpenCoverUnsafeRelativeSourcePath(string sourcePath)
    {
        var filePath = WriteTempCoverageFile(
            $$"""
            <CoverageSession>
              <Modules>
                <Module>
                  <Files>
                    <File uid="1" fullPath="{{sourcePath}}" />
                  </Files>
                  <Classes>
                    <Class>
                      <Methods>
                        <Method>
                          <SequencePoints>
                            <SequencePoint vc="0" sl="1" fileid="1" />
                          </SequencePoints>
                        </Method>
                      </Methods>
                    </Class>
                  </Classes>
                </Module>
              </Modules>
            </CoverageSession>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("safely matched");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsMicrosoftLineUnsafeRelativeSourcePath()
    {
        var filePath = WriteTempCoverageFile(
            """
            <results>
              <file path="../src/Calculator.cs">
                <line number="1" hits="0" />
              </file>
            </results>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("safely matched");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void BackfillableReportDetectionRejectsCoberturaUnsafeSourceRootCandidate()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0" lines-valid="1" lines-covered="0">
              <sources>
                <source>../src</source>
              </sources>
              <packages>
                <package name="sample">
                  <classes>
                    <class name="Calculator" filename="Calculator.cs">
                      <lines>
                        <line number="1" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            ExternalCoverageXmlBackfill.IsLineBackfillableReport(filePath, out var reason).Should().BeFalse();
            reason.Should().Contain("safely matched");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static CoverageBackfillData BackfillForLine(string path, int line)
        => BackfillForLines(path, line);

    private static CoverageBackfillData BackfillForLines(string path, params int[] lines)
    {
        var maxLine = 0;
        foreach (var line in lines)
        {
            if (line > maxLine)
            {
                maxLine = line;
            }
        }

        if (maxLine <= 0)
        {
            return CoverageBackfillData.FromBackendCoverage(new Dictionary<string, string> { [path] = string.Empty });
        }

        var bitmap = new byte[(maxLine + 7) / 8];
        foreach (var line in lines)
        {
            if (line <= 0)
            {
                continue;
            }

            var index = line - 1;
            bitmap[index >> 3] |= (byte)(128 >> (index & 7));
        }

        return CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                [path] = Convert.ToBase64String(bitmap)
            });
    }

    private static CoverageBackfillData BackfillForLineMap(Dictionary<string, int> lineByPath)
    {
        var coverage = new Dictionary<string, string>();
        foreach (var item in lineByPath)
        {
            var bitmap = new byte[(item.Value + 7) / 8];
            var index = item.Value - 1;
            bitmap[index >> 3] = (byte)(128 >> (index & 7));
            coverage[item.Key] = Convert.ToBase64String(bitmap);
        }

        return CoverageBackfillData.FromBackendCoverage(coverage);
    }

    private static void AssertXmlAttribute(XmlNode node, string attributeName, string expectedValue)
    {
        node.Should().NotBeNull();
        node.Attributes?[attributeName]?.Value.Should().Be(expectedValue);
    }

    private static string WriteTempCoverageFile(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }

    private static string WriteTempCoverageFile(string contents, Encoding encoding)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents, encoding);
        return path;
    }

    private static string CreateRelativeMergedLineCoverageReport(string format, bool covered)
    {
        var hits = covered ? "1" : "0";
        return format switch
        {
            "cobertura" =>
                $"""
                 <coverage>
                   <packages>
                     <package>
                       <classes>
                         <class filename="src/Calculator.cs">
                           <lines>
                             <line number="1" hits="{hits}" />
                           </lines>
                         </class>
                       </classes>
                     </package>
                   </packages>
                 </coverage>
                 """,
            "opencover" =>
                $"""
                 <CoverageSession>
                   <Modules>
                     <Module>
                       <Files>
                         <File uid="1" fullPath="src/Calculator.cs" />
                       </Files>
                       <Classes>
                         <Class>
                           <Methods>
                             <Method>
                               <SequencePoints>
                                 <SequencePoint vc="{hits}" sl="1" fileid="1" />
                               </SequencePoints>
                             </Method>
                           </Methods>
                         </Class>
                       </Classes>
                     </Module>
                   </Modules>
                 </CoverageSession>
                 """,
            "microsoft" =>
                $"""
                 <report>
                   <file path="src/Calculator.cs">
                     <line number="1" hits="{hits}" />
                   </file>
                 </report>
                 """,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    private static void AssertProcessedWithoutBackfill(ExternalCoverageXmlResult result)
    {
        result.Backfilled.Should().BeFalse();
        result.Rewritten.Should().BeFalse();
    }

    private static void TryDeleteFile(string filePath)
    {
        if (filePath.Length > 0 && File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    /// <summary>
    /// Temporarily replaces the cached CI source root used by source-root-relative path matching.
    /// </summary>
    private sealed class SourceRootOverride : IDisposable
    {
        private readonly string _previousSourceRoot;
        private readonly bool _hadPreviousSourceRoot;

        public SourceRootOverride(string sourceRoot)
        {
            var previousSourceRoot = CIEnvironmentValues.Instance.SourceRoot;
            _hadPreviousSourceRoot = previousSourceRoot is not null;
            _previousSourceRoot = previousSourceRoot ?? string.Empty;
            typeof(CIEnvironmentValues).GetProperty(nameof(CIEnvironmentValues.SourceRoot))?.SetValue(CIEnvironmentValues.Instance, sourceRoot);
        }

        public void Dispose()
        {
            typeof(CIEnvironmentValues).GetProperty(nameof(CIEnvironmentValues.SourceRoot))?.SetValue(CIEnvironmentValues.Instance, _hadPreviousSourceRoot ? _previousSourceRoot : null);
        }
    }
}
