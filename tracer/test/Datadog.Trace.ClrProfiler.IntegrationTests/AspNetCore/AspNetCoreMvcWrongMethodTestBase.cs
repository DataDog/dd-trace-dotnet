// <copyright file="AspNetCoreMvcWrongMethodTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [UsesVerify]
    public class AspNetCoreMvcWrongMethodTestBase : TracingIntegrationTest, IClassFixture<AspNetCoreTestFixture>
    {
        private readonly AspNetCoreTestFixture fixture;
        private readonly string _testName;

        public AspNetCoreMvcWrongMethodTestBase(string testName, string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(sampleName, output)
        {
            this.fixture = fixture;
            _testName = testName;
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet_core.request" => span.IsAspNetCore(metadataSchemaVersion),
                "aspnet_core_mvc.request" => span.IsAspNetCoreMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        public async Task TestIncorrectMethod(string path)
        {
            await fixture.TryStartApp(this);

            var spans = await fixture.WaitForSpans(path, true);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }
}
#endif
