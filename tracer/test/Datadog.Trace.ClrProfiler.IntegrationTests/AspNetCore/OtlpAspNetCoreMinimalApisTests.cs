// <copyright file="OtlpAspNetCoreMinimalApisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class OtlpAspNetCoreMinimalApisTestsCallTarget : OtlpAspNetCoreMinimalApisTests
    {
        public OtlpAspNetCoreMinimalApisTestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.None, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    public class OtlpAspNetCoreMinimalApisTestsCallTargetWithFeatureFlag : OtlpAspNetCoreMinimalApisTests
    {
        public OtlpAspNetCoreMinimalApisTestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    public class OtlpAspNetCoreMinimalApisTestsCallTargetSingleSpan : OtlpAspNetCoreMinimalApisTests
    {
        public OtlpAspNetCoreMinimalApisTestsCallTargetSingleSpan(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.SingleSpan, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    public class OtlpAspNetCoreMinimalApisTestsCallTargetWithOpenTelemetrySemantics : OtlpAspNetCoreMinimalApisTests
    {
        public OtlpAspNetCoreMinimalApisTestsCallTargetWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.None, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    public class OtlpAspNetCoreMinimalApisTestsCallTargetWithFeatureFlagWithOpenTelemetrySemantics : OtlpAspNetCoreMinimalApisTests
    {
        public OtlpAspNetCoreMinimalApisTestsCallTargetWithFeatureFlagWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    public class OtlpAspNetCoreMinimalApisTestsCallTargetSingleSpanWithOpenTelemetrySemantics : OtlpAspNetCoreMinimalApisTests
    {
        public OtlpAspNetCoreMinimalApisTestsCallTargetSingleSpanWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.SingleSpan, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    public abstract class OtlpAspNetCoreMinimalApisTests : OtlpAspNetCoreTestBase
    {
        /// <summary>
        /// The only route in Samples.AspNetCoreMinimalApis that is served by a minimal-API endpoint
        /// rather than by the HomeController the sample links in from Samples.AspNetCoreMvc21.
        /// </summary>
        private const string MinimalApiRoute = "/api/delay";

        /// <summary>
        /// The path base Samples.AspNetCoreMinimalApis is mounted under, matching the sample's own
        /// <c>app.UsePathBase("/path-base")</c>. The "/path-base/api/delay/0" row in
        /// <see cref="OtlpAspNetCoreTestBase.Data"/> reaches the same minimal-API endpoint as
        /// "/api/delay/0", just through the mounted path base, so it must be stripped before matching
        /// <see cref="MinimalApiRoute"/> below.
        /// </summary>
        private const string PathBasePrefix = "/path-base";

        protected OtlpAspNetCoreMinimalApisTests(AspNetCoreTestFixture fixture, ITestOutputHelper output, AspNetCoreFeatureFlags flags, bool openTelemetrySemanticsEnabled)
            : base("AspNetCoreMinimalApis", fixture, output, flags, openTelemetrySemanticsEnabled)
        {
            TestName = GetTestName(nameof(OtlpAspNetCoreMinimalApisTests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMinimalApisExpectations(string httpMethod, string path, int statusCode, bool handledByEndpoint)
        {
            await RunTestCaseAsync(httpMethod, path, statusCode, GetExpectedSpanCount(path, handledByEndpoint));
        }

        /// <summary>
        /// A minimal-API endpoint is a delegate rather than an MVC action, so it never reaches
        /// our MVC instrumentation. The rest of the paths are handled by the HomeController, so they
        /// behave the same as the MVC suites.
        /// </summary>
        /// <param name="path">The path that was requested.</param>
        /// <param name="handledByEndpoint">Whether an endpoint handled the request.</param>
        private int GetExpectedSpanCount(string path, bool handledByEndpoint)
        {
            var routePath = path.StartsWith(PathBasePrefix, StringComparison.Ordinal)
                                 ? path[PathBasePrefix.Length..]
                                 : path;

            if (!handledByEndpoint || routePath.StartsWith(MinimalApiRoute, StringComparison.Ordinal))
            {
                return 1;
            }

            return 1 + (ProducesMvcChildSpan ? 1 : 0);
        }
    }
}
#endif
