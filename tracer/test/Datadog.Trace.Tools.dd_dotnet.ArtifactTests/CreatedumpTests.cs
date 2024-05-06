// <copyright file="CreatedumpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests;

public class CreatedumpTests : ConsoleTestHelper
{
    private const string CreatedumpExpectedOutput = "Writing minidump";
    private const string CrashReportExpectedOutput = "The crash may have been caused by automatic instrumentation";

    public CreatedumpTests(ITestOutputHelper output)
        : base(output)
    {
        // Those environment variables can be set by Nuke, and will impact the outcome of the tests
        SetEnvironmentVariable("COMPlus_DbgMiniDumpType", string.Empty);
        SetEnvironmentVariable("COMPlus_DbgEnableMiniDump", string.Empty);
        SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", string.Empty);
    }

    private static (string Key, string Value) LdPreloadConfig
    {
        get
        {
            var path = Utils.GetApiWrapperPath();

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"LD wrapper not found at path {path}. Ensure you have built the profiler home directory using BuildProfilerHome");
            }

            return ("LD_PRELOAD", path);
        }
    }

    private static (string Key, string Value)[] CreatedumpConfig => [("COMPlus_DbgEnableMiniDump", "1"), ("COMPlus_DbgMiniDumpName", "/dev/null")];

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
        // DD_TRACE_CRASH_HANDLER_PASSTHROUGH environment variable, which codifies the result of the
        // "was COMPlus_DbgEnableMiniDump set?" check.

        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        (string, string)[] args = [LdPreloadConfig, ..CreatedumpConfig, ("DD_TRACE_CRASH_HANDLER_PASSTHROUGH", passthrough), ..CrashReportConfig(reportFile)];

        using var helper = await StartConsoleWithArgs("crash-datadog", args);

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

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DoNothingIfNotEnabled(bool enableCrashDumps)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        (string, string)[] args = [LdPreloadConfig];

        if (enableCrashDumps)
        {
            args = [..args, ..CreatedumpConfig];
        }

        using var helper = await StartConsoleWithArgs("crash-datadog", args);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        helper.StandardOutput.Should().NotContain(CrashReportExpectedOutput);

        if (enableCrashDumps)
        {
            helper.StandardOutput.Should().Contain(CreatedumpExpectedOutput);
        }
        else
        {
            helper.StandardOutput.Should().NotContain(CreatedumpExpectedOutput);
        }

        File.Exists(reportFile.Path).Should().BeFalse();
    }

    [SkippableTheory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task DisableTelemetry(bool telemetryEnabled, bool crashdumpEnabled)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        (string, string)[] args = [LdPreloadConfig, ..CrashReportConfig(reportFile)];

        if (crashdumpEnabled)
        {
            args = [..args, ..CreatedumpConfig];
        }

        args = [..args, ("DD_INSTRUMENTATION_TELEMETRY_ENABLED", telemetryEnabled ? "1" : "0")];

        using var helper = await StartConsoleWithArgs("crash-datadog", args);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        if (crashdumpEnabled)
        {
            helper.StandardOutput.Should().Contain(CreatedumpExpectedOutput);
        }
        else
        {
            helper.StandardOutput.Should().NotContain(CreatedumpExpectedOutput);
        }

        if (telemetryEnabled)
        {
            helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);
            File.Exists(reportFile.Path).Should().BeTrue();
        }
        else
        {
            helper.StandardOutput.Should().NotContain(CrashReportExpectedOutput);
            File.Exists(reportFile.Path).Should().BeFalse();
        }
    }

    [SkippableFact]
    public async Task WriteCrashReport()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-datadog",
                               [LdPreloadConfig, ..CrashReportConfig(reportFile)]);

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        helper.StandardOutput.Should().Contain(CrashReportExpectedOutput);

        File.Exists(reportFile.Path).Should().BeTrue();

        var report = JObject.Parse(reportFile.GetContent());

        var metadataTags = (JArray)report["metadata"]!["tags"];

        var exception = metadataTags
                       .Select(t => t.Value<string>())
                       .FirstOrDefault(t => t.StartsWith("exception:"));

        exception.Should().NotBeNull().And.StartWith("exception:Type: System.BadImageFormatException\nMessage: Expected\nStack Trace:\n");
        report["siginfo"]!["signum"]!.Value<string>().Should().Be("6");
    }

    [SkippableFact]
    public async Task IgnoreNonDatadogCrashes()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash",
                               [LdPreloadConfig, ..CrashReportConfig(reportFile)]);

        await helper.Task;

        helper.StandardOutput.Should()
              .NotContain(CrashReportExpectedOutput)
              .And.EndWith("Crashing...\n"); // Making sure there is no additional output

        File.Exists(reportFile.Path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task ReportedStacktrace()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        using var helper = await StartConsoleWithArgs(
                               "crash-datadog",
                               [LdPreloadConfig, ..CrashReportConfig(reportFile)]);

        await helper.Task;

        File.Exists(reportFile.Path).Should().BeTrue();

        var reader = new StringReader(helper.StandardOutput);

        int? expectedPid = null;
        var expectedCallstack = new List<string>();

        var pidRegex = "PID: (?<pid>[0-9]+)";

        while (reader.ReadLine() is { } line)
        {
            var pidMatch = Regex.Match(line, pidRegex);

            if (pidMatch.Success)
            {
                expectedPid = int.Parse(pidMatch.Groups["pid"].Value);
                continue;
            }

            if (line.StartsWith("Frame|"))
            {
                expectedCallstack.Add(line.Split('|')[1]);
            }
        }

        expectedPid.Should().NotBeNull();
        expectedCallstack.Should().HaveCountGreaterOrEqualTo(2);

        var report = JObject.Parse(reportFile.GetContent());

        ValidateStacktrace(report["stacktrace"]);
        ValidateStacktrace(report["additional_stacktraces"][expectedPid.Value.ToString()]);

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

    private static (string Key, string Value)[] CrashReportConfig(TemporaryFile reportFile)
    {
        var ddDotnetPath = Utils.GetDdDotnetPath();

        if (!File.Exists(ddDotnetPath))
        {
            throw new FileNotFoundException($"dd-dotnet not found at path {ddDotnetPath}");
        }

        return
        [
            ("DD_TRACE_CRASH_HANDLER", ddDotnetPath),
            ("DD_TRACE_CRASH_OUTPUT", reportFile.Url)
        ];
    }

    private class TemporaryFile : IDisposable
    {
        public TemporaryFile()
        {
            Path = System.IO.Path.GetTempFileName();
            File.Delete(Path);
        }

        public string Path { get; }

        public string Url => $"file:/{Path}";

        public string GetContent() => File.ReadAllText(Path);

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}

#endif
