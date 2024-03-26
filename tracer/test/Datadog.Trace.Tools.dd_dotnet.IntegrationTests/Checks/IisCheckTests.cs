// <copyright file="IisCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.IntegrationTests.Checks;

[SupportedOSPlatform("windows")]
[Collection(nameof(ConsoleTestsCollection))]
public class IisCheckTests : TestHelper
{
    public IisCheckTests(ITestOutputHelper output)
        : base("AspNetCoreMinimalApis", output)
    {
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkingApp(bool mixedRuntimes)
    {
        EnsureWindowsAndX64();

        var siteName = mixedRuntimes ? "sample/mixed" : "sample";

        var buildPs1 = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "build.ps1");

        try
        {
            // GacFixture is not compatible with .NET Core, use the Nuke target instead
            Process.Start("powershell", $"{buildPs1} GacAdd --framework net461").WaitForExit();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                siteName,
                iisFixture.IisExpress.ConfigFile,
                iisFixture.IisExpress.Process.Id,
                MockRegistryService());

            result.Should().Be(0);

            if (mixedRuntimes)
            {
                console.Output.Should().Contain(Resources.IisMixedRuntimes);
            }

            console.Output.Should().Contain(Resources.IisNoIssue);
        }
        finally
        {
            Process.Start("powershell", $"{buildPs1} GacRemove --framework net461").WaitForExit();
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NestedApp()
    {
        EnsureWindowsAndX64();

        var siteName = "sample/nested/app";

        var buildPs1 = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "build.ps1");

        try
        {
            // GacFixture is not compatible with .NET Core, use the Nuke target instead
            Process.Start("powershell", $"{buildPs1} GacAdd --framework net461").WaitForExit();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                             siteName,
                             iisFixture.IisExpress.ConfigFile,
                             iisFixture.IisExpress.Process.Id,
                             MockRegistryService());

            result.Should().Be(0);

            console.Output.Should().Contain(Resources.IisNoIssue);
        }
        finally
        {
            Process.Start("powershell", $"{buildPs1} GacRemove --framework net461").WaitForExit();
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task OutOfProcess()
    {
        EnsureWindowsAndX64();

        var buildPs1 = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "build.ps1");

        try
        {
            // GacFixture is not compatible with .NET Core, use the Nuke target instead
            Process.Start("powershell", $"{buildPs1} GacAdd --framework net461").WaitForExit();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreOutOfProcess);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                "sample",
                iisFixture.IisExpress.ConfigFile,
                iisFixture.IisExpress.Process.Id,
                MockRegistryService());

            result.Should().Be(0);

            console.Output.Should().Contain(Resources.OutOfProcess);
            console.Output.Should().NotContain(Resources.AspNetCoreProcessNotFound);
            console.Output.Should().NotContain(Resources.AspNetCoreOutOfProcessNotFound);
            console.Output.Should().Contain(Resources.IisNoIssue);
        }
        finally
        {
            Process.Start("powershell", $"{buildPs1} GacRemove --framework net461").WaitForExit();
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task OutOfProcessNotInitialized()
    {
        EnsureWindowsAndX64();

        var buildPs1 = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "build.ps1");

        try
        {
            // GacFixture is not compatible with .NET Core, use the Nuke target instead
            Process.Start("powershell", $"{buildPs1} GacAdd --framework net461").WaitForExit();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreOutOfProcess, sendRequest: false);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                             "sample",
                             iisFixture.IisExpress.ConfigFile,
                             iisFixture.IisExpress.Process.Id,
                             MockRegistryService());

            result.Should().Be(1);

            console.Output.Should().Contain(Resources.AspNetCoreOutOfProcessNotFound);
        }
        finally
        {
            Process.Start("powershell", $"{buildPs1} GacRemove --framework net461").WaitForExit();
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NoGac()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

        using var console = ConsoleHelper.Redirect();

        var result = await CheckIisCommand.ExecuteAsync(
            "sample",
            iisFixture.IisExpress.ConfigFile,
            iisFixture.IisExpress.Process.Id,
            MockRegistryService());

        result.Should().Be(1);

        console.Output.Should().Contain(Resources.MissingGac);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ListSites()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

        using var console = ConsoleHelper.Redirect();

        var result = await CheckIisCommand.ExecuteAsync(
            "dummySite",
            iisFixture.IisExpress.ConfigFile,
            iisFixture.IisExpress.Process.Id,
            MockRegistryService());

        result.Should().Be(1);

        console.Output.Should().Contain(Resources.CouldNotFindIisApplication("dummySite", "/"));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ListApplications()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

        using var console = ConsoleHelper.Redirect();

        var result = await CheckIisCommand.ExecuteAsync(
            "sample/dummy",
            iisFixture.IisExpress.ConfigFile,
            iisFixture.IisExpress.Process.Id,
            MockRegistryService());

        result.Should().Be(1);

        console.Output.Should().Contain(Resources.CouldNotFindIisApplication("sample", "/dummy"));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task IncorrectlyConfiguredAppPool()
    {
        EnsureWindowsAndX64();

        EnvironmentHelper.SetAutomaticInstrumentation(false);

        using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);
        using var console = ConsoleHelper.Redirect();
        var result = await CheckIisCommand.ExecuteAsync(
                         "sample",
                         iisFixture.IisExpress.ConfigFile,
                         iisFixture.IisExpress.Process.Id,
                         MockRegistryService());

        result.Should().Be(1);
        console.Output.Should().Contain(Resources.AppPoolCheckFindings("applicationPoolDefaults"));
        console.Output.Should().Contain(Resources.WrongEnvironmentVariableFormat("COR_ENABLE_PROFILING", "1", "0"));
        console.Output.Should().Contain(Resources.WrongEnvironmentVariableFormat("CORECLR_ENABLE_PROFILING", "1", "0"));
    }

    private static void EnsureWindowsAndX64()
    {
        if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            || IntPtr.Size != 8)
        {
            throw new SkipException();
        }
    }

    private static IRegistryService MockRegistryService()
    {
        var registryService = new Mock<IRegistryService>();
        registryService.Setup(r => r.GetLocalMachineValueNames(It.Is(@"SOFTWARE\Microsoft\.NETFramework", StringComparer.Ordinal)))
            .Returns(Array.Empty<string>());
        registryService.Setup(r => r.GetLocalMachineValue(It.Is<string>(s => s == ProcessBasicChecksTests.ClsidKey || s == ProcessBasicChecksTests.Clsid32Key)))
            .Returns(EnvironmentHelper.GetNativeLoaderPath());

        return registryService.Object;
    }

    private async Task<IisFixture> StartIis(IisAppType appType, bool sendRequest = true)
    {
        var fixture = new IisFixture { ShutdownPath = "/shutdown" };

        try
        {
            await fixture.TryStartIis(this, appType);
        }
        catch (Exception)
        {
            fixture.Dispose();
            throw;
        }

        if (sendRequest)
        {
            // Send a request to initialize the app
            using var httpClient = new HttpClient();
            await httpClient.GetAsync($"http://localhost:{fixture.HttpPort}/");
        }

        return fixture;
    }
}
