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
using Datadog.Trace.Ci;
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
        var previousTimeout = RunCiCommand.ProcessTimeoutForTests;
        var previousObserver = RunCiCommand.ProcessStartObserverForTests;
        string[]? launchedArguments = null;
        System.Collections.Generic.Dictionary<string, string?>? launchedEnvironment = null;
        Program.CallbackForTests = null;
        RunCiCommand.ProcessTimeoutForTests = TimeSpan.FromMinutes(20);
        RunCiCommand.ProcessStartObserverForTests = processInfo =>
        {
            launchedArguments = processInfo.ArgumentList.ToArray();
            launchedEnvironment = processInfo.Environment.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        };

        try
        {
            for (var runIndex = 0; runIndex < runCount; runIndex++)
            {
                TestOptimization.Instance.Reset();
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

                launchedArguments = null;
                launchedEnvironment = null;
                var exitCode = Program.Main(arguments);
                exitCode.Should().Be(0);

                AssertLaunch(launchedArguments, launchedEnvironment, useDotnetTest, useTestingPlatformCoverage, coverageDirectory);
                var testhostProcessId = AssertProgress(progressPath, expectedCaseCount);
                var publishedCoverage = AssertPublishedCoverage(coverageDirectory);
                File.Move(publishedCoverage, Path.Combine(root.RootPath, $"session-coverage-{runIndex}.json"));
                AssertCoverageDiagnostics(logDirectory, testhostProcessId, expectedCaseCount);
                if (useDotnetTest)
                {
                    AssertOuterCommandReconciliation(logDirectory);
                }
            }
        }
        finally
        {
            RunCiCommand.ProcessTimeoutForTests = previousTimeout;
            RunCiCommand.ProcessStartObserverForTests = previousObserver;
            Program.CallbackForTests = null;
            TestOptimization.Instance.Reset();
        }
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

    private void AssertLaunch(
        string[]? launchedArguments,
        System.Collections.Generic.IReadOnlyDictionary<string, string?>? launchedEnvironment,
        bool useDotnetTest,
        bool useTestingPlatformCoverage,
        string coverageDirectory)
    {
        launchedArguments.Should().NotBeNull();
        launchedEnvironment.Should().NotBeNull();
        var arguments = launchedArguments!;
        var environment = launchedEnvironment!;
        var datadogCollectorCount = arguments.Count(
            static argument => string.Equals(argument, "DatadogCoverage", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(argument, "/Collect:DatadogCoverage", StringComparison.OrdinalIgnoreCase));
        if (useTestingPlatformCoverage)
        {
            datadogCollectorCount.Should().Be(0, "Microsoft Testing Platform coverage must not load the Datadog coverage collector");
            arguments.Should().Contain("-p:TestingPlatformCommandLineArguments=--coverage");
        }
        else
        {
            datadogCollectorCount.Should().Be(1, "dd-trace ci run must inject exactly one Datadog coverage collector");
        }

        if (useDotnetTest && !useTestingPlatformCoverage)
        {
            arguments.Count(static argument => string.Equals(argument, "--test-adapter-path", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
            arguments.Should().Contain(AppContext.BaseDirectory);
        }
        else if (!useDotnetTest)
        {
            arguments.Count(argument => string.Equals(argument, $"/TestAdapterPath:{AppContext.BaseDirectory}", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        }

        environment[ConfigurationKeys.CIVisibility.CodeCoveragePath].Should().Be(coverageDirectory);
        environment.Should().ContainKey(ConfigurationKeys.CIVisibility.TestOptimizationRunId);
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

    private string AssertPublishedCoverage(string coverageDirectory)
    {
        var sessionCoverage = Directory.GetFiles(coverageDirectory, "session-coverage-*.json", SearchOption.TopDirectoryOnly);
        sessionCoverage.Should().ContainSingle();
        new GlobalCoverageInputReader().TryRead(sessionCoverage[0], out var coverage).Should().BeTrue();
        coverage.Should().NotBeNull();
        coverage!.GetTotalPercentage().Should().BeGreaterThan(0);

        Directory.GetFiles(coverageDirectory, "coverage-*.json", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-process-incomplete-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-process-ready-*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        Directory.GetFiles(coverageDirectory, ".dd-coverage-command-owner-*.claim", SearchOption.TopDirectoryOnly).Should().BeEmpty();
        var completedDirectory = Path.Combine(coverageDirectory, ".dd-coverage-completed");
        Directory.Exists(completedDirectory).Should().BeTrue();
        Directory.GetFiles(completedDirectory, "coverage-*.json", SearchOption.AllDirectories).Should().NotBeEmpty();

        return sessionCoverage[0];
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

    private sealed class ProgressRecord
    {
        public int Pid { get; set; }

        public int Completed { get; set; }

        public long PrivateBytes { get; set; }

        public long ManagedBytes { get; set; }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory(string prefix)
        {
            RootPath = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        internal string RootPath { get; }

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
