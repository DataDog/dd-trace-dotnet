// <copyright file="CreatedumpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests;

public class CreatedumpTests : ConsoleTestHelper
{
    public CreatedumpTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [SkippableFact]
    public async Task WriteCrashReport()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var helper = await StartConsoleWithArgs(
                               "crash-datadog",
                               ("LD_PRELOAD", Utils.GetApiWrapperPath()),
                               ("DD_TRACE_CRASH_HANDLER", Utils.GetDdDotnetPath()));

        await helper.Task;

        helper.StandardOutput.Should().Contain("The crash might have been caused by automatic instrumentation");
    }

    [SkippableFact]
    public async Task IgnoreNonDatadogCrashes()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        using var helper = await StartConsoleWithArgs(
                               "crash",
                               ("LD_PRELOAD", Utils.GetApiWrapperPath()),
                               ("DD_TRACE_CRASH_HANDLER", Utils.GetDdDotnetPath()));

        await helper.Task;

        helper.StandardOutput.Should().NotContain("The crash might have been caused by automatic instrumentation")
              .And.EndWith("Args: crash\n");
    }
}
