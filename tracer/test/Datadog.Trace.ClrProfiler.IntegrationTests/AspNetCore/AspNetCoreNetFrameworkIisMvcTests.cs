// <copyright file="AspNetCoreNetFrameworkIisMvcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [Collection("IisTests")]
    public class AspNetCoreIisNetFrameworkMvc21Tests(IisFixture fixture, ITestOutputHelper output)
        : AspNetCoreIisNetFrameworkMvcTestsBase("AspNetCoreMvc21", fixture, output, nameof(AspNetCoreIisNetFrameworkMvc21Tests));

    [Collection("IisTests")]
    public class AspNetCoreIisNetFrameworkMvc22Tests(IisFixture fixture, ITestOutputHelper output)
        : AspNetCoreIisNetFrameworkMvcTestsBase("AspNetCoreMvc22", fixture, output, nameof(AspNetCoreIisNetFrameworkMvc22Tests));

    public abstract class AspNetCoreIisNetFrameworkMvcTestsBase : AspNetCoreNetFrameworkIisMvcTestsBase, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        protected AspNetCoreIisNetFrameworkMvcTestsBase(string sampleName, IisFixture fixture, ITestOutputHelper output, string testName)
            : base(sampleName, fixture, output)
        {
            _testName = testName;
            _iisFixture = fixture;
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "LinuxUnsupported")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, int statusCode)
        {
            var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, (HttpStatusCode)statusCode, expectedSpanCount: 1);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableTheory]
        [InlineData("/")]
        [InlineData("/delay/0")]
        public async Task MeetsAllAspNetCoreMvcExpectationsWithIncorrectMethod(string path)
        {
            var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, HttpStatusCode.NotFound, expectedSpanCount: 1, httpMethod: HttpMethod.Post);

            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath);

            // Overriding the type name here as we have multiple test classes in the file
            await Verifier.Verify(spans, settings)
                          .UseMethodName("WrongMethod")
                          .UseTypeName(_testName);
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetCoreOutOfProcess);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
