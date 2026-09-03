// <copyright file="OtlpAspNetCoreTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    /// <summary>
    /// Shared harness for HTTP server suites hosted by <c>AspNetCoreTestFixture</c> (a Kestrel
    /// process) and exported over OTLP to the in-process <c>MockTracerAgent</c> that the fixture
    /// already runs for Datadog-protocol traces. Mirrors <c>OpenTelemetryAspNetTestBase</c>, which
    /// does the same for IIS-hosted .NET Framework samples. Test-case isolation works exactly like
    /// the non-OTLP AspNetCore suites: <see cref="MockTracerAgent.WaitForOtlpSpansAsync"/> filters by
    /// a <c>minDateTime</c> captured right before each request, rather than clearing any shared state.
    /// </summary>
    [UsesVerify]
    public abstract class OtlpAspNetCoreTestBase : TestHelper, IClassFixture<AspNetCoreTestFixture>, IAsyncLifetime
    {
        /// <summary>
        /// Attributes whose values depend on the machine, the socket, or the checkout path rather than
        /// on the request. See the identical list in <c>OpenTelemetryAspNetTestBase</c>.
        /// </summary>
        private static readonly string[] UnstableAttributeKeys =
        {
            "client.address",
            "client.port",
            "error.stack",
            "exception.stacktrace",
            "network.peer.address",
            "network.peer.port",
        };

        protected OtlpAspNetCoreTestBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames, bool openTelemetrySemanticsEnabled)
            : this(sampleName, fixture, output, enableRouteTemplateResourceNames ? AspNetCoreFeatureFlags.RouteTemplateResourceNames : AspNetCoreFeatureFlags.None, openTelemetrySemanticsEnabled)
        {
        }

        protected OtlpAspNetCoreTestBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output, AspNetCoreFeatureFlags flags, bool openTelemetrySemanticsEnabled)
            : base(sampleName, output)
        {
            Flags = flags;
            OpenTelemetrySemanticsEnabled = openTelemetrySemanticsEnabled;

            SetServiceVersion("1.0.0");

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, (flags == AspNetCoreFeatureFlags.RouteTemplateResourceNames).ToString());
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled, (flags == AspNetCoreFeatureFlags.SingleSpan).ToString());

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            // Registers the empty route template and the pre-routing path rewrite that the last two
            // rows of Data() exercise. Only this harness asks for them, so the sample applications'
            // pipelines are unchanged for every other suite.
            SetEnvironmentVariable("ADD_ROUTE_EDGE_CASES", "1");

            Fixture = fixture;
            Fixture.SetOutput(output);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        /// <summary>
        /// Gets the ASP.NET Core feature flags the suite runs with.
        /// </summary>
        protected AspNetCoreFeatureFlags Flags { get; }

        protected bool OpenTelemetrySemanticsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether an <c>aspnet_core_mvc.request</c> child span accompanies
        /// the server span, which derived suites need in order to know how many spans to expect. Only
        /// the route-template resource names feature flag produces one, and OTel semantics collapses
        /// the pair back into a single server span.
        /// </summary>
        protected bool ProducesMvcChildSpan
            => Flags == AspNetCoreFeatureFlags.RouteTemplateResourceNames && !OpenTelemetrySemanticsEnabled;

        /// <summary>
        /// Gets or sets the prefix of the snapshot file names, which also identifies the suite.
        /// </summary>
        protected string TestName { get; set; }

        /// <summary>
        /// Gets the path hit once per test case to confirm the app is up. The spans it produces are
        /// discarded before each test case runs.
        /// </summary>
        protected virtual string WarmupPath => "/alive-check";

        /// <summary>
        /// The smallest set of requests that reaches every HTTP server span requirement in the
        /// OpenTelemetry HTTP semantic conventions RFC. The rows are (method, path, status code,
        /// whether an endpoint handled the request rather than middleware short-circuiting it);
        /// each suite turns that last column into a span count of its own, because what an endpoint
        /// is - an MVC action or a minimal-API delegate - differs per sample application.
        /// </summary>
        public static TheoryData<string, string, int, bool> Data() => new()
        {
            // The baseline attribute set (http.request.method, url.path, url.scheme,
            // http.response.status_code, server.address, server.port, user_agent.original,
            // client.address, network.peer.address), with the low-cardinality route retained in
            // http.route, a "{method} {http.route}" span name, and no url.query.
            { "GET", "/api/delay/0", 200, true },

            // url.query is reported when the request carries a query string.
            { "GET", "/api/delay/0?id=1", 200, true },

            // ...and its sensitive values are obfuscated first.
            { "GET", "/api/delay/0?token=SUPER-SECRET-TOKEN-VALUE", 200, true },

            // Answered by middleware before routing runs, so there is no http.route: the span name
            // has to be the bare method, never the URI path.
            { "GET", "/ping", 200, false },

            // A method outside RFC 9110 is reported as _OTHER, with the verb the client sent kept in
            // http.request.method_original, and the span name falls back to "HTTP".
            { "FOO", "/ping", 200, false },

            // 3xx is not an error.
            { "GET", "/status-code/302", 302, true },

            // 4xx is not an error on a server span, unlike on a client span.
            { "GET", "/status-code/400", 400, true },

            // 5xx is an error, with no exception involved.
            { "GET", "/status-code/500", 500, true },

            // An unhandled exception is an error too, and is recorded as an exception span event.
            { "GET", "/bad-request", 500, true },

            // -- ASP.NET Core route edge cases --

            // A route template that matches the application root is stored by ASP.NET Core as the
            // empty string, which must be reported as "/" rather than verbatim - otherwise
            // http.route is an empty attribute and the span name has a trailing space.
            { "GET", "/", 200, true },

            // Middleware rewrote the path before routing ran, so the endpoint that matched is not the
            // one the request arrived on. It is still the endpoint that served the request, so it is
            // the route to report.
            { "GET", "/rewrite-me", 200, true },

            // The application is mounted under a path base, which routing strips before matching.
            // http.route is reported without the path base, aligning with built-in ASP.NET Core and OTel .NET instrumentation.
            { "GET", "/path-base/api/delay/0", 200, true },
        };

        public async Task InitializeAsync()
        {
            // sendHealthCheck: false -- the fixture's health check waits for a Datadog-protocol span,
            // but this sample exports OTLP; warm up with our own request below instead.
            // onAgentCreated runs here (not the constructor) since TryStartApp creates a fresh agent
            // and port per launch attempt.
            await Fixture.TryStartApp(
                this,
                sendHealthCheck: false,
                onAgentCreated: agent => ConfigureOtlpExport($"http://127.0.0.1:{agent.Port}/v1/traces"));
            await WarmUpApplicationAsync();
        }

        public Task DisposeAsync()
        {
            Fixture.SetOutput(null);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends one request and returns the spans the server produced for it, in the order they were
        /// started and with the machine-dependent values normalized.
        /// </summary>
        /// <param name="httpMethod">The request method, which may be a non-standard one.</param>
        /// <param name="path">The path to request, relative to the application root.</param>
        /// <param name="statusCode">The status code the request is expected to return.</param>
        /// <param name="expectedSpanCount">The number of spans the request is expected to produce.</param>
        internal async Task<IReadOnlyList<JToken>> RunTestCaseAndGetSpansAsync(string httpMethod, string path, int statusCode, int expectedSpanCount)
        {
            var merged = await SendRequestAndCollectSpansAsync(httpMethod, path, statusCode, expectedSpanCount);
            return merged.SelectTokens("$..spans[*]").ToList();
        }

        /// <summary>
        /// Sends one request and snapshots every span the server produced for it.
        /// </summary>
        /// <param name="httpMethod">The request method, which may be a non-standard one.</param>
        /// <param name="path">The path to request, relative to the application root.</param>
        /// <param name="statusCode">The status code the request is expected to return.</param>
        /// <param name="expectedSpanCount">The number of spans the request is expected to produce.</param>
        protected async Task RunTestCaseAsync(string httpMethod, string path, int statusCode, int expectedSpanCount)
        {
            var merged = await SendRequestAndCollectSpansAsync(httpMethod, path, statusCode, expectedSpanCount);
            var finalJson = merged.ToString(Formatting.Indented);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            OtlpSnapshotHelper.AddProtobufToJsonScrubbers(settings);

            // Any multi-line value that survives (an exception message, for example) is rendered with
            // the line endings of the machine the test ran on.
            settings.AddSimpleScrubber("\\r\\n", "\\n");

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            await Verifier.Verify(finalJson, settings)
                          .UseFileName($"{TestName}.__method={httpMethod}_path={sanitisedPath}")
                          .DisableRequireUniquePrefix();
        }

        protected virtual string GetTestName(string testName)
        {
            if (OpenTelemetrySemanticsEnabled)
            {
                return testName + ".OtelSemantics";
            }

            return testName + ".DatadogSemantics" + Flags switch
            {
                AspNetCoreFeatureFlags.RouteTemplateResourceNames => ".WithFF",
                AspNetCoreFeatureFlags.SingleSpan => ".SingleSpan",
                _ => ".NoFF",
            };
        }

        /// <summary>
        /// Overwrites the value of an attribute, whichever value kind it arrived as, wherever it
        /// appears (on a span or on one of its events). A text scrubber can't reach the value through
        /// the OTLP key/value attribute shape, and the same tag is not always reported with the same
        /// value kind.
        /// </summary>
        private static void NormalizeAttributeValues(JToken tracesRequests, string key, string replacement)
        {
            foreach (var attribute in tracesRequests.SelectTokens($"$..attributes[?(@.key == '{key}')]"))
            {
                if (attribute["value"] is JObject value)
                {
                    foreach (var property in value.Properties())
                    {
                        property.Value = replacement;
                    }
                }
            }
        }

        private async Task<JToken> SendRequestAndCollectSpansAsync(string httpMethod, string path, int statusCode, int expectedSpanCount)
        {
            var names = OtlpFieldNames.For(isJson: true);

            // Capture the current span IDs before the request. We cannot use the fixture's usual
            // filtered wait helper here: the /rewrite-me edge case is rewritten to /alive-check and
            // would be mistaken for a health-check span. Selecting newly received IDs also prevents
            // a span from an earlier case, within MockTracerAgent's timestamp tolerance, leaking in.
            var existingSpanIds = Fixture.Agent.OtlpSpans.Select(s => s.SpanId).ToHashSet();
            var now = DateTimeOffset.UtcNow;
            var testStartTimeUnixNano = now.ToUnixTimeNanoseconds();

            await SendRequestAsync(httpMethod, path, (HttpStatusCode)statusCode);

            var deadline = DateTime.UtcNow.AddSeconds(20);
            var relevantSpanIds = Fixture.Agent.OtlpSpans
                                                .Where(s => !existingSpanIds.Contains(s.SpanId) && s.StartTimeUnixNano >= (ulong)testStartTimeUnixNano)
                                                .Select(s => s.SpanId)
                                                .ToHashSet();
            while (DateTime.UtcNow < deadline && relevantSpanIds.Count < expectedSpanCount)
            {
                await Task.Delay(250);
                relevantSpanIds = Fixture.Agent.OtlpSpans
                                                .Where(s => !existingSpanIds.Contains(s.SpanId) && s.StartTimeUnixNano >= (ulong)testStartTimeUnixNano)
                                                .Select(s => s.SpanId)
                                                .ToHashSet();
            }

            // An OTLP export batch can contain spans from more than one request, so trim each batch
            // to the IDs selected above before building this test case's snapshot.
            var relevantRequests = Fixture.Agent.OtlpTraceRequests
                                          .Select(r => Fixture.Agent.TrimOtlpTraceRequestToSpans(r, relevantSpanIds))
                                          .Where(r => r.Spans.Count > 0)
                                          .ToList();
            relevantRequests.Should().NotBeNullOrEmpty();
            relevantRequests.Sum(r => r.Spans.Count).Should().Be(expectedSpanCount);

            // Sort by actual start time before NormalizeSpans (below) overwrites it with a placeholder.
            var mergedRequest = OtlpSnapshotHelper.MergeDatadogRequests(
                relevantRequests,
                spans => spans.OrderBy(s => s.StartTimeUnixNano)
                              .ThenBy(s => s.Name ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => OtlpSnapshotHelper.GetAttributeStringValue(s, "url.path", "http.url") ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => JsonFormatter.Default.Format(s), StringComparer.Ordinal));

            var tracesRequests = JToken.Parse(JsonFormatter.Default.Format(mergedRequest));

            OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
            OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, testStartTimeUnixNano);
            OtlpSnapshotHelper.NormalizeCodeOriginAttributes(tracesRequests);

            foreach (var key in UnstableAttributeKeys)
            {
                NormalizeAttributeValues(tracesRequests, key, $"normalized-{key}");
            }

            OtlpSnapshotHelper.SortSpanAttributes(tracesRequests);

            return tracesRequests;
        }

        private async Task SendRequestAsync(string httpMethod, string path, HttpStatusCode expectedStatusCode)
        {
            var request = Fixture.CreateRequest(new HttpMethod(httpMethod), path);
            var statusCode = await Fixture.SendHttpRequest(request);
            statusCode.Should().Be(expectedStatusCode);
        }

        /// <summary>
        /// Sends requests until the app responds, because AspNetCoreTestFixture was started without
        /// its own health check and so only waited for the port to be bound, not for the app to
        /// finish starting.
        /// </summary>
        private async Task WarmUpApplicationAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);

            while (true)
            {
                try
                {
                    var request = Fixture.CreateRequest(HttpMethod.Get, WarmupPath);
                    var statusCode = await Fixture.SendHttpRequest(request);
                    if (statusCode == HttpStatusCode.OK)
                    {
                        return;
                    }

                    Output.WriteLine($"[webserver] warm-up request to {WarmupPath} returned {statusCode}");
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"[webserver] warm-up request to {WarmupPath} failed: {ex.Message}");
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new InvalidOperationException($"Couldn't verify the application is ready to receive requests at {WarmupPath}.");
                }

                await Task.Delay(500);
            }
        }
    }
}
#endif
