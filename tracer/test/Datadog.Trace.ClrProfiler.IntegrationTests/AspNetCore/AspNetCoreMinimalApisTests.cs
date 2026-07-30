// <copyright file="AspNetCoreMinimalApisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
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

    public class AspNetCoreMinimalApisOTelTests : AspNetCoreMvcTestBase
    {
        private readonly string _testName;

        public AspNetCoreMinimalApisOTelTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreMinimalApis", fixture, output, AspNetCoreFeatureFlags.None)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
            _testName = GetTestName(nameof(AspNetCoreMinimalApisOTelTests));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTracesOTel(string path, int statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans("/");
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "otel", expectedServiceName: "Samples.AspNetCoreMinimalApis", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }

    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    public class AspNetCoreMinimalApisOTelTestsWithOtlp : AspNetCoreMvcTestBase
    {
        private readonly string _testName;
        private readonly string _testAgentHost;

        public AspNetCoreMinimalApisOTelTestsWithOtlp(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreMinimalApis", fixture, output, AspNetCoreFeatureFlags.None)
        {
            _testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", "true");
            SetEnvironmentVariable("OTEL_TRACES_EXPORTER", "otlp");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/json");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{_testAgentHost}:4318");

            _testName = GetTestName(nameof(AspNetCoreMinimalApisOTelTestsWithOtlp));
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [MemberData(nameof(Data))]
        public async Task SubmitsOtlpTraces(string path, int statusCode)
        {
            await ClearTestAgentSession(_testAgentHost);

            // Start the long-running ASP.NET Core app via the shared fixture (sendHealthCheck: false
            // because OTLP mode routes spans to the test agent, not the mock agent, which would cause
            // WaitForSpansAsync to timeout waiting for spans that will never arrive).
            await Fixture.TryStartApp(this, sendHealthCheck: false);

            // Send the request to the target path to generate a trace
            var request = Fixture.CreateRequest(HttpMethod.Get, path);
            await Fixture.SendHttpRequest(request);

            // Poll the test agent for OTLP traces (DD SDK always uses http/json per constructor setup)
            var tracesRequests = await WaitForTestAgentData($"http://{_testAgentHost}:4318/test/session/traces");
            tracesRequests.Should().NotBeNullOrEmpty();

            // All keys are camelCase because the DD SDK exports http/json
            const string resourceSpansKey = "resourceSpans";
            const string scopeSpansKey = "scopeSpans";
            const string stringValueKey = "stringValue";
            const string intValueKey = "intValue";
            const string traceIdKey = "traceId";
            const string spanIdKey = "spanId";
            const string parentSpanIdKey = "parentSpanId";
            const string startTimeUnixNanoKey = "startTimeUnixNano";
            const string endTimeUnixNanoKey = "endTimeUnixNano";
            const string timeUnixNanoKey = "timeUnixNano";

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
            {
                attribute["value"]![stringValueKey] = "sdk-version";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.name')]"))
            {
                attribute["value"]![stringValueKey] = "sdk-name";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..resource.attributes[?(@.key == 'git.commit.sha')]"))
            {
                attribute["value"]![stringValueKey] = "normalized-git-commit-sha";
            }

            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                span[startTimeUnixNanoKey] = "0";
                span[endTimeUnixNanoKey] = "0";
                span[traceIdKey] = "normalized-trace-id";
                span[spanIdKey] = "normalized-span-id";
                if (span[parentSpanIdKey] != null)
                {
                    span[parentSpanIdKey] = "normalized-parent-span-id";
                }
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'otel.trace_id')]"))
            {
                attribute["value"]![stringValueKey] = "normalized-otel-trace-id";
            }

            foreach (var attribute in tracesRequests.SelectTokens("$..spans[*].attributes[?(@.key == 'server.port')]"))
            {
                if (attribute["value"]![intValueKey] is not null)
                {
                    attribute["value"]![intValueKey] = "normalized-server-port";
                }
            }

            foreach (var @event in tracesRequests.SelectTokens("$..events[*]"))
            {
                ((JObject)@event).Remove(timeUnixNanoKey);
                ((JObject)@event).AddFirst(new JProperty(timeUnixNanoKey, "0"));
            }

            // DD SDK: assert resource attributes are consistent across all batches, consolidate
            // all span batches into a single request, and sort spans by name for stability.
            JToken previousResourceAttributes = null;
            foreach (var tracesRequest in tracesRequests)
            {
                tracesRequest[resourceSpansKey].Should().HaveCount(1);
                var resourceAttributes = tracesRequest[resourceSpansKey][0]["resource"]["attributes"];

                if (previousResourceAttributes == null)
                {
                    previousResourceAttributes = resourceAttributes;
                }
                else
                {
                    JToken.DeepEquals(previousResourceAttributes, resourceAttributes).Should().BeTrue();
                    previousResourceAttributes = resourceAttributes;
                }
            }

            JArray firstSpans = null;
            foreach (var tracesRequest in tracesRequests)
            {
                tracesRequest[resourceSpansKey][0][scopeSpansKey].Should().HaveCount(1);
                var spans = tracesRequest[resourceSpansKey][0][scopeSpansKey][0]["spans"] as JArray;

                if (firstSpans == null)
                {
                    firstSpans = spans;
                }
                else
                {
                    foreach (var span in spans)
                    {
                        firstSpans.Add(span);
                    }
                }
            }

            var sortedSpans = new JArray(firstSpans.OrderBy(s => s["name"]!.ToString()));
            tracesRequests[0][resourceSpansKey][0][scopeSpansKey][0]["spans"] = sortedSpans;
            var finalJson = tracesRequests[0].ToString(Formatting.Indented);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(finalJson, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        /// <summary>
        /// Clears the test-agent session, retrying if the agent is not yet ready.
        /// Ensures the OTLP HTTP endpoint is accepting connections before tests proceed.
        /// </summary>
        private static async Task ClearTestAgentSession(string testAgentHost, int maxRetries = 5, int delayMs = 1000)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"http://{testAgentHost}:4318/test/session/clear";

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }

            // Final attempt -- let it throw if it fails
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Polls the test-agent for data until non-empty results are returned or timeout is reached.
        /// The sample app exports data during shutdown, so there can be a brief delay
        /// between process exit and data appearing in the test-agent. The timeout is generous
        /// because first-time gRPC connections (TCP+HTTP/2+TLS handshake) plus tracer shutdown
        /// flushing can stack up on slower CI runners.
        /// </summary>
        private static async Task<JToken> WaitForTestAgentData(string url, int timeoutSeconds = 60, int pollIntervalMs = 500)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JToken.Parse(json);

                if (data.HasValues)
                {
                    return data;
                }

                await Task.Delay(pollIntervalMs);
            }

            // Final attempt -- return whatever we get so the caller's assertion shows the actual value
            var finalResponse = await httpClient.GetAsync(url);
            finalResponse.EnsureSuccessStatusCode();
            var finalJson = await finalResponse.Content.ReadAsStringAsync();
            return JToken.Parse(finalJson);
        }
    }
}
#endif
