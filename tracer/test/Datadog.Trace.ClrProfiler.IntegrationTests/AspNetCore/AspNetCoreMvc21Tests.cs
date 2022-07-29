// <copyright file="AspNetCoreMvc21Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
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
    public class AspNetCoreMvc21TestsCallTarget : AspNetCoreMvc21Tests
    {
        public AspNetCoreMvc21TestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreMvc21TestsCallTargetWithFeatureFlag : AspNetCoreMvc21Tests
    {
        public AspNetCoreMvc21TestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetCoreMvc21Tests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreMvc21Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames)
            : base("AspNetCoreMvc21", fixture, output, enableRouteTemplateResourceNames)
        {
            _testName = GetTestName(nameof(AspNetCoreMvc21Tests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);

            var aspnetCoreSpans = spans.Where(s => s.Name == "aspnet_core.request");
            foreach (var aspnetCoreSpan in aspnetCoreSpans)
            {
                var result = aspnetCoreSpan.IsAspNetCore();
                Assert.True(result.Success, result.ToString());
            }

            var aspnetCoreMvcSpans = spans.Where(s => s.Name == "aspnet_core_mvc.request");
            foreach (var aspnetCoreMvcSpan in aspnetCoreMvcSpans)
            {
                var result = aspnetCoreMvcSpan.IsAspNetCoreMvc();
                Assert.True(result.Success, result.ToString());
            }

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }
}
#endif
