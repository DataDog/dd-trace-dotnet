// <copyright file="AspNetCoreMvc31TraceId128BitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31TraceId128Bit : AspNetCoreMvc31TraceId128BitTests
    {
        public AspNetCoreMvc31TraceId128Bit(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, generate128BitTraceIds: true)
        {
        }
    }

    public class AspNetCoreMvc31TraceId64Bit : AspNetCoreMvc31TraceId128BitTests
    {
        public AspNetCoreMvc31TraceId64Bit(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, generate128BitTraceIds: false)
        {
        }
    }

    public abstract class AspNetCoreMvc31TraceId128BitTests : AspNetCoreMvcTestBase
    {
        private readonly bool _generate128BitTraceIds;
        private readonly string _testName;

        protected AspNetCoreMvc31TraceId128BitTests(
            AspNetCoreTestFixture fixture,
            ITestOutputHelper output,
            bool generate128BitTraceIds)
            : base(
                "AspNetCoreMvc31",
                fixture,
                output,
                enableRouteTemplateResourceNames: true)
        {
            _generate128BitTraceIds = generate128BitTraceIds;
            _testName = GetTestName(nameof(AspNetCoreMvc31TraceId128BitTests));

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, generate128BitTraceIds ? "true" : "false");
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        protected override string GetTestName(string testName)
        {
            return _generate128BitTraceIds switch
                   {
                       true => $"{testName}.128bit",
                       false => $"{testName}.64bit",
                   };
        }
    }
}
#endif
