// <copyright file="AspNetCoreMvc31InferredProxySpansTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31InferredProxySpansEnabled : AspNetCoreMvc31InferredProxySpansTests
    {
        public AspNetCoreMvc31InferredProxySpansEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, inferredProxySpansEnabled: true)
        {
        }
    }

    public class AspNetCoreMvc31InferredProxySpansDisabled : AspNetCoreMvc31InferredProxySpansTests
    {
        public AspNetCoreMvc31InferredProxySpansDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, inferredProxySpansEnabled: false)
        {
        }
    }

    public abstract class AspNetCoreMvc31InferredProxySpansTests : AspNetCoreMvcTestBase
    {
        private readonly bool _inferredProxySpansEnabled;
        private readonly string _testName;

        protected AspNetCoreMvc31InferredProxySpansTests(
            AspNetCoreTestFixture fixture,
            ITestOutputHelper output,
            bool inferredProxySpansEnabled)
            : base(
                "AspNetCoreMvc31",
                fixture,
                output,
                enableRouteTemplateResourceNames: true)
        {
            _inferredProxySpansEnabled = inferredProxySpansEnabled;

            var enabled = inferredProxySpansEnabled ? "Enabled" : "Disabled";
            _testName = $"{nameof(AspNetCoreMvc31InferredProxySpansTests)}.{enabled}";

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, inferredProxySpansEnabled ? "true" : "false");
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData("/", 200, 2)]
        [InlineData("/not-found", 404, 1)] // 404 does not create "aspnet_core_mvc.request" span
        [InlineData("/status-code/203", 203, 2)]
        [InlineData("/bad-request", 500, 2)]
        [InlineData("/handled-exception", 500, 2)]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, int statusCode, int spanCount)
        {
            var start = DateTimeOffset.UtcNow;
            await Fixture.TryStartApp(this);

            // get the http request so we can add the api gateway headers
            var request = Fixture.CreateRequest(HttpMethod.Get, path);

            if (_inferredProxySpansEnabled)
            {
                // expect an additional span for the inferred proxy span
                spanCount++;

                var headers = request.Headers;
                headers.Add("x-dd-proxy", "aws-apigateway");
                headers.Add("x-dd-proxy-request-time-ms", start.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                headers.Add("x-dd-proxy-domain-name", "test.api.com");
                headers.Add("x-dd-proxy-httpmethod", "GET");
                headers.Add("x-dd-proxy-path", "/api/test/1");
                headers.Add("x-dd-proxy-stage", "prod");
            }

            // don't call Fixture.WaitForSpans() directly so we can override span count and start date
            await Fixture.SendHttpRequest(request);
            var spans = await Fixture.Agent.WaitForSpansAsync(count: spanCount, minDateTime: start, returnAllOperations: true);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode, spanCount);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }
}
#endif
