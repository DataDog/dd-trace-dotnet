// <copyright file="AspNetCoreMvc31Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31TestsCallTarget : AspNetCoreMvc31Tests
    {
        public AspNetCoreMvc31TestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.None)
        {
        }
    }

    public class AspNetCoreMvc31TestsCallTargetWithFeatureFlag : AspNetCoreMvc31Tests
    {
        public AspNetCoreMvc31TestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames)
        {
        }
    }

#if NET6_0_OR_GREATER
    public class AspNetCoreMvc31TestsCallTargetSingleSpan : AspNetCoreMvc31Tests
    {
        public AspNetCoreMvc31TestsCallTargetSingleSpan(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.SingleSpan)
        {
        }
    }
#endif

    /// <summary>
    /// Asserts the ASP.NET Core span shape when <c>DD_TRACE_OTEL_SEMANTICS_ENABLED=true</c>, i.e.
    /// when the OpenTelemetry HTTP semantic convention attributes replace the Datadog ones.
    /// </summary>
    public class AspNetCoreMvc31OTelTests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        public AspNetCoreMvc31OTelTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreMvc31", fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames)
        {
            SetEnvironmentVariable("ADD_EXTRA_MIDDLEWARE", "1");
            SetEnvironmentVariable(ConfigurationKeys.OpenTelemetry.OtelSemanticsEnabled, "true");

            // The base class widens the server error statuses to "400-403, 500-503", but the
            // OpenTelemetry specification requires 4xx server responses not to be errors, so use
            // the product default instead.
            SetEnvironmentVariable(ConfigurationKeys.HttpServerErrorStatusCodes, "500-599");

            _testName = nameof(AspNetCoreMvc31OTelTests);
        }

        // The base rows, plus a query string so url.query is covered
        public static TheoryData<string, int> OTelData()
        {
            var data = Data();
            data.Add("/?query=test", 200);
            return data;
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(OTelData))]
        public async Task MeetsAllAspNetCoreMvcOTelExpectations(string path, int statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);

            // OpenTelemetry's ASP.NET Core instrumentation produces a single server span
            spans.Should().NotContain(s => s.Name == "aspnet_core_mvc.request");

            ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: "Samples.AspNetCoreMvc31", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // With OTel semantics, exceptions are recorded as span events rather than error.* tags
            VerifyHelper.AddSpanEventScrubbers(settings);

            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }

    public abstract class AspNetCoreMvc31Tests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreMvc31Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output, AspNetCoreFeatureFlags flags)
            : base("AspNetCoreMvc31", fixture, output, flags)
        {
            SetEnvironmentVariable("ADD_EXTRA_MIDDLEWARE", "1");
            _testName = GetTestName(nameof(AspNetCoreMvc31Tests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, int statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.AspNetCoreMvc31", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }
}
#endif
