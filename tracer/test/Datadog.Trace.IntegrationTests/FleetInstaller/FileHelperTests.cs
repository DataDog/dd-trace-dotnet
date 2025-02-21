// <copyright file="FileHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System.IO;
using Datadog.FleetInstaller;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

[Trait("RunOnWindows", "True")]
[Trait("IIS", "True")]
[Trait("FleetInstaller", "True")]
public class FileHelperTests(ITestOutputHelper output)
{
    private readonly FleetInstallerLogger _log = new(output);

    [SkippableFact]
    public void CreateLogDirectory_WhenDirectoryDoesntExist_CreatesDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.Exists(tempDirectory).Should().BeFalse();

        FileHelper.CreateLogDirectory(_log, tempDirectory).Should().BeTrue();
        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    [SkippableFact]
    public void CreateLogDirectory_WhenManyParentDirectoryDoesntExist_CreatesDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Path.GetRandomFileName(), Path.GetRandomFileName());
        Directory.Exists(tempDirectory).Should().BeFalse();

        FileHelper.CreateLogDirectory(_log, tempDirectory).Should().BeTrue();
        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    [SkippableFact]
    public void CreateLogDirectory_WhenDirectoryExists_ReturnsTrue()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Path.GetRandomFileName(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        Directory.Exists(tempDirectory).Should().BeTrue();

        FileHelper.CreateLogDirectory(_log, tempDirectory).Should().BeTrue();
        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    [SkippableFact]
    public void CreateLogDirectory_WhenInvalidPath_ReturnsFalse()
    {
        var tempDirectory = Path.Combine("X:", "-1");
        FileHelper.CreateLogDirectory(_log, tempDirectory).Should().BeFalse();
    }
}
#endif
