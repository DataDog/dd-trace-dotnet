// <copyright file="AspNetCoreMinimalApisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
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
    public class AspNetCoreMinimalApisTestsCallTarget : AspNetCoreMinimalApisTests
    {
        public AspNetCoreMinimalApisTestsCallTarget(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.None)
        {
        }
    }

    public class AspNetCoreMinimalApisTestsCallTargetWithFeatureFlag : AspNetCoreMinimalApisTests
    {
        public AspNetCoreMinimalApisTestsCallTargetWithFeatureFlag(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.RouteTemplateResourceNames)
        {
        }
    }

    public class AspNetCoreMinimalApisTestsCallTargetSingleSpan : AspNetCoreMinimalApisTests
    {
        public AspNetCoreMinimalApisTestsCallTargetSingleSpan(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, AspNetCoreFeatureFlags.SingleSpan)
        {
        }
    }

    public abstract class AspNetCoreMinimalApisTests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        protected AspNetCoreMinimalApisTests(AspNetCoreTestFixture fixture, ITestOutputHelper output, AspNetCoreFeatureFlags flags)
            : base("AspNetCoreMinimalApis", fixture, output, flags)
        {
            _testName = GetTestName(nameof(AspNetCoreMinimalApisTests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, int statusCode)
        {
            SetInstrumentationVerification();

            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.AspNetCoreMinimalApis", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);

            VerifyInstrumentation(Fixture.Process);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [InlineData("/", 200)]
        [InlineData("/not-found", 404)]
        [InlineData("/bad-request", 500)]
        public async Task BaggageInSpanTags(string path, int statusCode)
        {
            SetInstrumentationVerification();

            await Fixture.TryStartApp(this);
            var headers = new Dictionary<string, string>
            {
                { "baggage", "user.id=doggo" },
            };

            var spans = await Fixture.WaitForSpans(path, headers: headers);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.AspNetCoreMinimalApis", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_withBaggage")
                          .UseTypeName(_testName);

            VerifyInstrumentation(Fixture.Process);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [InlineData("/otel-baggage/clear-baggage", 200)]
        [InlineData("/otel-baggage/get-baggage", 200)]
        [InlineData("/otel-baggage/get-baggage-name/foo_case_sensitive_key", 200)]
        [InlineData("/otel-baggage/get-current", 200)]
        [InlineData("/otel-baggage/get-enumerator", 200)]
        [InlineData("/otel-baggage/remove-baggage/remove_me_key", 200)]
        [InlineData("/otel-baggage/set-baggage/foo_case_sensitive_key/overwrite_value", 200)]
        [InlineData("/otel-baggage/set-baggage/new_key/new_value", 200)]
        [InlineData("/otel-baggage/set-baggage-items/foo_case_sensitive_key/overwrite_value", 200)]
        [InlineData("/otel-baggage/set-baggage-items/new_key/new_value", 200)]
        [InlineData("/otel-baggage/set-current/foo_case_sensitive_key/overwrite_value", 200)]
        [InlineData("/otel-baggage/set-current/new_key/new_value", 200)]
        public async Task OtelBaggageApiIntegration(string path, int statusCode)
        {
            SetInstrumentationVerification();

            await Fixture.TryStartApp(this);
            string[] baggageItems = [
                "foo_case_sensitive_key=value_to_be_replaced",
                "unused_key=unused_value",
                "FOO_CASE_SENSITIVE_KEY=UNTOUCHED",
                "remove_me_key=remove_me_value",
            ];
            var headers = new Dictionary<string, string>
            {
                { "baggage", string.Join(",", baggageItems) },
            };

            var spans = await Fixture.WaitForSpans(path, headers: headers);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.AspNetCoreMinimalApis", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_OTelBaggageApi")
                          .UseTypeName(_testName);

            VerifyInstrumentation(Fixture.Process);
        }
    }
}
#endif
