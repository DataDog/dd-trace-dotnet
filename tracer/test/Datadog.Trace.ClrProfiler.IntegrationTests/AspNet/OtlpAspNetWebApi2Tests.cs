// <copyright file="OtlpAspNetWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetWebApi2TestsDatadogSemantics : OtlpAspNetWebApi2Tests
    {
        public OtlpAspNetWebApi2TestsDatadogSemantics(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    [Collection(nameof(TestAgentOtlpCollection))]
    public class OtlpAspNetWebApi2TestsOtelSemantics : OtlpAspNetWebApi2Tests
    {
        public OtlpAspNetWebApi2TestsOtelSemantics(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    /// <summary>
    /// The ASP.NET Web API 2 counterpart of <see cref="OtlpAspNetMvc5Tests"/>. Web API 2
    /// routes are attribute- or convention-based rather than MVC's <c>{controller}/{action}</c>, and
    /// the Web API span is created by a different hook point, so it needs its own coverage of the
    /// <see href="https://opentelemetry.io/docs/specs/semconv/http/http-spans/#http-server">HTTP
    /// server conventions</see>.
    /// </summary>
    public abstract class OtlpAspNetWebApi2Tests : OtlpAspNetTestBase
    {
        protected OtlpAspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool openTelemetrySemanticsEnabled)
            : base(iisFixture, output, nameof(OtlpAspNetWebApi2Tests), openTelemetrySemanticsEnabled)
        {
        }

        /// <summary>
        /// Gets one endpoint per requirement, as (method, path, status code, Datadog-semantics span
        /// count, OpenTelemetry-semantics span count). With Datadog semantics the count is 2 for a
        /// request that reaches a Web API action (the <c>aspnet.request</c> span plus the
        /// <c>aspnet-webapi.request</c> span nested inside it). With OpenTelemetry semantics the
        /// <c>aspnet-webapi.request</c> span is not created, because the conventions describe a single
        /// HTTP server span per request. The Datadog-semantics span counts match the ones
        /// <c>AspNetWebApi2Tests</c> already asserts.
        /// </summary>
        public static TheoryData<string, string, int, int, int> Data => new()
        {
            // The baseline attribute set (http.request.method, url.path, url.scheme,
            // http.response.status_code, server.address, server.port, user_agent.original,
            // client.address, network.peer.address), with the low-cardinality route retained in
            // http.route, a "{method} {http.route}" span name, and no url.query.
            { "GET", "/api/delay/0", 200, 2, 1 },

            // url.query is reported when the request carries a query string.
            { "GET", "/api/delay/0?id=1", 200, 2, 1 },

            // ...and its sensitive values are obfuscated first.
            { "GET", "/api/delay/0?token=SUPER-SECRET-TOKEN-VALUE", 200, 2, 1 },

            // A convention-based route, which resolves the template from the route table rather than
            // from an attribute.
            { "GET", "/api2/delay/0", 200, 2, 1 },

            // A method outside RFC 9110 is reported as _OTHER, with the verb the client sent kept in
            // http.request.method_original, and the span name falls back to "HTTP".
            { "FOO", "/api2/delay/0", 405, 2, 1 },

            // 3xx is not an error
            { "GET", "/api/statuscode/302", 302, 2, 1 },

            // 4xx is not an error on a server span, unlike on a client span.
            { "GET", "/api/statuscode/400", 400, 2, 1 },

            // 5xx is an error, with no exception involved.
            { "GET", "/api/statuscode/500", 500, 2, 1 },

            // An unhandled exception rather than a status code. The sample's IExceptionHandler is
            // itself traced, which is the extra (non-HTTP) span in the count.
            { "GET", "/api/transient-failure/false", 500, 3, 2 },

            // The sample's IExceptionHandler calls HttpServerUtility.TransferRequest, which re-runs
            // the whole IIS pipeline for "/api/statuscode/{value}" against a fresh HttpContext. With
            // Datadog semantics that second pipeline pass produces its own
            // aspnet.request/aspnet-webapi.request pair nested inside the original request's pair, for
            // four spans. With OpenTelemetry semantics there is a single server span per inbound
            // request, so the transferred request attaches to the span the original request started
            // rather than nesting a second one inside it: one span, named and routed from the original
            // request, but carrying the status code the transferred request produced.
            { "GET", "/api/TransferRequest/500", 500, 4, 1 },
            { "GET", "/api/TransferRequest/401", 401, 4, 1 },
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
