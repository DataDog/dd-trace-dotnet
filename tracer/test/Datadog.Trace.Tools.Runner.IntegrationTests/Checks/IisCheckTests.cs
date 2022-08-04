// <copyright file="IisCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Checks;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests.Checks
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class IisCheckTests : TestHelper
    {
        public IisCheckTests(ITestOutputHelper output)
            : base("AspNetCoreMvc31", output)
        {
        }

        [SkippableTheory]
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
                    new CheckIisSettings { SiteName = siteName },
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
                    new CheckIisSettings { SiteName = "sample" },
                    iisFixture.IisExpress.ConfigFile,
                    iisFixture.IisExpress.Process.Id,
                    MockRegistryService());

                result.Should().Be(0);

                console.Output.Should().Contain(Resources.OutOfProcess);
                console.Output.Should().NotContain(Resources.AspNetCoreProcessNotFound);
                console.Output.Should().Contain(Resources.IisNoIssue);
            }
            finally
            {
                Process.Start("powershell", $"{buildPs1} GacRemove --framework net461").WaitForExit();
            }
        }

        [SkippableFact]
        public async Task NoGac()
        {
            EnsureWindowsAndX64();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                new CheckIisSettings { SiteName = "sample" },
                iisFixture.IisExpress.ConfigFile,
                iisFixture.IisExpress.Process.Id,
                MockRegistryService());

            result.Should().Be(1);

            console.Output.Should().Contain(Resources.MissingGac);
        }

        [SkippableFact]
        public async Task ListSites()
        {
            EnsureWindowsAndX64();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                new CheckIisSettings { SiteName = "dummySite" },
                iisFixture.IisExpress.ConfigFile,
                iisFixture.IisExpress.Process.Id,
                MockRegistryService());

            result.Should().Be(1);

            console.Output.Should().Contain(Resources.CouldNotFindIisApplication("dummySite", "/"));
        }

        [SkippableFact]
        public async Task ListApplications()
        {
            EnsureWindowsAndX64();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(
                new CheckIisSettings { SiteName = "sample/dummy" },
                iisFixture.IisExpress.ConfigFile,
                iisFixture.IisExpress.Process.Id,
                MockRegistryService());

            result.Should().Be(1);

            console.Output.Should().Contain(Resources.CouldNotFindIisApplication("sample", "/dummy"));
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

        private async Task<IisFixture> StartIis(IisAppType appType)
        {
            var fixture = new IisFixture { ShutdownPath = "/shutdown" };

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
    }
}

#endif
