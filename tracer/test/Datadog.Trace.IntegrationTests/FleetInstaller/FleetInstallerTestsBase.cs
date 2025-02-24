// <copyright file="FleetInstallerTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using Datadog.FleetInstaller;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

public abstract class FleetInstallerTestsBase : IDisposable
{
    private static bool? _isElevated;
    private readonly ITestOutputHelper _output;
    private List<string> _homeDirectory;

    protected FleetInstallerTestsBase(ITestOutputHelper output)
    {
        Log = new(output);
        _output = output;
    }

    public static bool IsRunningAsAdministrator
    {
        get
        {
            _isElevated ??= new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            return _isElevated.Value;
        }
    }

    protected FleetInstallerLogger Log { get; }

    public void Dispose()
    {
        if (_homeDirectory is { } list)
        {
            foreach (var dir in list)
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    GacInstaller.TryGacUninstall(Log, new TracerValues(dir));
                }
                catch
                {
                    // Ignore, something is locking it
                }
            }
        }
    }

    protected string CreateMonitoringHomeCopy()
    {
        // create a copy of monitoring home, so that we can mess with it
        var original = EnvironmentHelper.GetMonitoringHomePath();
        var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _output.WriteLine($"Copying {original} to {homeDirectory}");

        CopyDirectory(original, homeDirectory);
        _homeDirectory ??= new();
        _homeDirectory.Add(homeDirectory);
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
}
#endif
