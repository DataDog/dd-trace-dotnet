// <copyright file="AspNetCoreMvc31AwsApiGatewayTests.cs" company="Datadog">
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
    public class AspNetCoreMvc31AwsApiGatewayEnabled : AspNetCoreMvc31AwsApiGatewayTests
    {
        public AspNetCoreMvc31AwsApiGatewayEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, awsApiGatewaySpanEnabled: true)
        {
        }
    }

    public class AspNetCoreMvc31AwsApiGatewayDisabled : AspNetCoreMvc31TraceId128BitTests
    {
        public AspNetCoreMvc31AwsApiGatewayDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, generate128BitTraceIds: false)
        {
        }
    }

    public abstract class AspNetCoreMvc31AwsApiGatewayTests : AspNetCoreMvcTestBase
    {
        private readonly bool _awsApiGatewaySpanEnabled;
        private readonly string _testName;

        protected AspNetCoreMvc31AwsApiGatewayTests(
            AspNetCoreTestFixture fixture,
            ITestOutputHelper output,
            bool awsApiGatewaySpanEnabled)
            : base(
                "AspNetCoreMvc31",
                fixture,
                output,
                enableRouteTemplateResourceNames: true)
        {
            _awsApiGatewaySpanEnabled = awsApiGatewaySpanEnabled;
            _testName = GetTestName(nameof(AspNetCoreMvc31AwsApiGatewayTests));

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, awsApiGatewaySpanEnabled ? "true" : "false");
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
            return _awsApiGatewaySpanEnabled switch
                   {
                       true => $"{testName}.InferredProxySpans_True",
                       false => $"{testName}.InferredProxySpans_False",
                   };
        }
    }
}
#endif
