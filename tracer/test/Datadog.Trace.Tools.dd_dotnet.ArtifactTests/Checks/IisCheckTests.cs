// <copyright file="IisCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests.Checks;

public class IisCheckTests : ToolTestHelper
{
    public IisCheckTests(ITestOutputHelper output)
        : base(GetSampleProjectName(), GetSampleProjectPath(), output)
    {
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkingApp(bool mixedRuntimes)
    {
#if NETFRAMEWORK
        if (mixedRuntimes)
        {
            // This test doesn't make sense on .NET Framework
            throw new SkipException();
        }
#endif
        EnsureWindowsAndX64();

        var siteName = mixedRuntimes ? "sample/mixed" : "sample";

        using var iisFixture = await StartIisWithGac(GetAppType());

        var output = await RunTool($"check iis {siteName} {IisExpressOptions(iisFixture)}");

        if (mixedRuntimes)
        {
            output.Should().Contain(Resources.IisMixedRuntimes);
        }

        output.Should().Contain(Resources.IisNoIssue);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NestedApp()
    {
        EnsureWindowsAndX64();

        var siteName = "sample/nested/app";

        using var iisFixture = await StartIisWithGac(GetAppType());
        var output = await RunTool($"check iis {siteName} {IisExpressOptions(iisFixture)}");

        output.Should().Contain(Resources.IisNoIssue);
    }

#if !NETFRAMEWORK
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task OutOfProcess()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIisWithGac(IisAppType.AspNetCoreOutOfProcess);

        var output = await RunTool($"check iis sample {IisExpressOptions(iisFixture)}");

        output.Should().Contain(Resources.OutOfProcess);
        output.Should().NotContain(Resources.AspNetCoreProcessNotFound);
        output.Should().Contain(Resources.IisNoIssue);
    }
#endif

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NoGac()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIis(GetAppType());

        var output = await RunTool($"check iis sample {IisExpressOptions(iisFixture)}");

        output.Should().Contain(Resources.MissingGac);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ListSites()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIis(GetAppType());

        var output = await RunTool($"check iis dummySite {IisExpressOptions(iisFixture)}");

        output.Should().Contain(Resources.CouldNotFindIisApplication("dummySite", "/"));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task ListApplications()
    {
        EnsureWindowsAndX64();

        using var iisFixture = await StartIis(GetAppType());

        var output = await RunTool($"check iis sample/dummy {IisExpressOptions(iisFixture)}");

        output.Should().Contain(Resources.CouldNotFindIisApplication("sample", "/dummy"));
    }

    private static IisAppType GetAppType()
    {
#if NETFRAMEWORK
            return IisAppType.AspNetIntegrated;
#else
        return IisAppType.AspNetCoreInProcess;
#endif
    }

    private static string GetSampleProjectName()
    {
#if NET6_0_OR_GREATER
            return "AspNetCoreMinimalApis";
#elif NETFRAMEWORK
            return "AspNetMvc5";
#else
        return "AspNetCoreMvc31";
#endif
    }

    private static string GetSampleProjectPath()
    {
#if NETFRAMEWORK
            return Path.Combine("test", "test-applications", "aspnet");
#else
        return null;
#endif
    }

    private static string IisExpressOptions(IisFixture iisFixture)
    {
        return $"--workerProcess {iisFixture.IisExpress.Process.Id} --iisConfigPath {iisFixture.IisExpress.ConfigFile}";
    }

    private static void EnsureWindowsAndX64()
    {
#if NETFRAMEWORK || (NETCOREAPP3_1_OR_GREATER && !NET5_0)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || IntPtr.Size != 8)
        {
            throw new SkipException();
        }
#else
        throw new SkipException();
#endif
    }

    private async Task<GacIisFixture> StartIisWithGac(IisAppType appType)
    {
        // GacFixture is not compatible with .NET Core, use the Nuke target instead
        GacIisFixture.GacAdd();

        try
        {
            return new GacIisFixture(await StartIis(appType));
        }
        catch
        {
            GacIisFixture.GacRemove();
            throw;
        }
    }

    private async Task<IisFixture> StartIis(IisAppType appType)
    {
        var fixture = new IisFixture { UseGac = false };

        if (appType is IisAppType.AspNetCoreInProcess or IisAppType.AspNetCoreOutOfProcess)
        {
            fixture.ShutdownPath = "/shutdown";
        }

        try
        {
            fixture.TryStartIis(this, appType);
        }
        catch (Exception)
        {
            fixture.Dispose();
            throw;
        }

        // Send a request to initialize the app
        using var httpClient = new HttpClient();
        await httpClient.GetAsync($"http://localhost:{fixture.HttpPort}/");

        return fixture;
    }

    private class GacIisFixture : IDisposable
    {
        private readonly IisFixture _fixture;

        public GacIisFixture(IisFixture fixture)
        {
            _fixture = fixture;
        }

        public static implicit operator IisFixture(GacIisFixture fixture) => fixture._fixture;

        public static void GacAdd()
        {
            RunNukeTask("GacAdd");
        }

        public static void GacRemove()
        {
            RunNukeTask("GacRemove");
        }

        public void Dispose()
        {
            _fixture.Dispose();

            GacRemove();
        }

        private static void RunNukeTask(string task)
        {
            var buildPs1 = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "build.ps1");

            // GacFixture is not compatible with .NET Core, use the Nuke target instead
            var startInfo = new ProcessStartInfo("powershell", $"{buildPs1} {task} --framework net461")
            {
                UseShellExecute = false
            };

            Process.Start(startInfo)!.WaitForExit();
        }
    }
}
