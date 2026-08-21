// <copyright file="OtlpAspNetCoreMvc21Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1
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
    public class OtlpAspNetCoreMvc21TestsWithoutFeatureFlag : OtlpAspNetCoreMvc21Tests
    {
        public OtlpAspNetCoreMvc21TestsWithoutFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: false, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc21TestsWithFeatureFlag : OtlpAspNetCoreMvc21Tests
    {
        public OtlpAspNetCoreMvc21TestsWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: true, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc21TestsWithoutFeatureFlagWithOpenTelemetrySemantics : OtlpAspNetCoreMvc21Tests
    {
        public OtlpAspNetCoreMvc21TestsWithoutFeatureFlagWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: false, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetCoreMvc21TestsWithFeatureFlagWithOpenTelemetrySemantics : OtlpAspNetCoreMvc21Tests
    {
        public OtlpAspNetCoreMvc21TestsWithFeatureFlagWithOpenTelemetrySemantics(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: true, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    public abstract class OtlpAspNetCoreMvc21Tests : OtlpAspNetCoreTestBase
    {
        protected OtlpAspNetCoreMvc21Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames, bool openTelemetrySemanticsEnabled)
            : base("AspNetCoreMvc21", fixture, output, enableRouteTemplateResourceNames, openTelemetrySemanticsEnabled)
        {
            TestName = GetTestName(nameof(OtlpAspNetCoreMvc21Tests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string httpMethod, string path, int statusCode, bool handledByEndpoint)
        {
            await RunTestCaseAsync(httpMethod, path, statusCode, GetExpectedSpanCount(handledByEndpoint));
        }

        /// <summary>
        /// The server span is the only one this sample application produces, except that every one of
        /// its endpoints is an MVC action, so a routed request also gets an
        /// <c>aspnet_core_mvc.request</c> child in the one combination that still emits it.
        /// </summary>
        /// <param name="handledByEndpoint">Whether an endpoint handled the request.</param>
        private int GetExpectedSpanCount(bool handledByEndpoint)
            => handledByEndpoint && ProducesMvcChildSpan ? 2 : 1;
    }
}
#endif
