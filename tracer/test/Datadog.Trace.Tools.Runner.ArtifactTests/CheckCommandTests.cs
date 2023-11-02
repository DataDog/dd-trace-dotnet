// <copyright file="CheckCommandTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.ArtifactTests;

public class CheckCommandTests : RunnerTests
{
    [SkippableFact]
    public void Help()
    {
        using var helper = StartProcess("check");

        helper.Process.WaitForExit();
        helper.Drain();

        helper.Process.ExitCode.Should().Be(1);
        helper.StandardOutput.Should().Contain("dd-trace check [command] [options]");
        helper.ErrorOutput.Should().Contain("Required command was not provided.");
    }

    [SkippableFact]
    public void MultipleArguments()
    {
        // Currently we can only test this one Windows.
        // The only command that accepts a space is "check iis" and it's not available on Linux.
        SkipOn.Platform(SkipOn.PlatformValue.Linux);
        using var helper = StartProcess("check iis \"hello world\"");

        helper.Process.WaitForExit();
        helper.Drain();

        helper.Process.ExitCode.Should().Be(1);
        helper.StandardOutput.Should().Contain("Could not find IIS application \"hello world/\".");
        helper.ErrorOutput.Should().NotContain("Unrecognized command or argument");
    }
}
