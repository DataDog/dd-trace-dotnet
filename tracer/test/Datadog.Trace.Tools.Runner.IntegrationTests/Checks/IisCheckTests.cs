// <copyright file="IisCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1

using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests.Checks
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class IisCheckTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public IisCheckTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetCoreMvc31", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/shutdown";
        }

        [SkippableFact]
        public async Task CheckApp()
        {
            if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                throw new SkipException();
            }

            _iisFixture.TryStartIis(this, IisAppType.AspNetCoreInProcess);

            // Send a request to initialize the app
            using var httpClient = new HttpClient();
            await httpClient.GetAsync($"http://localhost:{_iisFixture.HttpPort}/");

            using var console = ConsoleHelper.Redirect();

            var result = await CheckIisCommand.ExecuteAsync(new CheckIisSettings { SiteName = "sample" }, _iisFixture.IisExpress.ConfigFile, _iisFixture.IisExpress.Process.Id);

            result.Should().Be(0);

            console.Output.Should().Contain("No issue found with the IIS site.");
        }
    }
}

#endif
