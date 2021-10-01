// <copyright file="AspNetCoreIisMvc31Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyNUnit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreIisMvc31TestsInProcess : AspNetCoreIisMvc31Tests
    {
        public AspNetCoreIisMvc31TestsInProcess()
            : base(inProcess: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreIisMvc31TestsInProcessWithFeatureFlag : AspNetCoreIisMvc31Tests
    {
        public AspNetCoreIisMvc31TestsInProcessWithFeatureFlag()
            : base(inProcess: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetCoreIisMvc31TestsOutOfProcess : AspNetCoreIisMvc31Tests
    {
        public AspNetCoreIisMvc31TestsOutOfProcess()
            : base(inProcess: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetCoreIisMvc31TestsOutOfProcessWithFeatureFlag : AspNetCoreIisMvc31Tests
    {
        public AspNetCoreIisMvc31TestsOutOfProcessWithFeatureFlag()
            : base(inProcess: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetCoreIisMvc31Tests : AspNetCoreIisMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreIisMvc31Tests(bool inProcess, bool enableRouteTemplateResourceNames)
            : base("AspNetCoreMvc31", inProcess, enableRouteTemplateResourceNames)
        {
            _testName = GetTestName(nameof(AspNetCoreIisMvc31Tests));
        }

        [Property("Category", "EndToEnd")]
        [Property("Category", "LinuxUnsupported")]
        [Property("RunOnWindows", "True")]
        [TestCaseSource(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            // We actually sometimes expect 2, but waiting for 1 is good enough
            var spans = await GetWebServerSpans(path, Agent, HttpPort, statusCode, expectedSpanCount: 1);

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
