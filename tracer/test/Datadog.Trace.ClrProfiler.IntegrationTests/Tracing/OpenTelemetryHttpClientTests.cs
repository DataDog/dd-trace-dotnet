// <copyright file="OpenTelemetryHttpClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public class OpenTelemetryHttpClientTests : TracingIntegrationTest, IAsyncLifetime
    {
        private readonly OtlpTestAgentSession _otlpSession = new();

        public OpenTelemetryHttpClientTests(ITestOutputHelper output)
            : base("OpenTelemetry.HttpClient", output)
        {
            SetServiceVersion("1.0.0");
        }

        public async Task InitializeAsync() => await _otlpSession.CheckAvailabilityAsync(Output);

        public async Task DisposeAsync() => await _otlpSession.DisposeAsync();

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsHttpMessageHandler(metadataSchemaVersion);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RequiresDockerDependency", "true")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SubmitsOtlpTraces(bool openTelemetrySemanticsEnabled)
        {
            SetInstrumentationVerification();

            var names = OtlpFieldNames.For(isJson: false);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());
            ConfigureOtlpExport(_otlpSession);

            var applicationStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            // Traces go to the test-agent over OTLP, but telemetry still goes to the mock agent
            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

            var tracesRequests = await _otlpSession.WaitForTracesAsync();
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
