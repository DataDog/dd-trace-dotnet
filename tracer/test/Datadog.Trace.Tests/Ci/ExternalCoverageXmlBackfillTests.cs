// <copyright file="ExternalCoverageXmlBackfillTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.Coverage.Backfill;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class ExternalCoverageXmlBackfillTests
{
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
                        <Method>
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
            File.ReadAllText(filePath).Should().Contain("""<SequencePoint vc="1" sl="2" fileid="1" />""");
            File.ReadAllText(filePath).Should().Contain("sequenceCoverage=\"100\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaBackfillFailsWhenBackendPathDoesNotMatchReport()
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

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out _).Should().BeFalse();
            File.ReadAllText(filePath).Should().Contain("""<line number="2" hits="0" />""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CoberturaReportMatchesBackendPathByUnambiguousSuffix()
    {
        var filePath = WriteTempCoverageFile(
            """
            <coverage line-rate="0.5" lines-valid="2" lines-covered="1">
              <packages>
                <package name="sample" line-rate="0.5">
                  <classes>
                    <class name="Calculator" filename="integrations/Samples.XUnitTests/TestSuite.cs" line-rate="0.5">
                      <lines>
                        <line number="23" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        try
        {
            var backfill = BackfillForLine("tracer/test/test-applications/integrations/Samples.XUnitTests/TestSuite.cs", line: 23);

            ExternalCoverageXmlBackfill.TryProcess(filePath, backfill, applyBackfill: true, out var result).Should().BeTrue();

            result.Percentage.Should().Be(100);
            result.CoveredLines.Should().Be(1);
            result.Backfilled.Should().BeTrue();
            result.Rewritten.Should().BeTrue();
            File.ReadAllText(filePath).Should().Contain("""<line number="23" hits="1" />""");
        }
        finally
        {
            File.Delete(filePath);
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

    private static CoverageBackfillData BackfillForLine(string path, int line)
    {
        var bitmap = new byte[(line + 7) / 8];
        var index = line - 1;
        bitmap[index >> 3] = (byte)(128 >> (index & 7));
        return CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                [path] = Convert.ToBase64String(bitmap)
            });
    }

    private static string WriteTempCoverageFile(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }
}
