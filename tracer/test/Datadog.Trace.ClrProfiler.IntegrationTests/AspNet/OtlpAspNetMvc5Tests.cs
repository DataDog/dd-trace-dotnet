// <copyright file="OtlpAspNetMvc5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class OtlpAspNetMvc5TestsWithDatadogSemantics : OtlpAspNetMvc5Tests
    {
        public OtlpAspNetMvc5TestsWithDatadogSemantics(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    public class OtlpAspNetMvc5TestsWithOpenTelemetrySemantics : OtlpAspNetMvc5Tests
    {
        public OtlpAspNetMvc5TestsWithOpenTelemetrySemantics(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    /// <summary>
    /// Covers the HTTP server coverage for the OpenTelemetry HTTP Semantic Conventions
    /// implementation, using the ASP.NET MVC 5 sample hosted in IIS Express. One test case per
    /// endpoint, each exercising a different requirement from
    /// <see href="https://opentelemetry.io/docs/specs/semconv/http/http-spans/#http-server">the HTTP
    /// server conventions</see>: standard vs. unknown request methods, route templates as
    /// <c>http.route</c>, status-to-error mapping (which for server spans differs from client spans
    /// in the 4xx range), and <c>url.query</c> obfuscation.
    /// </summary>
    public abstract class OtlpAspNetMvc5Tests : OtlpAspNetTestBase
    {
        protected OtlpAspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output, bool openTelemetrySemanticsEnabled)
            : base(iisFixture, output, nameof(OtlpAspNetMvc5Tests), openTelemetrySemanticsEnabled)
        {
        }

        /// <summary>
        /// Gets one endpoint per OpenTelemetry HTTP server requirement, as (method, path, status code,
        /// Datadog-semantics span count, OpenTelemetry-semantics span count). With Datadog semantics
        /// the count is 2 for a request that reaches an MVC action (the <c>aspnet.request</c> span
        /// plus the <c>aspnet-mvc.request</c> span nested inside it) and 1 when no action runs,
        /// because the MVC span is created by the action invoker. With OpenTelemetry semantics the
        /// <c>aspnet-mvc.request</c> span is not created, because the conventions describe a single
        /// HTTP server span per request.
        /// </summary>
        public static TheoryData<string, string, int, int, int> Data => new()
        {
            // The baseline attribute set (http.request.method, url.path, url.scheme,
            // http.response.status_code, server.address, server.port, user_agent.original,
            // client.address, network.peer.address), with the low-cardinality route retained in
            // http.route, a "{method} {http.route}" span name, and no url.query.
            { "GET", "/delay/0", 200, 2, 1 },

            // url.query is reported when the request carries a query string.
            { "GET", "/delay/0?id=1", 200, 2, 1 },

            // ...and its sensitive values are obfuscated first.
            { "GET", "/delay/0?token=SUPER-SECRET-TOKEN-VALUE", 200, 2, 1 },

            // No controller for the matched route, so no MVC span is created and the server span is
            // the only record of the request. With OpenTelemetry semantics there is also no
            // http.route, so the span name must be just the method rather than the URI path.
            { "GET", "/not-a-registered-route/1", 404, 1, 1 },

            // A method outside RFC 9110 is reported as _OTHER, with the verb the client sent kept in
            // http.request.method_original, and the span name falls back to "HTTP".
            { "FOO", "/not-a-registered-route/1", 404, 1, 1 },

            // 3xx is not an error
            { "GET", "/statuscode/302", 302, 2, 1 },

            // 4xx is not an error on a server span, unlike on a client span.
            { "GET", "/statuscode/400", 400, 2, 1 },

            // 5xx is an error, with no exception involved.
            { "GET", "/statuscode/500", 500, 2, 1 },

            // An unhandled exception is an error too, and is recorded as an exception span event.
            { "GET", "/badrequest", 500, 2, 1 },

            // The sample's Application_Error calls HttpServerUtility.TransferRequest, which re-runs
            // the whole IIS pipeline for "/Error/Index" against a fresh HttpContext. With Datadog
            // semantics that second pipeline pass produces its own aspnet.request/aspnet-mvc.request
            // pair nested inside the original request's pair, for four spans. With OpenTelemetry
            // semantics there is a single server span per inbound request, so the transferred request
            // attaches to the span the original request started instead of nesting a second one
            // inside it: one span, named and routed from the original request, but carrying the
            // status code the transferred request produced.
            { "GET", "/badrequest?TransferRequest=true", 500, 4, 1 },

            // Same, but the transferred request ends with a status code of its own rather than
            // inheriting the 500 from the unhandled exception, which is what pins down that the
            // transferred request is still the one that stamps http.response.status_code onto the
            // span it does not own.
            { "GET", "/BadRequestWithStatusCode/401?TransferRequest=true", 401, 4, 1 },
        };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        // [Trait("RunOnWindows", "True")] // TODO: Run tests when our in-process test agent supports OTLP traces
        [Trait("LoadFromGAC", "True")]
        [Trait("RequiresDockerDependency", "true")]
        [MemberData(nameof(Data))]
        public Task SubmitsOtlpTraces(string httpMethod, string path, int statusCode, int datadogSpanCount, int otelSpanCount)
            => RunTestCaseAsync(httpMethod, path, statusCode, OpenTelemetrySemanticsEnabled ? otelSpanCount : datadogSpanCount);
    }
}
#endif
