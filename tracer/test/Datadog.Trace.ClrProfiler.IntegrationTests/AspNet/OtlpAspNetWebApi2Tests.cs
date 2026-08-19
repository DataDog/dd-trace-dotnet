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
        /// Gets one endpoint per requirement, as (path, status code, route template, Datadog-semantics span
        /// count). With Datadog semantics the count is 2 for a request that reaches a Web API action
        /// (the <c>aspnet.request</c> span plus the <c>aspnet-webapi.request</c> span nested inside
        /// it). With OpenTelemetry semantics the <c>aspnet-webapi.request</c> span is not created,
        /// because the conventions describe a single HTTP server span per request, so the count is one
        /// lower. The span counts match the ones <c>AspNetWebApi2Tests</c> already asserts.
        /// </summary>
        public static TheoryData<string, string, int, int> Data => new()
        {
            // The baseline attribute set (http.request.method, url.path, url.scheme,
            // http.response.status_code, server.address, server.port, user_agent.original,
            // client.address, network.peer.address), with the low-cardinality route retained in
            // http.route, a "{method} {http.route}" span name, and no url.query.
            { "GET", "/api/delay/0", 200, 2 },

            // url.query is reported when the request carries a query string.
            { "GET", "/api/delay/0?id=1", 200, 2 },

            // ...and its sensitive values are obfuscated first.
            { "GET", "/api/delay/0?token=SUPER-SECRET-TOKEN-VALUE", 200, 2 },

            // A convention-based route, which resolves the template from the route table rather than
            // from an attribute.
            { "GET", "/api2/delay/0", 200, 2 },

            // A method outside RFC 9110 is reported as _OTHER, with the verb the client sent kept in
            // http.request.method_original, and the span name falls back to "HTTP".
            { "FOO", "/api2/delay/0", 405, 2 },

            // 3xx is not an error
            { "GET", "/api/statuscode/302", 302, 2 },

            // 4xx is not an error on a server span, unlike on a client span.
            { "GET", "/api/statuscode/400", 400, 2 },

            // 5xx is an error, with no exception involved.
            { "GET", "/api/statuscode/500", 500, 2 },

            // An unhandled exception rather than a status code. The sample's IExceptionHandler is
            // itself traced, which is the extra (non-HTTP) span in the count.
            { "GET", "/api/transient-failure/false", 500, 3 },
        };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        // [Trait("RunOnWindows", "True")] // TODO: Run tests when our in-process test agent supports OTLP traces
        [Trait("LoadFromGAC", "True")]
        [Trait("RequiresDockerDependency", "true")]
        [MemberData(nameof(Data))]
        public Task SubmitsOtlpTraces(string httpMethod, string path, int statusCode, int datadogSpanCount)
            => RunTestCaseAsync(httpMethod, path, statusCode, OpenTelemetrySemanticsEnabled ? datadogSpanCount - 1 : datadogSpanCount);
    }
}
#endif
