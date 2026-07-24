// <copyright file="CiRunGlobalCoverageMemoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0

#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests;

[Collection(nameof(ConsoleTestsCollection))]
public sealed class CiRunGlobalCoverageMemoryTests
{
    private const long MaximumStressPrivateBytesGrowth = 384L * 1024 * 1024;
    private const string SampleName = "NUnitGlobalCoverageMemory";
    private const string SampleProjectRelativePath = "tracer/test/test-applications/integrations/Samples.NUnitGlobalCoverageMemory/Samples.NUnitGlobalCoverageMemory.csproj";
    private const string SampleSourceFileName = "GlobalCoverageMemoryTests.cs";
    private const int CommonCoverageLine = 131_072;
    private const int FirstCoverageSentinelLine = 131_073;
    private const int MiddleCoverageSentinelLine = 131_074;
    private const int LastCoverageSentinelLine = 131_075;
    private readonly ITestOutputHelper _output;

    public CiRunGlobalCoverageMemoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public void DotnetTestSdk10OuterCommandHook()
    {
        Skip.IfNot(FrameworkDescription.Instance.IsWindows());
        AssertSdk10();

        RunStress(
            packageVersion: "6.0.0",
            expectedCaseCount: 1,
            includeCoverlet: false,
            useDotnetTest: true);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public void TestingPlatformCoverageWithoutDatadogCollectorSealsAndAllowsDirectoryReuse()
    {
        Skip.IfNot(FrameworkDescription.Instance.IsWindows());
        AssertSdk10();

        RunStress(
            packageVersion: "6.0.0",
            expectedCaseCount: 1,
            includeCoverlet: false,
            useDotnetTest: true,
            useTestingPlatformCoverage: true,
            runCount: 2);
    }

    [SkippableTheory]
    [InlineData("3.2.0")]
    [InlineData("6.0.0")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "LoadTest")]
    public void SixThousandNUnitContextsDoNotRetainNativeCoverageBuffers(string coverletVersion)
    {
        Skip.IfNot(FrameworkDescription.Instance.IsWindows());
        RunStress(
            packageVersion: coverletVersion,
            expectedCaseCount: 6_000,
            includeCoverlet: true,
            useDotnetTest: false);
    }

    private void RunStress(
        string packageVersion,
        int expectedCaseCount,
        bool includeCoverlet,
        bool useDotnetTest,
        bool useTestingPlatformCoverage = false,
        int runCount = 1)
    {
        var environmentHelper = new EnvironmentHelper(SampleName, typeof(CiRunGlobalCoverageMemoryTests), _output);
        var sampleAssembly = environmentHelper.GetTestCommandForSampleApplicationPath(packageVersion, "net8.0");
        Skip.IfNot(File.Exists(sampleAssembly), $"The versioned sample was not built: {sampleAssembly}");

        var collectorAssembly = Path.Combine(AppContext.BaseDirectory, "Datadog.Trace.Coverage.collector.dll");
        File.Exists(collectorAssembly).Should().BeTrue("the runner output must contain the Datadog VSTest collector");

        using var root = new TemporaryDirectory("dd-global-coverage-memory-");
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(root.RootPath, "coverage")).FullName;

        using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());
        for (var runIndex = 0; runIndex < runCount; runIndex++)
        {
            var logDirectory = Directory.CreateDirectory(Path.Combine(root.RootPath, $"logs-{runIndex}")).FullName;
            var progressPath = Path.Combine(root.RootPath, $"progress-{runIndex}.jsonl");
            var targetCommand = useDotnetTest
                                    ? CreateDotnetTestCommand(environmentHelper, useTestingPlatformCoverage)
                                    : CreateVstestCommand(environmentHelper.GetDotnetExe(), sampleAssembly, includeCoverlet);
            var arguments = CreateCiRunArguments(
                environmentHelper.MonitoringHome,
                agent.Port,
                coverageDirectory,
                logDirectory,
                progressPath,
                expectedCaseCount,
                targetCommand);

            var result = RunRunner(environmentHelper.GetDotnetExe(), arguments, logDirectory);
            result.ExitCode.Should().Be(0, result.Error);

            AssertLaunch(result.Output, useDotnetTest, useTestingPlatformCoverage);
            var testhostProcessId = AssertProgress(progressPath, expectedCaseCount);
            var publishedCoverage = AssertPublishedCoverage(coverageDirectory, expectedCaseCount);
            File.Move(publishedCoverage, Path.Combine(root.RootPath, $"session-coverage-{runIndex}.json"));
            AssertCoverageDiagnostics(logDirectory, testhostProcessId, expectedCaseCount);
            if (useDotnetTest)
            {
                AssertOuterCommandReconciliation(logDirectory);
            }
        }
    }

    private ProcessResult RunRunner(string dotnetExecutable, string[] arguments, string logDirectory)
    {
        var runnerAssembly = typeof(Program).Assembly.Location;
        File.Exists(runnerAssembly).Should().BeTrue();
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

    private void AssertSdk10()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                WorkingDirectory = EnvironmentTools.GetSolutionDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start().Should().BeTrue();
        if (!process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("The .NET SDK version preflight exceeded 30 seconds.");
        }

        var versionText = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        process.ExitCode.Should().Be(0, error);
        Version.TryParse(versionText, out var version).Should().BeTrue($"'{versionText}' must be a .NET SDK version");
        version!.Major.Should().Be(10, "this smoke test must exercise the .NET SDK 10 TestCommand integration");
    }

    private string[] CreateDotnetTestCommand(EnvironmentHelper environmentHelper, bool useTestingPlatformCoverage)
    {
        var projectPath = Path.Combine(EnvironmentTools.GetSolutionDirectory(), SampleProjectRelativePath);
        System.Collections.Generic.List<string> command =
        [
            environmentHelper.GetDotnetExe(),
            "test",
            projectPath,
            "--no-build",
            "--configuration",
            EnvironmentTools.GetBuildConfiguration(),
            "--framework",
            "net8.0",
            "-p:ApiVersion=6.0.0"
        ];

        if (useTestingPlatformCoverage)
        {
            command.Add("-p:TestingPlatformCommandLineArguments=--coverage");
        }

        return command.ToArray();
    }

    private string[] CreateVstestCommand(
        string dotnetExecutable,
        string sampleAssembly,
        bool includeCoverlet)
    {
        var command = new System.Collections.Generic.List<string>
        {
            dotnetExecutable,
            "vstest",
            sampleAssembly,
        };

        if (includeCoverlet)
        {
            command.Add("/Collect:XPlat Code Coverage;IncludeTestAssembly=true");
        }

        return command.ToArray();
    }

    private string[] CreateCiRunArguments(
        string monitoringHome,
        int agentPort,
        string coverageDirectory,
        string logDirectory,
        string progressPath,
        int expectedCaseCount,
        string[] targetCommand)
    {
        var arguments = new System.Collections.Generic.List<string>
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

    private void AssertLaunch(string output, bool useDotnetTest, bool useTestingPlatformCoverage)
    {
        var launchLine = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                               .Should()
                               .ContainSingle(static line => line.StartsWith("Running:", StringComparison.Ordinal))
                               .Subject;
        var datadogCollectorCount = Regex.Matches(launchLine, @"(?<!\w)(?:/Collect:)?DatadogCoverage(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
        if (useTestingPlatformCoverage)
        {
            datadogCollectorCount.Should().Be(0, "Microsoft Testing Platform coverage must not load the Datadog coverage collector");
            launchLine.Should().Contain("-p:TestingPlatformCommandLineArguments=--coverage");
        }
        else
        {
            datadogCollectorCount.Should().Be(1, "dd-trace ci run must inject exactly one Datadog coverage collector");
        }

        if (useDotnetTest && !useTestingPlatformCoverage)
        {
            Regex.Matches(launchLine, @"(?<!\w)--test-adapter-path(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.Should().Be(1);
            launchLine.Should().Contain(AppContext.BaseDirectory);
        }
        else if (!useDotnetTest)
        {
            Regex.Matches(launchLine, Regex.Escape($"/TestAdapterPath:{AppContext.BaseDirectory}"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.Should().Be(1);
        }
    }

    private int AssertProgress(string progressPath, int expectedCaseCount)
    {
        File.Exists(progressPath).Should().BeTrue();
        var records = File.ReadAllLines(progressPath)
                          .Where(static line => !string.IsNullOrWhiteSpace(line))
                          .Select(static line => JsonConvert.DeserializeObject<ProgressRecord>(line))
                          .ToArray();

        records.Should().NotBeEmpty();
        records.Should().OnlyContain(record => record != null && record.Pid > 0 && record.PrivateBytes > 0 && record.ManagedBytes > 0);
        records.Select(static record => record!.Pid).Distinct().Should().ContainSingle("all test cases must run in one testhost process");
        records[^1]!.Completed.Should().Be(expectedCaseCount);

        if (expectedCaseCount > 1)
        {
            var initialPrivateBytes = records[0]!.PrivateBytes;
            var maximumPrivateBytes = records.Max(static record => record!.PrivateBytes);
            (maximumPrivateBytes - initialPrivateBytes).Should()
                                                       .BeLessThan(
                                                            MaximumStressPrivateBytesGrowth,
                                                            "completed test contexts must not retain their 128 KiB native coverage buffers");
        }

        return records[0]!.Pid;
    }

    private string AssertPublishedCoverage(string coverageDirectory, int expectedCaseCount)
    {
        var sessionCoverage = Directory.GetFiles(coverageDirectory, "session-coverage-*.json", SearchOption.TopDirectoryOnly);
        sessionCoverage.Should().ContainSingle();
        var reader = new GlobalCoverageInputReader();
        reader.TryRead(sessionCoverage[0], out var coverage).Should().BeTrue();
        coverage.Should().NotBeNull();
        coverage!.GetTotalPercentage().Should().BeGreaterThan(0);
        var sampleFile = coverage.Components.SelectMany(static component => component.Files)
                                 .Should()
                                 .ContainSingle(file => string.Equals(Path.GetFileName(file.Path), SampleSourceFileName, StringComparison.OrdinalIgnoreCase))
                                 .Subject;
        AssertLine(sampleFile.ExecutableBitmap, CommonCoverageLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, CommonCoverageLine, expected: true);
        AssertLine(sampleFile.ExecutableBitmap, FirstCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, FirstCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutableBitmap, MiddleCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, MiddleCoverageSentinelLine, expected: expectedCaseCount > 1);
        AssertLine(sampleFile.ExecutableBitmap, LastCoverageSentinelLine, expected: true);
        AssertLine(sampleFile.ExecutedBitmap, LastCoverageSentinelLine, expected: expectedCaseCount > 1);

        Directory.GetFiles(coverageDirectory, "coverage-*.json", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-process-incomplete-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-process-ready-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-command-owner-*.claim", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        var completedDirectory = Path.Combine(coverageDirectory, ".dd-coverage-completed");
        Directory.Exists(completedDirectory).Should().BeTrue();
        Directory.GetFiles(completedDirectory, "coverage-*.json", SearchOption.AllDirectories).Should().NotBeEmpty();

        return sessionCoverage[0];
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

    private void AssertOuterCommandReconciliation(string logDirectory)
    {
        var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories);
        logFiles.Should().NotBeEmpty();
        ContainsLine("Global coverage command DotnetTestCommand acquired reconciliation role ReconciliationOwner.").Should()
                                                                                                            .BeTrue("the .NET SDK 10 outer TestCommand hook must own reconciliation");
        ContainsLine("Global coverage reconciliation completed by DotnetTestCommand.").Should()
                                                                                       .BeTrue("the outer TestCommand hook must complete publication");

        bool ContainsLine(string text)
            => logFiles.Any(file => File.ReadLines(file).Any(line => line.Contains(text, StringComparison.Ordinal)));
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

        static Match FindLastMatch(string[] lines, string pattern)
        {
            var match = lines.Select(line => Regex.Match(line, pattern, RegexOptions.CultureInvariant))
                             .LastOrDefault(static candidate => candidate.Success);
            match.Should().NotBeNull("the sealed testhost must emit global coverage lifecycle diagnostics");
            return match!;
        }

        static long Parse(Match match, int group)
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
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}

#endif
