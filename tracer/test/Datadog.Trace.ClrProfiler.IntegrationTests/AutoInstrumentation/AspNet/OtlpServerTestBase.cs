// <copyright file="OtlpServerTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// Shared harness for the .NET Framework HTTP server coverage of the OpenTelemetry HTTP Semantic
    /// Conventions: a long-lived sample application exporting over OTLP to the ddapm test-agent, one
    /// request per test case, and the response payload normalized so it can be snapshotted. Mirrors
    /// <c>OtlpAspNetCoreTestBase</c>, which does the same for the .NET Core samples; both drive the
    /// <see cref="OtlpTestAgentSession"/> their fixture owns, since none of the session/isolation
    /// plumbing depends on how the application under test was started.
    /// <para>
    /// How the application is started, where it listens, and which fixture owns its session are the
    /// only things that differ, so that is all a derived harness supplies:
    /// <see cref="OtlpAspNetTestBase"/> hosts a sample in IIS Express, while
    /// <c>OtlpOwinWebApi2Tests</c> self-hosts one with OWIN.
    /// </para>
    /// <para>
    /// Derived suites carry <c>[Collection(nameof(TestAgentOtlpCollection))]</c> because the session
    /// is shared with every other OTLP test reading from the same test agent, and are snapshotted
    /// under both semantics, so the pair of snapshots is the diff between the Datadog defaults and
    /// the OpenTelemetry conventions for the same request. This intentionally only covers OTLP
    /// export, which is where the RFC requires typed attribute values.
    /// </para>
    /// </summary>
    [UsesVerify]
    public abstract class OtlpServerTestBase : TestHelper, IAsyncLifetime
    {
        /// <summary>
        /// Temporary property used to carry each span's real start time through normalization and
        /// merging, so the spans can still be ordered chronologically afterwards.
        /// </summary>
        private const string StartTimeKey = "__startTimeUnixNano";

        /// <summary>
        /// Attributes whose values depend on the machine, the socket, or the checkout path rather than
        /// on the request. Normalized whichever value kind they arrive as, in the same way
        /// <see cref="OtlpSnapshotHelper.NormalizeSpans"/> normalizes <c>server.port</c>. The exception
        /// type and message are deliberately left alone: only the stack trace is unstable.
        /// <para>
        /// The <c>client.*</c> and <c>network.peer.*</c> keys are only emitted when client-IP
        /// collection is enabled, but are listed here so that enabling it doesn't turn into a snapshot
        /// that changes per run.
        /// </para>
        /// </summary>
        private static readonly string[] UnstableAttributeKeys =
        [
            "client.address",
            "client.port",
            "error.stack",
            "exception.stacktrace",
            "network.peer.address",
            "network.peer.port",
        ];

        /// <summary>
        /// The ddapm test-agent session the application under test exports to, owned by the fixture
        /// that starts it: the session token is baked into the application's environment when it
        /// starts, and the application outlives every test case in the class, so a session created
        /// per test case would stop matching what the running application actually sends.
        /// </summary>
        private readonly OtlpTestAgentSession _otlpSession;

        internal OtlpServerTestBase(string sampleAppName, string samplePathOverride, ITestOutputHelper output, string testName, bool openTelemetrySemanticsEnabled, OtlpTestAgentSession otlpSession)
            : base(sampleAppName, samplePathOverride, output)
        {
            _otlpSession = otlpSession;
            OpenTelemetrySemanticsEnabled = openTelemetrySemanticsEnabled;
            TestName = testName + (openTelemetrySemanticsEnabled ? ".OtelSemantics" : ".DatadogSemantics");

            SetServiceVersion("1.0.0");

            // The OpenTelemetry conventions require the low-cardinality route template in
            // "http.route", which is only tracked when route-template resource names are enabled and
            // route-template expansion is not.
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, "false");

            SetEnvironmentVariable("DD_TRACE_OTEL_SEMANTICS_ENABLED", openTelemetrySemanticsEnabled.ToString());

            // OTEL_TRACES_EXPORTER=otlp is what makes the Datadog SDK emit OTLP instead of msgpack.
            // Everything else is left at its default dd-trace-dotnet value.
            ConfigureOtlpExport(_otlpSession);
        }

        protected bool OpenTelemetrySemanticsEnabled { get; }

        /// <summary>
        /// Gets the prefix of the snapshot file names, which also identifies the suite.
        /// </summary>
        protected string TestName { get; }

        /// <summary>
        /// Gets the path hit until the application responds, to confirm it is up. The spans it
        /// produces are discarded before each test case runs.
        /// </summary>
        protected abstract string WarmupPath { get; }

        public async Task InitializeAsync()
        {
            if (!await _otlpSession.CheckAvailabilityAsync(Output))
            {
                // Don't pay for starting the application under test (which for IIS Express also means
                // installing into the GAC) for a test that is about to skip.
                return;
            }

            await StartApplicationAsync();
            await WarmUpApplicationAsync();
        }

        public virtual Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Gets the value of a span attribute as a string, whichever value kind it was reported as,
        /// or <c>null</c> when the span doesn't carry the attribute.
        /// </summary>
        /// <param name="span">The span to read.</param>
        /// <param name="key">The attribute key.</param>
        internal static string GetAttribute(JToken span, string key)
            => span.SelectTokens($"$.attributes[?(@.key == '{key}')].value.*")
                   .Select(v => v.ToString())
                   .FirstOrDefault();

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
        /// Starts the application under test, without the fixture's own health check: that waits for
        /// a span to reach the mock agent, and <c>OTEL_TRACES_EXPORTER=otlp</c> sends traces to the
        /// ddapm test-agent instead. <see cref="WarmUpApplicationAsync"/> takes its place.
        /// </summary>
        protected abstract Task StartApplicationAsync();

        /// <summary>
        /// Gets the absolute URL the application under test serves <paramref name="path"/> at.
        /// </summary>
        /// <param name="path">The path to request, relative to the application root.</param>
        protected abstract string GetRequestUrl(string path);

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
            // The IIS integration-test job doesn't filter on RequiresDockerDependency the way the
            // other jobs do, so it would run this test without a test-agent to export OTLP to.
            Skip.IfNot(_otlpSession.IsAvailable, $"The ddapm test-agent is not reachable at {_otlpSession.TracesUrl}.");

            var names = OtlpFieldNames.For(isJson: false);

            // Unlike the console-application OTLP tests, the application under test outlives each
            // test case, so drop everything the previous case and the warm-up request produced first.
            await _otlpSession.ClearSessionWhenQuietAsync(Output);

            // Captured before the request is sent, so it is a lower bound for every span the server
            // creates while handling it.
            var testStartTimeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();

            await SendRequestAsync(httpMethod, path, (HttpStatusCode)statusCode);

            var tracesRequests = await _otlpSession.WaitForSpansAsync(expectedSpanCount, testStartTimeUnixNano, names.StartTimeUnixNano);
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

            // Sort chronologically first so the snapshot mirrors the nesting the server produced (the
            // aspnet.request span, then the framework span inside it), then by name/path/JSON to keep
            // ties stable across runs.
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
            using var httpClient = new HttpClient();

            // disable tracing for this HttpClient request
            httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");

            // Pinned so "user_agent.original" is stable in the snapshots
            httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, "testhelper");

            var url = GetRequestUrl(path);
            using var request = CreateHttpRequestMessage(new HttpMethod(httpMethod), url, DateTimeOffset.UtcNow);
            using var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Output.WriteLine($"[http] {httpMethod} {url} -> {response.StatusCode} {content}");
            response.StatusCode.Should().Be(expectedStatusCode);
        }

        /// <summary>
        /// Sends requests until the application responds, because it was started without its own
        /// health check and so only waited for the port to be bound, not for the application to
        /// finish starting.
        /// </summary>
        private async Task WarmUpApplicationAsync()
        {
            var url = GetRequestUrl(WarmupPath);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");

            var deadline = DateTime.UtcNow.AddSeconds(60);

            while (true)
            {
                try
                {
                    using var response = await httpClient.GetAsync(url);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }

                    Output.WriteLine($"[webserver] warm-up request to {url} returned {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"[webserver] warm-up request to {url} failed: {ex.Message}");
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new InvalidOperationException($"Couldn't verify the application is ready to receive requests at {url}.");
                }

                await Task.Delay(500);
            }
        }
    }
}
#endif
