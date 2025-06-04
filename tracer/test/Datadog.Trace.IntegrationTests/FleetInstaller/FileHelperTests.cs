// <copyright file="FileHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.FleetInstaller;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

[Trait("RunOnWindows", "True")]
[Trait("IIS", "True")] // these are to stop the tests being run in other stages
[Trait("MSI", "True")] // these are to stop the tests being run in other stages
[Trait("FleetInstaller", "True")]
public class FileHelperTests(ITestOutputHelper output) : FleetInstallerTestsBase(output)
{
    [SkippableFact]
    public void CreateLogDirectory_WhenDirectoryDoesntExist_CreatesDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.Exists(tempDirectory).Should().BeFalse();

        FileHelper.CreateLogDirectory(Log, tempDirectory).Should().BeTrue();
        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    [SkippableFact]
    public void CreateLogDirectory_WhenManyParentDirectoryDoesntExist_CreatesDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Path.GetRandomFileName(), Path.GetRandomFileName());
        Directory.Exists(tempDirectory).Should().BeFalse();

        FileHelper.CreateLogDirectory(Log, tempDirectory).Should().BeTrue();
        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    [SkippableFact]
    public void CreateLogDirectory_WhenDirectoryExists_ReturnsTrue()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Path.GetRandomFileName(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        Directory.Exists(tempDirectory).Should().BeTrue();

        FileHelper.CreateLogDirectory(Log, tempDirectory).Should().BeTrue();
        Directory.Exists(tempDirectory).Should().BeTrue();
    }

    [SkippableFact]
    public void CreateLogDirectory_WhenInvalidPath_ReturnsFalse()
    {
        var tempDirectory = Path.Combine("X:", "-1");
        FileHelper.CreateLogDirectory(Log, tempDirectory).Should().BeFalse();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenFilesExist_ReturnsTrue()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        FileHelper.TryVerifyFilesExist(Log, values, out var error)
                  .Should().BeTrue();
        error.Should().BeNull();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenDirectoryDoesntExist_ReturnsFalse()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.Exists(homeDirectory).Should().BeFalse();

        var values = new TracerValues(homeDirectory);
        FileHelper.TryVerifyFilesExist(Log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenX64NativeLoaderDoesNotExist_ReturnsFalse()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        File.Delete(values.NativeLoaderX64Path);

        FileHelper.TryVerifyFilesExist(Log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenX86NativeLoaderDoesNotExist_ReturnsFalse()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        File.Delete(values.NativeLoaderX86Path);

        FileHelper.TryVerifyFilesExist(Log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [SkippableTheory]
    [MemberData(nameof(Data.IndexesOfFilesToGac), MemberType = typeof(Data))]
    public void TryVerifyFilesExist_WhenFileToGacDoesNotExist_ReturnsFalse(int index)
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        File.Delete(values.FilesToAddToGac.Skip(index).First());

        FileHelper.TryVerifyFilesExist(Log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [SkippableFact]
    public void TryDeleteNativeLoaders_WhenFilesDoNotExist_ReturnsTrue()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(homeDirectory);

        var values = new TracerValues(homeDirectory);
        FileHelper.TryDeleteNativeLoaders(Log, values)
                  .Should()
                  .BeTrue();
    }

    [SkippableFact]
    public void TryDeleteNativeLoaders_WhenRootFolderDoesNotExist_ReturnsTrue()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.Exists(homeDirectory).Should().BeFalse();

        var values = new TracerValues(homeDirectory);
        FileHelper.TryDeleteNativeLoaders(Log, values)
                  .Should()
                  .BeTrue();
    }

    [SkippableFact]
    public void TryDeleteNativeLoaders_WhenFilesExistAndUnused_DeletesFilesAndReturnsTrue()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        File.Exists(values.NativeLoaderX64Path).Should().BeTrue();
        File.Exists(values.NativeLoaderX86Path).Should().BeTrue();

        FileHelper.TryDeleteNativeLoaders(Log, values)
                  .Should()
                  .BeTrue();

        File.Exists(values.NativeLoaderX64Path).Should().BeFalse();
        File.Exists(values.NativeLoaderX86Path).Should().BeFalse();
    }

    [SkippableFact]
    public void TryDeleteNativeLoaders_WhenX64NativeLoaderIsInUse_DoesNotDeleteFileAndReturnsFalse()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        // locks the file
        var filePath = values.NativeLoaderX64Path;
        using var file = File.OpenRead(filePath);

        FileHelper.TryDeleteNativeLoaders(Log, values)
                  .Should()
                  .BeFalse();

        File.Exists(filePath).Should().BeTrue();
    }

    [SkippableFact]
    public void TryDeleteNativeLoaders_WhenX86NativeLoaderIsInUse_DoesNotDeleteFileAndReturnsFalse()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        // locks the file
        var filePath = values.NativeLoaderX86Path;
        using var file = File.OpenRead(filePath);

        FileHelper.TryDeleteNativeLoaders(Log, values)
                  .Should()
                  .BeFalse();

        File.Exists(filePath).Should().BeTrue();
    }

    public static class Data
    {
        public static IEnumerable<object[]> IndexesOfFilesToGac
            => Enumerable.Range(0, new TracerValues(Path.GetTempPath()).FilesToAddToGac.Count)
                         .Select(i => new object[] { i });
    }
}
#endif
