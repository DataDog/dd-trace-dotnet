// <copyright file="AspNetCoreMvc31Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31TestsCallTarget : AspNetCoreMvc31Tests
    {
        public AspNetCoreMvc31TestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreMvc31TestsCallTargetWithFeatureFlag : AspNetCoreMvc31Tests
    {
        public AspNetCoreMvc31TestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetCoreMvc31Tests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreMvc31Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames)
            : base("AspNetCoreMvc31", fixture, output, enableRouteTemplateResourceNames)
        {
            _testName = GetTestName(nameof(AspNetCoreMvc31Tests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.AspNetCoreMvc31", isExternalSpan: false);

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
