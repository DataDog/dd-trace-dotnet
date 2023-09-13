// <copyright file="IisCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests.Checks
{
    public class IisCheckTests : TestHelper
    {
        public IisCheckTests(ITestOutputHelper output)
            : base("AspNetCoreMvc31", output)
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

                var output = await RunTool($"check iis {siteName} {IisExpressOptions(iisFixture)}");

                if (mixedRuntimes)
                {
                    output.Should().Contain(Resources.IisMixedRuntimes);
                }

                output.Should().Contain(Resources.IisNoIssue);
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

                var output = await RunTool($"check iis {siteName} {IisExpressOptions(iisFixture)}");

                output.Should().Contain(Resources.IisNoIssue);
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

                var output = await RunTool($"check iis sample {IisExpressOptions(iisFixture)}");

                output.Should().Contain(Resources.OutOfProcess);
                output.Should().NotContain(Resources.AspNetCoreProcessNotFound);
                output.Should().Contain(Resources.IisNoIssue);
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

            var output = await RunTool($"check iis sample {IisExpressOptions(iisFixture)}");

            output.Should().Contain(Resources.MissingGac);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task ListSites()
        {
            EnsureWindowsAndX64();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            var output = await RunTool($"check iis dummySite {IisExpressOptions(iisFixture)}");

            output.Should().Contain(Resources.CouldNotFindIisApplication("dummySite", "/"));
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task ListApplications()
        {
            EnsureWindowsAndX64();

            using var iisFixture = await StartIis(IisAppType.AspNetCoreInProcess);

            var output = await RunTool($"check iis sample/dummy {IisExpressOptions(iisFixture)}");

            output.Should().Contain(Resources.CouldNotFindIisApplication("sample", "/dummy"));
        }

        private static string IisExpressOptions(IisFixture iisFixture)
        {
            return $"--workerProcess {iisFixture.IisExpress.Process.Id} --iisConfigPath {iisFixture.IisExpress.ConfigFile}";
        }

        private static void EnsureWindowsAndX64()
        {
#if !NETCOREAPP3_1
            // TODO: Find how to test on other targets
            throw new SkipException();
#else
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || IntPtr.Size != 8)
            {
                throw new SkipException();
            }
#endif
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

        private async Task<string> RunTool(string arguments, params (string Key, string Value)[] environmentVariables)
        {
            // TODO: point to the actual artifact
            var targetFolder = @"C:\git\dd-trace-dotnet-diag\tracer\src\Datadog.Trace.Tools.dd_dotnet\bin\Release\net7.0\win-x64\publish";
            var executable = Path.Combine(targetFolder, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dd-dotnet.exe" : "dd-dotnet");

            var processStart = new ProcessStartInfo(executable, arguments)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            foreach (var (key, value) in environmentVariables)
            {
                processStart.EnvironmentVariables[key] = value;
            }

            using var helper = new ProcessHelper(Process.Start(processStart));

            await helper.Task;

            var splitOutput = helper.StandardOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            return string.Join(" ", splitOutput.Select(o => o.TrimEnd()));
        }
    }
}
