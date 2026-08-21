// <copyright file="OtlpAspNetCoreMvc31Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc31TestsCallTarget : OtlpAspNetCoreMvc31Tests
    {
        public OtlpAspNetCoreMvc31TestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.None, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc31TestsCallTargetWithFeatureFlag : OtlpAspNetCoreMvc31Tests
    {
        public OtlpAspNetCoreMvc31TestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc31TestsCallTargetWithOpenTelemetrySemantics : OtlpAspNetCoreMvc31Tests
    {
        public OtlpAspNetCoreMvc31TestsCallTargetWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.None, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc31TestsCallTargetWithFeatureFlagWithOpenTelemetrySemantics : OtlpAspNetCoreMvc31Tests
    {
        public OtlpAspNetCoreMvc31TestsCallTargetWithFeatureFlagWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames, openTelemetrySemanticsEnabled: true)
        {
        }
    }

#if NET6_0_OR_GREATER
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc31TestsCallTargetSingleSpan : OtlpAspNetCoreMvc31Tests
    {
        public OtlpAspNetCoreMvc31TestsCallTargetSingleSpan(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.SingleSpan, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc31TestsCallTargetSingleSpanWithOpenTelemetrySemantics : OtlpAspNetCoreMvc31Tests
    {
        public OtlpAspNetCoreMvc31TestsCallTargetSingleSpanWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.SingleSpan, openTelemetrySemanticsEnabled: true)
        {
        }
    }
#endif

    public abstract class OtlpAspNetCoreMvc31Tests : OtlpAspNetCoreTestBase
    {
        protected OtlpAspNetCoreMvc31Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output, AspNetCoreFeatureFlags flags, bool openTelemetrySemanticsEnabled)
            : base("AspNetCoreMvc31", fixture, output, flags, openTelemetrySemanticsEnabled)
        {
            // Unlike AspNetCoreMvc31Tests, do not run the extra middleware so we can only test the default behavior.
            // SetEnvironmentVariable("ADD_EXTRA_MIDDLEWARE", "1");
            TestName = GetTestName(nameof(OtlpAspNetCoreMvc31Tests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string httpMethod, string path, int statusCode, bool handledByEndpoint)
        {
            await RunTestCaseAsync(httpMethod, path, statusCode, GetExpectedSpanCount(handledByEndpoint));
        }

        /// <summary>
        /// Every endpoint in this sample application is an MVC action, so a routed request also
        /// reaches our MVC instrumentation.
        /// </summary>
        /// <param name="handledByEndpoint">Whether an endpoint handled the request.</param>
        private int GetExpectedSpanCount(bool handledByEndpoint)
        {
            if (!handledByEndpoint)
            {
                return 1;
            }

            return 1 + (ProducesMvcChildSpan ? 1 : 0);
        }
    }
}
#endif
