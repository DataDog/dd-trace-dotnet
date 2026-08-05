// <copyright file="GlobalCoverageMemoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0 || NET10_0

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public sealed class GlobalCoverageMemoryTests : TestingFrameworkEvpTest
{
    private const long MaximumStressMemoryGrowth = 384L * 1024 * 1024;
    private const string SampleName = "NUnitGlobalCoverageMemory";
    private const string SampleSourceFileName = "GlobalCoverageMemoryTests.cs";
    private const int CommonCoverageLine = 131_072;
    private const int FirstCoverageSentinelLine = 131_073;
    private const int MiddleCoverageSentinelLine = 131_074;
    private const int LastCoverageSentinelLine = 131_075;
    private readonly ITestOutputHelper _output;

    public GlobalCoverageMemoryTests(ITestOutputHelper output)
        : base(SampleName, output)
    {
        _output = output;
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "LoadTest")]
    public void SixThousandNUnitContextsDoNotRetainNativeCoverageBuffers()
    {
        RunStress(expectedCaseCount: 6_000);
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.NUnitGlobalCoverageMemoryCoverlet), MemberType = typeof(PackageVersions))]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task SupportedCoverletVersionsProduceCorrectCoverage(string packageVersion)
    {
        var coverageResults = new List<CodeCoverageAggregationResult>();
        var unresolvedCoverageReferences = new List<string>();
        InjectSession(
            out var sessionId,
            out _,
            out _,
            out _,
            out _,
            out _,
            out var runId);

        using var root = new TemporaryDirectory("dd-coverlet-compatibility-");
        var resultsDirectory = Directory.CreateDirectory(Path.Combine(root.RootPath, "results")).FullName;
        var logDirectory = Directory.CreateDirectory(Path.Combine(root.RootPath, "logs")).FullName;
        var progressPath = Path.Combine(root.RootPath, "progress.jsonl");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverage, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.TestSessionCommand, "dotnet test --collect XPlat Code Coverage");
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDirectory);
        SetEnvironmentVariable("NUNIT_GLOBAL_COVERAGE_CASE_COUNT", "1");
        SetEnvironmentVariable("NUNIT_GLOBAL_COVERAGE_PROGRESS_PATH", progressPath);

        var coverageIpcTestOptimization = CreateCoverageIpcTestOptimization(runId);
        using var ipcServer = new IpcServer($"session_{sessionId}");
        ipcServer.SetMessageReceivedCallback(
            message =>
            {
                if (TryResolveCoverageIpcMessage(coverageIpcTestOptimization, sessionId, message, out var coverageResult, out var unresolvedReference))
                {
                    lock (coverageResults)
                    {
                        coverageResults.Add(coverageResult);
                    }
                }
                else if (unresolvedReference is not null)
                {
                    lock (unresolvedCoverageReferences)
                    {
                        unresolvedCoverageReferences.Add(unresolvedReference);
                    }
                }
            });

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true, useStatsD: !IsMacOS());
        using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                      agent,
                                      arguments: $"/Collect:\"XPlat Code Coverage;IncludeTestAssembly=true\" /ResultsDirectory:\"{resultsDirectory}\"",
                                      packageVersion: packageVersion,
                                      expectedExitCode: 0);

        AssertProgress(progressPath, expectedCaseCount: 1);
        AssertCoberturaCoverage(resultsDirectory);
        var logLines = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories)
                                .SelectMany(File.ReadLines)
                                .ToArray();
        logLines.Should().Contain(
            line => line.Contains(nameof(CoverageGetCoverageResultIntegration), StringComparison.Ordinal),
            "the native profiler must match and rewrite Coverlet.Core.Coverage.GetCoverageResult for every supported version");
        logLines.Should().NotContain(line => line.Contains("Could not cast to ICoverageResultProxy", StringComparison.Ordinal));

        lock (unresolvedCoverageReferences)
        {
            unresolvedCoverageReferences.Should().BeEmpty();
        }

        if (FrameworkDescription.Instance.IsWindows())
        {
            // On Windows the in-process Coverlet callback proves that the CallTarget integration ran.
            // Coverlet currently writes Cobertura without invoking that callback on Linux, where the
            // report assertions above still verify the real collector and its line-level correctness.
            CodeCoverageAggregationResult coverageResult;
            lock (coverageResults)
            {
                coverageResult = coverageResults.Should().ContainSingle().Subject;
            }

            coverageResult.Source.Should().Be(CodeCoverageReportSource.Coverlet);
            coverageResult.Percentage.Should().BeGreaterThan(0);
            coverageResult.ExecutableLines.Should().BeGreaterThan(0);
            coverageResult.CoveredLines.Should().BeGreaterThan(0);
        }
    }

    private static string? GetFileName(string? path)
    {
        if (path is null)
        {
            return null;
        }

        // CI builds sample PDBs on Windows before consuming them on Linux, so their document
        // names can use either directory separator regardless of the current operating system.
        var lastSeparator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return path[(lastSeparator + 1)..];
    }

    private void RunStress(int expectedCaseCount)
    {
        var environmentHelper = new EnvironmentHelper(SampleName, typeof(GlobalCoverageMemoryTests), _output);
        var sampleAssembly = environmentHelper.GetTestCommandForSampleApplicationPath();
        File.Exists(sampleAssembly).Should().BeTrue($"the required sample output must be present at {sampleAssembly}");

        var runnerDirectory = GetRunnerDirectory();
        var runnerAssembly = Path.Combine(runnerDirectory, "Datadog.Trace.Tools.Runner.dll");
        File.Exists(runnerAssembly).Should().BeTrue("the runner tool output must contain the runner assembly");
        File.Exists(Path.Combine(runnerDirectory, "Datadog.Trace.Coverage.collector.dll"))
            .Should()
            .BeTrue("the runner tool output must contain the Datadog VSTest collector");
        File.Exists(Path.Combine(runnerDirectory, "Datadog.collector.dll"))
            .Should()
            .BeFalse("the production runner layout must not contain the test suite's legacy collector with the same VSTest friendly name");

        using var root = new TemporaryDirectory("dd-global-coverage-memory-");
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(root.RootPath, "coverage")).FullName;

        using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
        var logDirectory = Directory.CreateDirectory(Path.Combine(root.RootPath, "logs")).FullName;
        var progressPath = Path.Combine(root.RootPath, "progress.jsonl");
        var targetCommand = CreateVstestCommand(environmentHelper.GetDotnetExe(), sampleAssembly);
        var arguments = CreateCiRunArguments(
            environmentHelper.MonitoringHome,
            agent.Port,
            coverageDirectory,
            logDirectory,
            progressPath,
            expectedCaseCount,
            targetCommand);

        var result = RunRunner(environmentHelper.GetDotnetExe(), runnerAssembly, arguments, logDirectory);
        result.ExitCode.Should().Be(0, result.Error);

        AssertLaunch(result.Output, runnerDirectory);
        var testhostProcessId = AssertProgress(progressPath, expectedCaseCount);
        AssertPublishedCoverage(coverageDirectory, expectedCaseCount);
        AssertCoverageDiagnostics(logDirectory, testhostProcessId, expectedCaseCount);
    }

    private ProcessResult RunRunner(string dotnetExecutable, string runnerAssembly, string[] arguments, string logDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = dotnetExecutable,
                WorkingDirectory = EnvironmentTools.GetSolutionDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.ArgumentList.Add(runnerAssembly);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Run the CLI in its own process so this load test exercises only production paths.
        // Debug output also gives the test an observable record of the normalized child command.
        process.StartInfo.Environment[ConfigurationKeys.DebugEnabled] = "1";
        process.StartInfo.Environment[ConfigurationKeys.LogDirectory] = logDirectory;
        process.Start().Should().BeTrue();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(20).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("The dd-trace CI run memory test exceeded 20 minutes.");
        }

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        _output.WriteLine(output);
        if (!string.IsNullOrWhiteSpace(error))
        {
            _output.WriteLine(error);
        }

        return new ProcessResult(process.ExitCode, output, error);
    }

    private string GetRunnerDirectory()
    {
        // The integration-test output also contains Datadog.collector.dll from DatadogTestCollector.
        // Use the tool output so VSTest resolves the same collector and directory layout shipped to customers.
        // The tool artifact used by integration tests is published for net8.0 independently of
        // the sample TFM. A net8 runner can launch both net8 and net10 VSTest assemblies.
        var pivot = $"{EnvironmentTools.GetBuildConfiguration().ToLowerInvariant()}_net8.0";
        return Path.Combine(
            EnvironmentTools.GetSolutionDirectory(),
            "artifacts",
            "bin",
            "Datadog.Trace.Tools.Runner.Tool",
            pivot);
    }

    private string[] CreateVstestCommand(string dotnetExecutable, string sampleAssembly)
        =>
        [
            dotnetExecutable,
            "vstest",
            sampleAssembly,
        ];

    private string[] CreateCiRunArguments(
        string monitoringHome,
        int agentPort,
        string coverageDirectory,
        string logDirectory,
        string progressPath,
        int expectedCaseCount,
        string[] targetCommand)
    {
        var arguments = new List<string>
        {
            "ci",
            "run",
            "--tracer-home",
            monitoringHome,
            "--agent-url",
            $"http://127.0.0.1:{agentPort}",
            "--set-env",
            $"{ConfigurationKeys.CIVisibility.CodeCoverage}=1",
            "--set-env",
            $"{ConfigurationKeys.CIVisibility.CodeCoveragePath}={coverageDirectory}",
            "--set-env",
            $"{ConfigurationKeys.LogDirectory}={logDirectory}",
            "--set-env",
            $"NUNIT_GLOBAL_COVERAGE_PROGRESS_PATH={progressPath}",
            "--set-env",
            $"NUNIT_GLOBAL_COVERAGE_CASE_COUNT={(expectedCaseCount == 1 ? "1" : string.Empty)}",
            "--set-env",
            $"{ConfigurationKeys.DebugEnabled}=1",
            "--"
        };

        arguments.AddRange(targetCommand);
        return arguments.ToArray();
    }

    private void AssertLaunch(string output, string runnerDirectory)
    {
        var launchLine = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                               .Should()
                               .ContainSingle(static line => line.StartsWith("Running:", StringComparison.Ordinal))
                               .Subject;
        var datadogCollectorCount = Regex.Matches(launchLine, @"(?<!\w)(?:/Collect:)?DatadogCoverage(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
        datadogCollectorCount.Should().Be(1, "dd-trace ci run must inject exactly one Datadog coverage collector");

        Regex.Matches(launchLine, Regex.Escape($"/TestAdapterPath:{runnerDirectory}"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.Should().Be(1);
    }

    private int AssertProgress(string progressPath, int expectedCaseCount)
    {
        File.Exists(progressPath).Should().BeTrue();
        var records = File.ReadAllLines(progressPath)
                          .Where(static line => !string.IsNullOrWhiteSpace(line))
                          .Select(static line => JsonConvert.DeserializeObject<ProgressRecord>(line))
                          .ToArray();

        records.Should().NotBeEmpty();
        records.Should().OnlyContain(record => record != null && record.Pid > 0 && record.ManagedBytes > 0);
        records.Select(static record => record!.Pid).Distinct().Should().ContainSingle("all test cases must run in one testhost process");
        records[^1]!.Completed.Should().Be(expectedCaseCount);

        if (expectedCaseCount > 1 && !IsMacOS())
        {
            // Process.PrivateMemorySize64 reports zero on macOS. Linux and Windows, which run this
            // regression in CI, must provide private-byte samples so the native leak remains covered.
            records.Should().OnlyContain(record => record!.PrivateBytes > 0, "the stress run requires private-byte measurements");
            var initialBytes = records[0]!.PrivateBytes;
            var maximumBytes = records.Max(static record => record!.PrivateBytes);
            (maximumBytes - initialBytes).Should()
                                         .BeLessThan(
                                              MaximumStressMemoryGrowth,
                                              "completed test contexts must not retain their 128 KiB native coverage buffers");
        }

        return records[0]!.Pid;
    }

    private void AssertPublishedCoverage(string coverageDirectory, int expectedCaseCount)
    {
        var sessionCoverage = Directory.GetFiles(coverageDirectory, "session-coverage-*.json", SearchOption.TopDirectoryOnly);
        sessionCoverage.Should().ContainSingle();
        var reader = new GlobalCoverageInputReader();
        reader.TryRead(sessionCoverage[0], out var coverage).Should().BeTrue();
        coverage.Should().NotBeNull();
        coverage!.GetTotalPercentage().Should().BeGreaterThan(0);
        var sampleFile = coverage.Components.SelectMany(static component => component.Files)
                                 .Should()
                                 .ContainSingle(file => string.Equals(GetFileName(file.Path), SampleSourceFileName, StringComparison.OrdinalIgnoreCase))
                                 .Subject;
        AssertLine(sampleFile.ExecutableBitmap, CommonCoverageLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, CommonCoverageLine, expected: true);
        AssertLine(sampleFile.ExecutableBitmap, FirstCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, FirstCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutableBitmap, MiddleCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, MiddleCoverageSentinelLine, expected: expectedCaseCount > 1);
        AssertLine(sampleFile.ExecutableBitmap, LastCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, LastCoverageSentinelLine, expected: expectedCaseCount > 1);

        Directory.GetFiles(coverageDirectory, GlobalCoverageProtocol.CoverageFilePattern, SearchOption.TopDirectoryOnly).Should().NotBeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-process-incomplete-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    private void AssertCoberturaCoverage(string resultsDirectory)
    {
        var reportPath = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
                                  .Should()
                                  .ContainSingle()
                                  .Subject;
        var sourceClasses = XDocument.Load(reportPath)
                                     .Descendants("class")
                                     .Where(element => string.Equals(GetFileName((string?)element.Attribute("filename")), SampleSourceFileName, StringComparison.OrdinalIgnoreCase))
                                     .ToArray();
        sourceClasses.Should().NotBeEmpty();
        var lines = sourceClasses.SelectMany(static element => element.Descendants("line"))
                                 .GroupBy(element => int.Parse(element.Attribute("number")!.Value, CultureInfo.InvariantCulture))
                                 .ToDictionary(
                                      static group => group.Key,
                                      static group => group.Sum(element => long.Parse(element.Attribute("hits")!.Value, CultureInfo.InvariantCulture)));

        lines[CommonCoverageLine].Should().BeGreaterThan(0);
        lines[FirstCoverageSentinelLine].Should().BeGreaterThan(0);
        lines[MiddleCoverageSentinelLine].Should().Be(0);
        lines[LastCoverageSentinelLine].Should().Be(0);
    }

    private bool TryResolveCoverageIpcMessage(ITestOptimization testOptimization, ulong sessionId, object message, out CodeCoverageAggregationResult result, out string? unresolvedReference)
    {
        result = default;
        unresolvedReference = null;

        if (message is SessionCodeCoverageMessage coverageMessage)
        {
            result = new CodeCoverageAggregationResult(
                coverageMessage.Source,
                coverageMessage.Value,
                coverageMessage.Backfilled,
                coverageMessage.ExecutableLines,
                coverageMessage.CoveredLines,
                coverageMessage.Diagnostic,
                coverageMessage.ResultId,
                coverageMessage.BackfillValidated,
                coverageMessage.BackfillNotApplicable,
                coverageMessage.BackfillValidation,
                coverageMessage.SupersededResultIds);
            return true;
        }

        if (message is SessionCodeCoverageReferenceMessage referenceMessage)
        {
            if (CoverageBackfillDataStore.TryReadCoverageIpcResult(testOptimization, sessionId, referenceMessage.Source, referenceMessage.ResultId, out result))
            {
                return true;
            }

            unresolvedReference = $"{referenceMessage.Source}:{referenceMessage.ResultId}";
        }

        return false;
    }

    private ITestOptimization CreateCoverageIpcTestOptimization(string runId)
    {
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.RunId).Returns(runId);
        testOptimization.Setup(x => x.CIValues).Returns(new CoverageIpcTestEnvironmentValues(Environment.CurrentDirectory));
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(GlobalCoverageMemoryTests)));
        return testOptimization.Object;
    }

    private void AssertLine(byte[]? bitmap, int line, bool expected)
    {
        bitmap.Should().NotBeNull();
        var zeroBasedLine = line - 1;
        var byteIndex = zeroBasedLine >> 3;
        bitmap!.Length.Should().BeGreaterThan(byteIndex);
        var mask = (byte)(128 >> (zeroBasedLine & 7));
        ((bitmap[byteIndex] & mask) != 0).Should().Be(expected, $"line {line} should have the expected coverage state");
    }

    private void AssertCoverageDiagnostics(string logDirectory, int testhostProcessId, int expectedCaseCount)
    {
        var logLines = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories)
                                .SelectMany(File.ReadLines)
                                .ToArray();
        var contextMatch = FindLastMatch(
            logLines,
            $@"Global coverage context diagnostics: pid={testhostProcessId}, started=(\d+), closed=(\d+), disposed=(\d+), merged=(\d+)\.");
        var nativeMatch = FindLastMatch(
            logLines,
            $@"Global coverage native context-buffer diagnostics: pid={testhostProcessId}, currentBytes=(\d+), peakBytes=(\d+), activeBuffers=(\d+), peakBuffers=(\d+)\.");
        var nativeAllocationMatch = FindLastMatch(
            logLines,
            $@"Global coverage native context-buffer allocation diagnostics: pid={testhostProcessId}, allocations=(\d+), frees=(\d+), maximumBufferBytes=(\d+)\.");

        Parse(contextMatch, 1).Should().Be(expectedCaseCount);
        Parse(contextMatch, 2).Should().Be(expectedCaseCount);
        Parse(contextMatch, 3).Should().Be(expectedCaseCount);
        Parse(contextMatch, 4).Should().Be(expectedCaseCount);

        Parse(nativeMatch, 1).Should().Be(0);
        Parse(nativeMatch, 2).Should().BeGreaterThanOrEqualTo(128 * 1024);
        Parse(nativeMatch, 3).Should().Be(0);
        Parse(nativeMatch, 4).Should().BeGreaterThanOrEqualTo(1);
        var allocations = Parse(nativeAllocationMatch, 1);
        allocations.Should().BeGreaterThanOrEqualTo(expectedCaseCount);
        Parse(nativeAllocationMatch, 2).Should().Be(allocations);
        Parse(nativeAllocationMatch, 3).Should().BeGreaterThanOrEqualTo(128 * 1024);

        static System.Text.RegularExpressions.Match FindLastMatch(string[] lines, string pattern)
        {
            var match = lines.Select(line => Regex.Match(line, pattern, RegexOptions.CultureInvariant))
                             .LastOrDefault(static candidate => candidate.Success);
            match.Should().NotBeNull("the sealed testhost must emit global coverage lifecycle diagnostics");
            return match!;
        }

        static long Parse(System.Text.RegularExpressions.Match match, int group)
            => long.Parse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private readonly record struct ProcessResult(int ExitCode, string Output, string Error);

    private sealed class ProgressRecord
    {
        public int Pid { get; set; }

        public int Completed { get; set; }

        public long PrivateBytes { get; set; }

        public long ManagedBytes { get; set; }
    }

    private sealed class CoverageIpcTestEnvironmentValues : CIEnvironmentValues
    {
        public CoverageIpcTestEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory(string prefix)
        {
            RootPath = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

#endif
