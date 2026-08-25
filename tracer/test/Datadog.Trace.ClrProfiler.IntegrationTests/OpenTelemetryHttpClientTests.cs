// <copyright file="OpenTelemetryHttpClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Google.Protobuf;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    // Covers the HTTP client system-test coverage for the OpenTelemetry HTTP Semantic Conventions
    // implementation, including:
    // - standard vs. unknown request methods
    // - 3xx/4xx/5xx status-to-error mapping (which differs from the Datadog default for 5xx)
    // - url.full credential/query redaction
    // Note: This intentionally only covers OTLP export (which is where the RFC requires typed attribute values).
    [UsesVerify]
    public class OpenTelemetryHttpClientTests : TracingIntegrationTest
    {
        public OpenTelemetryHttpClientTests(ITestOutputHelper output)
            : base("OpenTelemetry.HttpClient", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsHttpMessageHandler(metadataSchemaVersion);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SubmitsOtlpTraces(bool openTelemetrySemanticsEnabled)
        {
            SetInstrumentationVerification();

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            var applicationStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            // Traces go to the mock agent over OTLP, and telemetry goes to the same mock agent over
            // the Datadog protocol.
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            ConfigureOtlpExport($"http://127.0.0.1:{((MockTracerAgent.TcpUdpAgent)agent).Port}/v1/traces");

            using ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

            var relevantRequests = await agent.WaitForOtlpTraceRequestsAsync(count: 1);
            relevantRequests.Should().NotBeNullOrEmpty();

            // Merge and sort on the typed protobuf model first, while span start times are still
            // real -- NormalizeSpans (below) replaces them with a fixed placeholder.
            var mergedRequest = OtlpSnapshotHelper.MergeDatadogRequests(
                relevantRequests,
                spans => spans.OrderBy(s => s.StartTimeUnixNano)
                              .ThenBy(s => s.Name ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => OtlpSnapshotHelper.GetAttributeStringValue(s, "url.full", "http.url") ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => JsonFormatter.Default.Format(s), StringComparer.Ordinal));

            var tracesRequests = JToken.Parse(JsonFormatter.Default.Format(mergedRequest));
            var names = OtlpFieldNames.For(isJson: true);

            OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
            OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, applicationStartTimeUnixNano);
            OtlpSnapshotHelper.SortSpanAttributes(tracesRequests);

            var finalJson = tracesRequests.ToString(Formatting.Indented);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            // different TFMs use different underlying handlers, which we don't really care about for the snapshots
            settings.AddSimpleScrubber("System.Net.Http.SocketsHttpHandler", "System.Net.Http.HttpClientHandler");
            OtlpSnapshotHelper.AddProtobufToJsonScrubbers(settings);

            var suffix = openTelemetrySemanticsEnabled ? "_OtelSemantics" : string.Empty;
            await Verifier.Verify(finalJson, settings)
                          .UseFileName($"{nameof(OpenTelemetryHttpClientTests)}.{nameof(SubmitsOtlpTraces)}{suffix}")
                          .DisableRequireUniquePrefix();

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.HttpMessageHandler);
            VerifyInstrumentation(processResult.Process);
        }
    }
}
