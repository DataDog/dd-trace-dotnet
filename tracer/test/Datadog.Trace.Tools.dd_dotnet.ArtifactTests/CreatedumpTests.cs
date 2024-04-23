// <copyright file="CreatedumpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
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
    private const string CrashReportExpectedOutput = "The crash might have been caused by automatic instrumentation";

    public CreatedumpTests(ITestOutputHelper output)
        : base(output)
    {
    }

    private static (string Key, string Value) LdPreloadConfig => ("LD_PRELOAD", Utils.GetApiWrapperPath());

    private static (string Key, string Value)[] CreatedumpConfig => [("COMPlus_DbgEnableMiniDump", "1"), ("COMPlus_DbgMiniDumpName", "/dev/null")];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreatedumpPassthrough(bool enableCrashDumps)
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

        helper.StandardOutput.Should().NotContain("The crash might have been caused by automatic instrumentation");

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
    [InlineData(true)]
    [InlineData(false)]
    public async Task DoNothingIfNotEnabled(bool enableCrashDumps)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var reportFile = new TemporaryFile();

        var args = new List<(string, string)> { LdPreloadConfig };

        if (enableCrashDumps)
        {
            args.Add(("COMPlus_DbgEnableMiniDump", "1"));
            args.Add(("COMPlus_DbgMiniDumpName", "/dev/null"));
        }

        using var helper = await StartConsoleWithArgs("crash-datadog", args.ToArray());

        await helper.Task;

        using var assertionScope = new AssertionScope();
        assertionScope.AddReportable("stdout", helper.StandardOutput);
        assertionScope.AddReportable("stderr", helper.ErrorOutput);

        helper.StandardOutput.Should().NotContain("The crash might have been caused by automatic instrumentation");

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

        report["tags"]!["exception"]!.Value<string>().Should().StartWith("Type: System.BadImageFormatException\nMessage: Expected\nStack Trace:\n");
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
              .And.EndWith("Args: crash\n"); // Making sure there is no additional output

        File.Exists(reportFile.Path).Should().BeFalse();
    }

    private static (string Key, string Value)[] CrashReportConfig(TemporaryFile reportFile)
    {
        return
        [
            ("DD_TRACE_CRASH_HANDLER", Utils.GetDdDotnetPath()),
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
