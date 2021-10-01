// <copyright file="AspNetCoreMvc30Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyNUnit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc30TestsCallsite : AspNetCoreMvc30Tests
    {
        public AspNetCoreMvc30TestsCallsite()
            : base(enableCallTarget: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreMvc30TestsCallsiteWithFeatureFlag : AspNetCoreMvc30Tests
    {
        public AspNetCoreMvc30TestsCallsiteWithFeatureFlag()
            : base(enableCallTarget: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetCoreMvc30TestsCallTarget : AspNetCoreMvc30Tests
    {
        public AspNetCoreMvc30TestsCallTarget()
            : base(enableCallTarget: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreMvc30TestsCallTargetWithFeatureFlag : AspNetCoreMvc30Tests
    {
        public AspNetCoreMvc30TestsCallTargetWithFeatureFlag()
            : base(enableCallTarget: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetCoreMvc30Tests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreMvc30Tests(bool enableCallTarget, bool enableRouteTemplateResourceNames)
            : base("AspNetCoreMvc30", enableCallTarget, enableRouteTemplateResourceNames)
        {
            _testName = GetTestName(nameof(AspNetCoreMvc30Tests));
        }

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [TestCaseSource(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            var spans = await WaitForSpans(path);

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
