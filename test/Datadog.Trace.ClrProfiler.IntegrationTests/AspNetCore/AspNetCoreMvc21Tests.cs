// <copyright file="AspNetCoreMvc21Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Net;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc21TestsCallsite : AspNetCoreMvc21Tests
    {
        public AspNetCoreMvc21TestsCallsite(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreMvc21TestsCallsiteWithFeatureFlag : AspNetCoreMvc21Tests
    {
        public AspNetCoreMvc21TestsCallsiteWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetCoreMvc21TestsCallTarget : AspNetCoreMvc21Tests
    {
        public AspNetCoreMvc21TestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreMvc21TestsCallTargetWithFeatureFlag : AspNetCoreMvc21Tests
    {
        public AspNetCoreMvc21TestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetCoreMvc21Tests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreMvc21Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableCallTarget, bool enableRouteTemplateResourceNames)
            : base("AspNetCoreMvc21", fixture, output, enableCallTarget, enableRouteTemplateResourceNames)
        {
            _testName = GetTestName(nameof(AspNetCoreMvc21Tests));
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            await Fixture.TryStartApp(this, Output);

            var spans = await Fixture.WaitForSpans(Output, path);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseTypeName(_testName);
        }
    }
}
#endif
