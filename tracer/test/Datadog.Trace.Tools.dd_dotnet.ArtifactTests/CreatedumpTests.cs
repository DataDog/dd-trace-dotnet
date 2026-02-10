// <copyright file="CreatedumpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
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
    private const string CrashReportUnfilteredExpectedOutput = "The crash is not suspicious, but filtering has been disabled";

    public CreatedumpTests(ITestOutputHelper output)
        : base(output)
    {
        // Those environment variables can be set by Nuke, and will impact the outcome of the tests
        SetEnvironmentVariable("COMPlus_DbgMiniDumpType", string.Empty);
        SetEnvironmentVariable("COMPlus_DbgEnableMiniDump", string.Empty);
        SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", string.Empty);

        // Don't let the EnvironmentHelper override our environment variables
        EnvironmentHelper.UseCrashTracking = false;
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

        assertionScope.AddReportable("Report", reportFile.GetContent());
        var report = JObject.Parse(reportFile.GetContent());
        report["error"]["is_crash"].Value<bool>().Should().Be(true);

        var metadataTags = (JArray)(report["metadata"]!["tags"]);

        var exception = metadataTags
                       .Select(t => t.Value<string>())
                       .FirstOrDefault(t => t.StartsWith("exception:"));

        exception.Should().NotBeNull().And.StartWith("exception:Type: System.BadImageFormatException\nMessage: Expected\nStack Trace:\n");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            report["sig_info"]!["si_signo"]!.Value<string>().Should().Be("6");
        }

        report["error"]!["message"].Value<string>().Should().Be("Process was terminated due to an unhandled exception of type 'System.BadImageFormatException'. Message: Expected.");
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
        assertionScope.AddReportable("Report", reportFile.GetContent());
        var report = JObject.Parse(reportFile.GetContent());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            report["error"]!["message"].Value<string>().Should().Be("Process was terminated with SIGSEGV (SEGV_MAPERR)");
        }
        else
        {
            report["error"]!["message"].Value<string>().Should().Be("OOpps");
        }
    }

    [SkippableFact]
    public async Task ResumeProcessWhenCrashing()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-native",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile), ("DD_INTERNAL_CRASHTRACKING_CRASH", "1")]);

        var completion = await Task.WhenAny(helper.Task, Task.Delay(TimeSpan.FromMinutes(1)));

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        Assert.Equal(completion, helper.Task);
    }

    [SkippableFact]
    public async Task CheckThreadName()
    {
        // Test that threads prefixed with DD_ are marked as suspicious even if they have nothing of Datadog in the stacktrace

        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-thread-datadog",
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
            return report["error"]["threads"] != null;
        }

        var agent = helper.Agent;

        (await agent.WaitForLatestTelemetryAsync(IsCrashReport)).Should().NotBeNull();
    }

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OptionallyDoNotReportNonDatadogCrashes(bool mainThread)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        var arg = mainThread ? "crash" : "crash-thread";

        using var helper = await StartConsoleWithArgs(
                               arg,
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile), ("DD_CRASHTRACKING_FILTERING_ENABLED", "1")]);

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
    public async Task ReportNonDatadogCrashes()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash",
                               enableProfiler: true,
                               [LdPreloadConfig, CrashReportConfig(reportFile), ("DD_CRASHTRACKING_FILTERING_ENABLED", "0")]);

        await helper.Task;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            helper.StandardOutput.Should().Contain(CrashReportUnfilteredExpectedOutput);
        }

        File.Exists(reportFile.Path).Should().BeTrue();
    }

    [SkippableFact]
    public async Task MakeSureNonDatadogCrashesAreReportedByDefault()
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
            helper.StandardOutput.Should().Contain(CrashReportUnfilteredExpectedOutput);
        }

        File.Exists(reportFile.Path).Should().BeTrue();
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

        ValidateStacktrace(report["error"]["stack"]);

        void ValidateStacktrace(JToken callstack)
        {
            callstack["frames"].Should().BeOfType<JArray>();

            var frames = (JArray)callstack["frames"];

            using var assertionScope = new AssertionScope();

            try
            {
                assertionScope.AddReportable("Frames", string.Join(Environment.NewLine, frames.Select(f => f["function"].Value<string>())));
            }
            catch (Exception e)
            {
                assertionScope.AddReportable("Frames", e.ToString());
            }

            foreach (var expectedFrame in expectedCallstack)
            {
                var frame = frames.FirstOrDefault(f => expectedFrame.Equals(f["function"].Value<string>()));

                frame.Should().NotBeNull($"couldn't find expected frame {expectedFrame}");
            }

            var validatedModules = new HashSet<string>();

            foreach (var frame in frames)
            {
                string moduleName = null;

                var path = frame["path"];
                // we do not set the path for managed assemblies.
                if (path != null)
                {
                    moduleName = path.Value<string>();
                }

                if (!string.IsNullOrEmpty(moduleName) && !moduleName.StartsWith("<") && Path.IsPathRooted(moduleName))
                {
                    if (!validatedModules.Add(moduleName))
                    {
                        continue;
                    }

                    var expectedBuildId = string.Empty;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // Open the PE file
                        using var file = File.OpenRead(moduleName);
                        using var peReader = new PEReader(file);

                        var debugDirectoryEntries = peReader.ReadDebugDirectory();
                        var codeViewEntry = debugDirectoryEntries.Single(e => e.Type == DebugDirectoryEntryType.CodeView);
                        var pdbInfo = peReader.ReadCodeViewDebugDirectoryData(codeViewEntry);

                        frame["build_id_type"].Value<string>().Should().Be("PDB");
                        expectedBuildId = $"{pdbInfo.Guid.ToString("N")}{unchecked((uint)pdbInfo.Age)}".ToLower();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        if (frame["build_id"] == null)
                        {
                            // On linux we can face cases where the build_id is not available:
                            // - specifically on alpine, /lib/ld-musl-XX do not have a build_id.
                            // - We are looking at a frame for which the library was unloaded /memfd:doublemapper (deleted)
                            continue;
                        }

                        frame["build_id_type"].Value<string>().Should().Be("GNU"); // or SHA1??

                        using var elf = ELFReader.Load(moduleName);
                        var buildIdNote = elf.GetSection(".note.gnu.build-id") as INoteSection;
                        expectedBuildId = ToHexString(buildIdNote.Description).ToLower();
                    }

                    frame["build_id"].Value<string>().Should().BeEquivalentTo(expectedBuildId);
                }
            }

            validatedModules.Should().NotBeEmpty();

#if NETFRAMEWORK
            var clrModuleName = "clr.dll";
#else
            var clrModuleName = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libcoreclr.so" : "coreclr.dll";
#endif

            if (!Utils.IsAlpine())
            {
                validatedModules.Should().ContainMatch($@"*{Path.DirectorySeparatorChar}{clrModuleName}");
            }
        }

        string ToHexString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }

            return hex.ToString();
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
