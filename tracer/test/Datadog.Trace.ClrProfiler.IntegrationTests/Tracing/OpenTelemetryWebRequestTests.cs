// <copyright file="OpenTelemetryWebRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    // Covers the WebRequest system-test coverage for the OpenTelemetry HTTP Semantic Conventions
    // implementation, including:
    // - standard vs. unknown request methods
    // - 3xx/4xx/5xx status-to-error mapping (which differs from the Datadog default for 5xx)
    // - url.full credential/query redaction
    // Note: This intentionally only covers OTLP export (which is where the RFC requires typed attribute values).
    [UsesVerify]
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OpenTelemetryWebRequestTests : TracingIntegrationTest
    {
        private readonly Regex _exceptionStacktraceOtlp400Regex = new(@"stringValue"": ""System.Net.WebException: The remote server returned an error: \(400\) Bad Request.*""");
        private readonly Regex _exceptionStacktraceOtlp500Regex = new(@"stringValue"": ""System.Net.WebException: The remote server returned an error: \(500\) Internal Server Error.*""");

        public OpenTelemetryWebRequestTests(ITestOutputHelper output)
            : base("OpenTelemetry.WebRequest", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsWebRequest(metadataSchemaVersion);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RequiresDockerDependency", "true")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SubmitsOtlpTraces(bool openTelemetrySemanticsEnabled)
        {
            SetInstrumentationVerification();

            var names = OtlpFieldNames.For(isJson: false);
            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "127.0.0.1";

            await OtlpSnapshotHelper.ClearTestAgentSessionAsync(testAgentHost);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            // OTEL_TRACES_EXPORTER=otlp is what makes the Datadog SDK emit OTLP instead of msgpack.
            // Everything else is left at its default dd-trace-dotnet value.
            SetEnvironmentVariable("OTEL_TRACES_EXPORTER", "otlp");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:4318");

            var applicationStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            // Traces go to the test-agent over OTLP, but telemetry still goes to the mock agent
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

            var tracesRequests = await OtlpSnapshotHelper.WaitForTestAgentDataAsync($"http://{testAgentHost}:4318/test/session/traces");
            tracesRequests.Should().NotBeNullOrEmpty();

            // NormalizeSpans overwrites startTimeUnixNano with a fixed placeholder, so capture the
            // real value first (keyed by span reference) to sort chronologically afterward.
            var spanStartTimes = new Dictionary<JToken, long>(ReferenceEqualityComparer.Instance);
            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                spanStartTimes[span] = long.Parse(span[names.StartTimeUnixNano]!.ToString());
            }

            OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
            OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, applicationStartTimeUnixNano);
            OtlpSnapshotHelper.SortSpanAttributes(tracesRequests);

            // Sort chronologically first so the snapshot mirrors the order the sample application
            // issued its requests in, then by name/URL/JSON to keep ties (e.g. two requests to the
            // same endpoint with identical timestamp resolution) stable across runs.
            var merged = OtlpSnapshotHelper.MergeDatadogRequests(
                tracesRequests,
                names,
                spans => spans.OrderBy(s => spanStartTimes[s])
                              .ThenBy(s => s["name"]?.ToString() ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => OtlpSnapshotHelper.GetAttributeStringValue(s, names, "url.full", "http.url") ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => s.ToString(Formatting.None), StringComparer.Ordinal));

            var finalJson = merged.ToString(Formatting.Indented);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            OtlpSnapshotHelper.AddProtobufToJsonScrubbers(settings);
            settings.AddSimpleScrubber("\\r\\n", "\\n");
            settings.AddRegexScrubber(_exceptionStacktraceOtlp400Regex, @"stringValue"": ""System.Net.WebException: The remote server returned an error: (400) Bad Request.""");
            settings.AddRegexScrubber(_exceptionStacktraceOtlp500Regex, @"stringValue"": ""System.Net.WebException: The remote server returned an error: (500) Internal Server Error.""");

            var suffix = openTelemetrySemanticsEnabled ? "_OtelSemantics" : string.Empty;
#if NET5_0_OR_GREATER
            suffix += "_Net5";
#elif NETFRAMEWORK
            suffix += "_NetFramework";
#endif
            await Verifier.Verify(finalJson, settings)
                          .UseFileName($"{nameof(OpenTelemetryWebRequestTests)}.{nameof(SubmitsOtlpTraces)}{suffix}")
                          .DisableRequireUniquePrefix();

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.WebRequest);
            VerifyInstrumentation(processResult.Process);
        }
    }
}
