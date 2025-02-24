// <copyright file="GacInstallerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.IO;
using System.Reflection;
using Datadog.FleetInstaller;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests.FleetInstaller;

public class GacInstallerTests(ITestOutputHelper output) : FleetInstallerTestsBase(output)
{
    private readonly ITestOutputHelper _output = output;

    [SkippableFact]
    public void FullInstallUninstall_WhenLoaderIsRemovedFromDisk_CanInstallAndUninstall()
    {
        Skip.IfNot(IsRunningAsAdministrator);

        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);
        // Verify prerequistes
        AssertFilesAreInGac(values, isInGac: false);

        GacInstaller.TryGacInstall(Log, values).Should().BeTrue();
        AssertFilesAreInGac(values, isInGac: true);

        // delete the native loader files
        File.Delete(values.NativeLoaderX64Path);
        File.Delete(values.NativeLoaderX86Path);

        GacInstaller.TryGacUninstall(Log, values).Should().BeTrue();
        AssertFilesAreInGac(values, isInGac: false);
    }

    [SkippableFact]
    public void FullInstallUninstall_WhenX64LoaderIsStillOnDisk_CanNotUninstall()
    {
        Skip.IfNot(IsRunningAsAdministrator);

        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);
        // Verify prerequistes
        AssertFilesAreInGac(values, isInGac: false);

        GacInstaller.TryGacInstall(Log, values).Should().BeTrue();
        AssertFilesAreInGac(values, isInGac: true);

        File.Delete(values.NativeLoaderX86Path);

        // Should fail and not be removed
        GacInstaller.TryGacUninstall(Log, values).Should().BeFalse();
        AssertFilesAreInGac(values, isInGac: true);
    }

    [SkippableFact]
    public void FullInstallUninstall_WhenX86LoaderIsStillOnDisk_CanNotUninstall()
    {
        Skip.IfNot(IsRunningAsAdministrator);

        var homeDirectory = CreateMonitoringHomeCopy();
        var values = new TracerValues(homeDirectory);
        // Verify prerequistes
        AssertFilesAreInGac(values, isInGac: false);

        GacInstaller.TryGacInstall(Log, values).Should().BeTrue();
        AssertFilesAreInGac(values, isInGac: true);

        File.Delete(values.NativeLoaderX64Path);

        // Should fail and not be removed
        GacInstaller.TryGacUninstall(Log, values).Should().BeFalse();
        AssertFilesAreInGac(values, isInGac: true);
    }

    [SkippableFact]
    public void FullInstallUninstall_WhenExistingVersionIsInGac_CanAddAndInstallSameVersionMultipleTimes()
    {
        // This scenario should _only_ happen in testing, but it's a "safe" one to handle
        Skip.IfNot(IsRunningAsAdministrator);

        // download the old version

        var homeDirectory1 = CreateMonitoringHomeCopy();
        var homeDirectory2 = CreateMonitoringHomeCopy();
        var values1 = new TracerValues(homeDirectory1);
        var values2 = new TracerValues(homeDirectory2);

        // Verify prerequistes
        AssertFilesAreInGac(values1, isInGac: false);

        GacInstaller.TryGacInstall(Log, values1).Should().BeTrue();
        AssertFilesAreInGac(values1, isInGac: true);
        AssertFilesAreInGac(values2, isInGac: true);

        GacInstaller.TryGacInstall(Log, values2).Should().BeTrue();
        AssertFilesAreInGac(values2, isInGac: true);

        // delete the native loader files from 1
        File.Delete(values1.NativeLoaderX64Path);
        File.Delete(values1.NativeLoaderX86Path);

        // Should succeed, but will still be in the gac
        GacInstaller.TryGacUninstall(Log, values1).Should().BeTrue();
        AssertFilesAreInGac(values1, isInGac: true);

        // delete the native loader files from 2
        File.Delete(values2.NativeLoaderX64Path);
        File.Delete(values2.NativeLoaderX86Path);

        // Should succeed, and now the values are gone
        GacInstaller.TryGacUninstall(Log, values2).Should().BeTrue();
        AssertFilesAreInGac(values1, isInGac: false);
    }

    // [SkippableFact]
    // public void FullInstallUninstall_WhenExistingVersionIsInGac_CanAddAndUninstallNewVersion()
    // {
    //     Skip.IfNot(IsRunningAsAdministrator);
    //
    //     // download the old version
    //
    //     var homeDirectory = CreateMonitoringHomeCopy();
    //     var values = new TracerValues(homeDirectory);
    //     // Verify prerequistes
    //     AssertFilesAreInGac(values, isInGac: false);
    //
    //     GacInstaller.TryGacInstall(Log, values).Should().BeTrue();
    //     AssertFilesAreInGac(values, isInGac: true);
    //
    //     File.Delete(values.NativeLoaderX64Path);
    //
    //     // Should fail and not be removed
    //     GacInstaller.TryGacUninstall(Log, values).Should().BeFalse();
    //     AssertFilesAreInGac(values, isInGac: false);
    // }

    private void AssertFilesAreInGac(TracerValues values, bool isInGac, string version = TracerConstants.AssemblyVersion)
    {
        foreach (var gacFile in values.FilesToAddToGac)
        {
            var assemblyName = $"{Path.GetFileNameWithoutExtension(gacFile)}, Version={version}";
            IsAssemblyInGac(assemblyName).Should().Be(isInGac);
        }

        return;

        bool IsAssemblyInGac(string assemblyName)
        {
            _output.WriteLine("Checking if assembly is in GAC: {0}", assemblyName);
            var gacUtilPath = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\gacutil.exe";
            var result = ProcessHelpers.RunCommand(new ProcessHelpers.Command(gacUtilPath, "/l Datadog.Trace"));

            _output.WriteLine($"ExitCode: {result?.ExitCode}, Error: {result?.Error}");
            _output.WriteLine(result?.Output);
            if (result.Output.Contains("Number of items = 0"))
            {
                return false;
            }

            if (result.Output.Contains("Number of items = "))
            {
                return true;
            }

            _output.WriteLine("Unexpected output");
            return false;
        }
    }

    // private string DownloadPreviousVersion()
    // {
    //     var previousVersionUrl =
    //     var homeDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    //     _output.WriteLine($"Copying {original} to {homeDirectory}");
    // }
}

#endif
