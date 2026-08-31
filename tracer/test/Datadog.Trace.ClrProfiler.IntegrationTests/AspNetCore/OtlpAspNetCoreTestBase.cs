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
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    /// <summary>
    /// Shared harness for HTTP server suites hosted by <c>AspNetCoreTestFixture</c> (a Kestrel
    /// process) and exported over OTLP to the ddapm test-agent. Mirrors
    /// <c>OpenTelemetryAspNetTestBase</c>, which does the same for IIS-hosted .NET Framework samples;
    /// both build on the fixture-agnostic <see cref="OtlpTestAgentSession"/>, since none of the
    /// session/isolation/normalization plumbing depends on how the application under test was
    /// started.
    /// </summary>
    [UsesVerify]
    public abstract class OtlpAspNetCoreTestBase : TestHelper, IClassFixture<AspNetCoreTestFixture>, IAsyncLifetime
    {
        /// <summary>
        /// Temporary property used to carry each span's real start time through normalization and
        /// merging, so the spans can still be ordered chronologically afterwards.
        /// </summary>
        private const string StartTimeKey = "__startTimeUnixNano";

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

            // Set RouteTemplateResourceNamesEnabled and SingleSpanAspNetCoreEnabled according to the test configuration, which will affect how spans under Datadog semantics are named and structured.
            // Under OpenTelemetry semantics, TracerSettings force-enables both, so there should be no changes to the resulting spans.
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, (flags == AspNetCoreFeatureFlags.RouteTemplateResourceNames).ToString());
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled, (flags == AspNetCoreFeatureFlags.SingleSpan).ToString());

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            // Registers the empty route template and the pre-routing path rewrite that the last two
            // rows of Data() exercise. Only this harness asks for them, so the sample applications'
            // pipelines are unchanged for every other suite.
            SetEnvironmentVariable("ADD_ROUTE_EDGE_CASES", "1");

            // OTEL_TRACES_EXPORTER=otlp is what makes the Datadog SDK emit OTLP instead of msgpack.
            // Everything else is left at its default dd-trace-dotnet value.
            ConfigureOtlpExport(fixture.OtlpSession);

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
        };

        /// <summary>
        /// xUnit runs this once per test case, since it builds a fresh instance of the test class for
        /// each one, so the actual startup goes through the fixture's once-per-class instance.
        /// The result is that only the first test case needs to pay for the availability check and the warm-up.
        /// </summary>
        public Task InitializeAsync()
            => Fixture.EnsureInitializedAsync(StartApplicationAsync);

        public async Task DisposeAsync()
        {
            Fixture.SetOutput(null);

            // Clear the session at the end of the test to avoid leaking spans between test cases.
            if (Fixture.OtlpSession.IsAvailable)
            {
                await Fixture.OtlpSession.ClearSessionAsync();
            }
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
            Skip.IfNot(Fixture.OtlpSession.IsAvailable, $"The ddapm test-agent is not reachable at {Fixture.OtlpSession.TracesUrl}.");

            var names = OtlpFieldNames.For(isJson: false);

            // DisposeAsync already clears after every case, but clear again to ensure that
            // spans still in-flight due to a previous failure do not leak into the next test case
            await Fixture.OtlpSession.ClearSessionAsync();

            // Captured before the request is sent, so it is a lower bound for every span the server
            // creates while handling it.
            var testStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            await SendRequestAsync(httpMethod, path, (HttpStatusCode)statusCode);

            var tracesRequests = await Fixture.OtlpSession.WaitForSpansAsync(expectedSpanCount, testStartTimeUnixNano, names.StartTimeUnixNano);
            tracesRequests.Should().NotBeNullOrEmpty();
            OtlpTestAgentSession.CountSpans(tracesRequests).Should().Be(expectedSpanCount);

            // Stash the real start time on each span so that the chronological ordering below survives
            // both NormalizeSpans, which replaces startTimeUnixNano with a fixed placeholder, and
            // MergeDatadogRequests, which clones the tokens as it re-parents the spans of every
            // subsequent export into the first one. The property is removed again before returning.
            foreach (var span in tracesRequests.SelectTokens("$..spans[*]"))
            {
                ((JObject)span)[StartTimeKey] = span[names.StartTimeUnixNano]!.ToString();
            }

            OtlpSnapshotHelper.NormalizeResourceAttributes(tracesRequests, names);
            OtlpSnapshotHelper.NormalizeSpans(tracesRequests, names, testStartTimeUnixNano);
            OtlpSnapshotHelper.NormalizeCodeOriginAttributes(tracesRequests);

            foreach (var key in UnstableAttributeKeys)
            {
                NormalizeAttributeValues(tracesRequests, key, $"normalized-{key}");
            }

            OtlpSnapshotHelper.SortSpanAttributes(tracesRequests);

            // Sort chronologically first so the snapshot mirrors the nesting the server produced, then
            // by name/path/JSON to keep ties stable across runs.
            var merged = OtlpSnapshotHelper.MergeDatadogRequests(
                tracesRequests,
                names,
                spans => spans.OrderBy(s => long.Parse(s[StartTimeKey]!.ToString()))
                              .ThenBy(s => s["name"]?.ToString() ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => OtlpSnapshotHelper.GetAttributeStringValue(s, names, "url.path", "http.url") ?? string.Empty, StringComparer.Ordinal)
                              .ThenBy(s => s.ToString(Formatting.None), StringComparer.Ordinal));

            foreach (var span in merged.SelectTokens("$..spans[*]"))
            {
                ((JObject)span).Remove(StartTimeKey);
            }

            return merged;
        }

        private async Task SendRequestAsync(string httpMethod, string path, HttpStatusCode expectedStatusCode)
        {
            var request = Fixture.CreateRequest(new HttpMethod(httpMethod), path);
            var statusCode = await Fixture.SendHttpRequest(request);
            statusCode.Should().Be(expectedStatusCode);
        }

        /// <summary>
        /// Brings up the application the whole test class shares. Runs once per class, through
        /// <see cref="AspNetCoreTestFixture.EnsureInitializedAsync"/>, so it reads the environment
        /// variables the first test case's constructor set - which is also why it can't be a fixture
        /// <c>InitializeAsync</c>, as those are not set until a test class instance exists.
        /// </summary>
        private async Task StartApplicationAsync()
        {
            if (!await Fixture.OtlpSession.CheckAvailabilityAsync(Output))
            {
                // Don't pay for starting the sample app for a test that is about to skip.
                return;
            }

            // sendHealthCheck: false because AspNetCoreTestFixture's own health check waits for a
            // span to reach the mock agent, and OTEL_TRACES_EXPORTER=otlp sends traces to the ddapm
            // test-agent instead. Warm the app up with our own request and discard its spans afterwards.
            await Fixture.TryStartApp(this, sendHealthCheck: false);
            await WarmUpApplicationAsync();

            // Clear the session so the warm-up request is not returned in the next test case.
            await Fixture.OtlpSession.ClearSessionWhenQuietAsync(Output);
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
