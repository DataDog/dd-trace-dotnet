// <copyright file="FrameworkDescriptionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using static Datadog.Trace.TestHelpers.SkipOn.PlatformValue;

namespace Datadog.Trace.Tests;

public class FrameworkDescriptionTests
{
#if NETFRAMEWORK
    [SkippableFact]
    public void Windows_GivesExpectedValues()
    {
        // We don't support Mono
        SkipOn.Platform(MacOs);
        SkipOn.Platform(Linux);

        var description = FrameworkDescription.Create();
        description.OSPlatform.Should().Be("Windows");
        description.OSArchitecture.Should().Be("x64"); // We only test on 64 bit OS atm
        description.OSDescription.Should().StartWith("Microsoft Windows NT 10.0."); // Windows 10/11
    }
#else
    [SkippableFact]
    public void Windows_GivesExpectedValues()
    {
        SkipOn.Platform(MacOs);
        SkipOn.Platform(Linux);

        var description = FrameworkDescription.Create();
        description.OSPlatform.Should().Be("Windows");
        description.OSArchitecture.Should().Be("x64"); // We only test on 64 bit OS atm
        description.OSDescription.Should().StartWith("Microsoft Windows 10.0."); // Windows 10/11, note that this is _slightly_ different to the .NET FX version annoyingly
    }

    [SkippableFact]
    public void Linux_GivesExpectedValues()
    {
        SkipOn.Platform(Windows);
        SkipOn.Platform(MacOs);

        var expectedOs = (EnvironmentHelper.IsAlpine(), RuntimeInformation.OSArchitecture) switch
        {
            (true, Architecture.Arm64) => "Alpine Linux v3.18",
            (true, Architecture.X64) => "Alpine Linux v3.14",
            _ => "Debian GNU/Linux 10 (buster)",
        };

        var description = FrameworkDescription.Create();
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";

        description.OSPlatform.Should().Be("Linux");
        description.OSArchitecture.Should().Be(arch);
        description.OSDescription.Should().StartWith(expectedOs);
    }
#endif
}
