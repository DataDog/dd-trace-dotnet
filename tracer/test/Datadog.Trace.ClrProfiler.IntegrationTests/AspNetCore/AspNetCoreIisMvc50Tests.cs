// <copyright file="AspNetCoreIisMvc50Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [Collection("IisTests")]
    public class AspNetCoreIisMvc50TestsInProcessWithFeatureFlag : AspNetCoreIisMvc50Tests
    {
        public AspNetCoreIisMvc50TestsInProcessWithFeatureFlag(IisFixture fixture, ITestOutputHelper output)
            : base(fixture, output, inProcess: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetCoreIisMvc50TestsOutOfProcessWithFeatureFlag : AspNetCoreIisMvc50Tests
    {
        public AspNetCoreIisMvc50TestsOutOfProcessWithFeatureFlag(IisFixture fixture, ITestOutputHelper output)
            : base(fixture, output, inProcess: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetCoreIisMvc50Tests : AspNetCoreIisMvcTestBase
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        protected AspNetCoreIisMvc50Tests(IisFixture fixture, ITestOutputHelper output, bool inProcess, bool enableRouteTemplateResourceNames)
            : base("AspNetCoreMvc31", fixture, output, inProcess, enableRouteTemplateResourceNames)
        {
            _testName = GetTestName("AspNetCoreIisMvc31Tests");
            _iisFixture = fixture;
            _iisFixture.TryStartIis(this, inProcess ? IisAppType.AspNetCoreInProcess : IisAppType.AspNetCoreOutOfProcess);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "LinuxUnsupported")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            // We actually sometimes expect 2, but waiting for 1 is good enough
            var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode, expectedSpanCount: 1);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);
            settings.DisableRequireUniquePrefix(); // Disable default Verifier behavior so AspNetCore31 and AspNetCore50 can share snapshots

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }
}
#endif
