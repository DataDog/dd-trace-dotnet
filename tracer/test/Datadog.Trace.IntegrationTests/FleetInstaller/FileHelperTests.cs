// <copyright file="FileHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.FleetInstaller;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

[Trait("RunOnWindows", "True")]
[Trait("IIS", "True")] // these are to stop the tests being run in other stages
[Trait("MSI", "True")] // these are to stop the tests being run in other stages
[Trait("FleetInstaller", "True")]
public class FileHelperTests(ITestOutputHelper output) : IDisposable
{
    private readonly FleetInstallerLogger _log = new(output);
    private string _homeDirectory;

    public void Dispose()
    {
        if (_homeDirectory is { } dir)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignore, something is locking it
            }
        }
    }

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

    [SkippableFact]
    public void TryVerifyFilesExist_WhenFilesExist_ReturnsTrue()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        FileHelper.TryVerifyFilesExist(_log, values, out var error)
                  .Should().BeTrue();
        error.Should().BeNull();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenDirectoryDoesntExist_ReturnsFalse()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.Exists(homeDirectory).Should().BeFalse();

        var values = new TracerValues(homeDirectory);
        FileHelper.TryVerifyFilesExist(_log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenX64NativeLoaderDoesNotExist_ReturnsFalse()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        File.Delete(values.NativeLoaderX64Path);

        FileHelper.TryVerifyFilesExist(_log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [SkippableFact]
    public void TryVerifyFilesExist_WhenX86NativeLoaderDoesNotExist_ReturnsFalse()
    {
        var homeDirectory = CreateMonitoringHomeCopy();

        var values = new TracerValues(homeDirectory);
        File.Delete(values.NativeLoaderX86Path);

        FileHelper.TryVerifyFilesExist(_log, values, out var error)
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

        FileHelper.TryVerifyFilesExist(_log, values, out var error)
                  .Should().BeFalse();
        error.Should().NotBeNull();
    }

    private string CreateMonitoringHomeCopy()
    {
        if (_homeDirectory is not null)
        {
            return _homeDirectory;
        }

        // create a copy of monitoring home, so that we can mess with it
        var original = EnvironmentHelper.GetMonitoringHomePath();
        var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        output.WriteLine("Copying {original} to {homeDirectory}");

        CopyDirectory(original, homeDirectory);
        _homeDirectory = homeDirectory;
        return homeDirectory;

        // I can't believe there's no built in API for this...
        static void CopyDirectory(string source, string destination)
        {
            var dir = new DirectoryInfo(source);
            dir.Exists.Should().BeTrue();
            var dirs = dir.GetDirectories();

            Directory.CreateDirectory(destination);

            foreach (var file in dir.GetFiles())
            {
                file.CopyTo(Path.Combine(destination, file.Name));
            }

            foreach (var subDir in dirs)
            {
                var newDestinationDir = Path.Combine(destination, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }

    public static class Data
    {
        public static IEnumerable<object[]> IndexesOfFilesToGac
            => Enumerable.Range(0, new TracerValues(Path.GetTempPath()).FilesToAddToGac.Count)
                         .Select(i => new object[] { i });
    }
}
#endif
