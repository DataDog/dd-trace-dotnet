// <copyright file="CreatedumpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests;

public class CreatedumpTests : ConsoleTestHelper
{
#if !NETFRAMEWORK // Createdump is not supported on .NET Framework
    private const string CreatedumpExpectedOutput = "Writing minidump with heap to file /dev/null";
#endif
    private const string CrashReportExpectedOutput = "The crash may have been caused by automatic instrumentation";

    public CreatedumpTests(ITestOutputHelper output)
        : base(output)
    {
        // Those environment variables can be set by Nuke, and will impact the outcome of the tests
        SetEnvironmentVariable("COMPlus_DbgMiniDumpType", string.Empty);
        SetEnvironmentVariable("COMPlus_DbgEnableMiniDump", string.Empty);
        SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", string.Empty);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetEnvironmentVariable("DD_CRASHTRACKING_ENABLED", "1");
        }
    }

    private static (string Key, string Value) LdPreloadConfig
    {
        get
        {
            var path = Utils.GetApiWrapperPath();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !File.Exists(path))
            {
                throw new FileNotFoundException($"LD wrapper not found at path {path}. Ensure you have built the profiler home directory using BuildProfilerHome");
            }

            return ("LD_PRELOAD", path);
        }
    }

    private static (string Key, string Value)[] CreatedumpConfig => [("COMPlus_DbgEnableMiniDump", "1"), ("COMPlus_DbgMiniDumpName", "/dev/null")];

#if !NETFRAMEWORK
    [SkippableTheory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public async Task Passthrough(string passthrough, bool shouldCallCreatedump)
    {
        // This tests the case when a dotnet exe calls another one
        // The expected behavior is that in the parent process, we check if COMPlus_DbgEnableMiniDump
        // was already set. If it was, then we need to forward the call to createdump in case of crash.
        // If it wasn't set, then we set it so that dd-dotnet will be invoked in case of crash.
        // If a child dotnet process is spawned, we may then mistakenly think that COMPlus_DbgEnableMiniDump
        // was set from the environment, even though it was set by us. To prevent that, we set the
        // DD_INTERNAL_CRASHTRACKING_PASSTHROUGH environment variable, which codifies the result of the
        // "was COMPlus_DbgEnableMiniDump set?" check.

        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Windows); // This test is not needed on Windows because we don't hook createdump

        using var reportFile = new TemporaryFile();

        (string, string)[] args = [LdPreloadConfig, .. CreatedumpConfig, ("DD_INTERNAL_CRASHTRACKING_PASSTHROUGH", passthrough), CrashReportConfig(reportFile)];

        using var helper = await StartConsoleWithArgs("crash-datadog", enableProfiler: true, args);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);

        File.Exists(reportFile.Path).Should().BeTrue();

        if (shouldCallCreatedump)
        {
            helper.StandardOutput.Should().Contain(CreatedumpExpectedOutput);
        }
        else
        {
            helper.StandardOutput.Should().NotContain(CreatedumpExpectedOutput);
        }
    }

    [SkippableFact]
    public async Task BashScript()
    {
        // This tests the case when an app is called through a bash script
        // This scenario has unique challenges because:
        //   - The COMPlus_DbgMiniDumpName environment variable that we override is then inherited by the child
        //   - Bash overrides the getenv/setenv functions, which cause some unexpected behaviors

        SkipOn.Platform(SkipOn.PlatformValue.Windows);
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        (string, string)[] environment = [LdPreloadConfig, .. CreatedumpConfig, CrashReportConfig(reportFile)];

        var (executable, args) = PrepareSampleApp(EnvironmentHelper);

        var bashScript = $"#!/bin/bash\n{executable} {args} crash-datadog\n";
        using var bashFile = new TemporaryFile();
        bashFile.SetContent(bashScript);

        using var helper = await StartConsole("/bin/bash", bashFile.Path, EnvironmentHelper, false, environment);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
        File.Exists(reportFile.Path).Should().BeTrue();

        helper.StandardOutput.Should().Contain(CreatedumpExpectedOutput);
    }
#endif

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DoNothingIfNotEnabled(bool enableCrashDumps)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        if (enableCrashDumps)
        {
            // Crashtracking is not supported on Windows x86
            SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);
        }

        using var reportFile = new TemporaryFile();

        (string, string)[] args = [LdPreloadConfig, CrashReportConfig(reportFile), ("DD_CRASHTRACKING_ENABLED", "0")];

        if (enableCrashDumps)
        {
            args = [.. args, .. CreatedumpConfig];
        }

        using var helper = await StartConsoleWithArgs("crash-datadog", enableProfiler: true, args);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should().NotContain(CrashReportExpectedOutput);
        }

#if !NETFRAMEWORK
        if (enableCrashDumps)
        {
            helper.StandardOutput.Should().Contain(CreatedumpExpectedOutput);
        }
        else
        {
            helper.StandardOutput.Should().NotContain(CreatedumpExpectedOutput);
        }
#endif

        File.Exists(reportFile.Path).Should().BeFalse();
    }

    [SkippableTheory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task DisableTelemetry(bool telemetryEnabled, bool crashdumpEnabled)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        (string, string)[] args = [LdPreloadConfig, CrashReportConfig(reportFile)];

        if (crashdumpEnabled)
        {
            args = [.. args, .. CreatedumpConfig];
        }

        args = [.. args, ("DD_INSTRUMENTATION_TELEMETRY_ENABLED", telemetryEnabled ? "1" : "0")];

        using var helper = await StartConsoleWithArgs("crash-datadog", enableProfiler: true, args);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

#if !NETFRAMEWORK
        if (crashdumpEnabled)
        {
            helper.StandardOutput.Should().Contain(CreatedumpExpectedOutput);
        }
        else
        {
            helper.StandardOutput.Should().NotContain(CreatedumpExpectedOutput);
        }
#endif

        if (telemetryEnabled)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
            }

            File.Exists(reportFile.Path).Should().BeTrue();
        }
        else
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                helper.StandardOutput.Should().NotContain(CrashReportExpectedOutput);
            }

            File.Exists(reportFile.Path).Should().BeFalse();
        }
    }

    [SkippableFact]
    public async Task WriteCrashReport()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-datadog",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile)]);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
        }

        File.Exists(reportFile.Path).Should().BeTrue();

        var report = JObject.Parse(reportFile.GetContent());

        var metadataTags = (JArray)report["metadata"]!["tags"];

        var exception = metadataTags
                       .Select(t => t.Value<string>())
                       .FirstOrDefault(t => t.StartsWith("exception:"));

        exception.Should().NotBeNull().And.StartWith("exception:Type: System.BadImageFormatException\nMessage: Expected\nStack Trace:\n");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            report["siginfo"]!["signum"]!.Value<string>().Should().Be("6");
        }
    }

#if !NETFRAMEWORK
    [SkippableTheory]
    [InlineData(".")]
    [InlineData("./continuousprofiler")]
    public async Task WorksFromDifferentFolders(string subFolder)
    {
        // Check that we're still able to locate dd-dotnet when LD_PRELOAD points to the root folder

        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        // Create a new home folder for the test
        var tempHomeFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            CopyDirectory(EnvironmentHelper.GetMonitoringHomePath(), tempHomeFolder);

            var apiWrapperPath = Utils.GetApiWrapperPath();
            var newApiWrapperPath = Path.Combine(tempHomeFolder, subFolder, Path.GetFileName(apiWrapperPath));

            Directory.CreateDirectory(Path.GetDirectoryName(newApiWrapperPath));
            File.Copy(apiWrapperPath, newApiWrapperPath);

            using var reportFile = new TemporaryFile();

            using var helper = await StartConsoleWithArgs(
                                   "crash-datadog",
                                   enableProfiler: true,
                                   [("LD_PRELOAD", newApiWrapperPath), CrashReportConfig(reportFile)]);

            await helper.Task;

            using var assertionScope = new AssertionScope();
            assertionScope.AddReportable("stdout", helper.StandardOutput);
            assertionScope.AddReportable("stderr", helper.ErrorOutput);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
            }

            File.Exists(reportFile.Path).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempHomeFolder, true);
        }
    }
#endif

    [SkippableFact]
    public async Task NativeCrash()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        if (Utils.IsAlpine())
        {
            throw new SkipException("Signal unwinding does not work correctly on Alpine");
        }

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-native",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile)]);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
        }

        File.Exists(reportFile.Path).Should().BeTrue();
    }

    [SkippableFact]
    public async Task CheckThreadName()
    {
        // Test that threads prefixed with DD_ are marked as suspicious even if they have nothing of Datadog in the stacktrace

        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-thread",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile)]);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
        }

        File.Exists(reportFile.Path).Should().BeTrue();
    }

    [SkippableFact]
    public async Task SendReportThroughTelemetry()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var helper = await StartConsoleWithArgs(
                               "crash-datadog",
                               enableProfiler: true,
                               [LdPreloadConfig]);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
        }

        bool IsCrashReport(object payload)
        {
            if (payload is not TelemetryData data)
            {
                return false;
            }

            if (data.TryGetPayload<LogsPayload>(TelemetryRequestTypes.RedactedErrorLogs) is not { } log)
            {
                return false;
            }

            if (log.Logs.Count != 1)
            {
                return false;
            }

            var report = JObject.Parse(log.Logs[0].Message);
            return report["additional_stacktraces"] != null;
        }

        var agent = helper.Agent;

        agent.WaitForLatestTelemetry(IsCrashReport).Should().NotBeNull();
    }

    [SkippableFact]
    public async Task IgnoreNonDatadogCrashes()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile)]);

        await helper.Task;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should()
              .NotContain(CrashReportExpectedOutput)
              .And.EndWith("Crashing...\n"); // Making sure there is no additional output
        }

        File.Exists(reportFile.Path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task ReportedStacktrace()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-datadog",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile)]);

        await helper.Task;

        File.Exists(reportFile.Path).Should().BeTrue();

        var reader = new StringReader(helper.StandardOutput);

        int? mainThreadId = null;
        var expectedCallstack = new List<string>();

        var tidRegex = "Main thread: (?<tid>[0-9]+)";

        while (reader.ReadLine() is { } line)
        {
            var tidMatch = Regex.Match(line, tidRegex);

            if (tidMatch.Success)
            {
                mainThreadId = int.Parse(tidMatch.Groups["tid"].Value);
                continue;
            }

            if (line.StartsWith("Frame|"))
            {
                expectedCallstack.Add(line.Split('|')[1]);
            }
        }

        mainThreadId.Should().NotBeNull();
        expectedCallstack.Should().HaveCountGreaterOrEqualTo(2);

        var report = JObject.Parse(reportFile.GetContent());

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("Report", report.ToString());

        ValidateStacktrace(report["stacktrace"]);
        ValidateStacktrace(report["additional_stacktraces"][mainThreadId.Value.ToString()]);

        void ValidateStacktrace(JToken callstack)
        {
            callstack.Should().BeOfType<JArray>();

            var frames = (JArray)callstack;

            using var assertionScope = new AssertionScope();

            try
            {
                assertionScope.AddReportable("Frames", string.Join(Environment.NewLine, frames.Select(f => f["names"][0]["name"].Value<string>())));
            }
            catch (Exception e)
            {
                assertionScope.AddReportable("Frames", e.ToString());
            }

            foreach (var expectedFrame in expectedCallstack)
            {
                var frame = frames.FirstOrDefault(f => expectedFrame.Equals(f["names"][0]["name"].Value<string>()));

                frame.Should().NotBeNull($"couldn't find expected frame {expectedFrame}");
            }
        }
    }

    private static (string Key, string Value) CrashReportConfig(TemporaryFile reportFile)
    {
        return ("DD_INTERNAL_CRASHTRACKING_OUTPUT", reportFile.Url);
    }

    private static void CopyDirectory(string source, string destination)
    {
        var dir = new DirectoryInfo(source);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {source}");
        }

        Directory.CreateDirectory(destination);

        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destination, file.Name));
        }

        foreach (var subdir in dir.GetDirectories())
        {
            CopyDirectory(subdir.FullName, Path.Combine(destination, subdir.Name));
        }
    }

    private class TemporaryFile : IDisposable
    {
        public TemporaryFile()
        {
            Path = System.IO.Path.GetTempFileName();
            File.Delete(Path);
        }

        public string Path { get; }

        public string Url => $"file://{Path}";

        public string GetContent() => File.ReadAllText(Path);

        public void SetContent(string content) => File.WriteAllText(Path, content);

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
