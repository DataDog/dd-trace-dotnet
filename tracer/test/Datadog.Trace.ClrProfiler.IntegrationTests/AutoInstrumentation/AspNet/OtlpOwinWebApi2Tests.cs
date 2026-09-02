// <copyright file="OtlpOwinWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class OtlpOwinWebApi2TestsDatadogSemantics : OtlpOwinWebApi2Tests
    {
        public OtlpOwinWebApi2TestsDatadogSemantics(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, openTelemetrySemanticsEnabled: false)
        {
        }
    }

    public class OtlpOwinWebApi2TestsOtelSemantics : OtlpOwinWebApi2Tests
    {
        public OtlpOwinWebApi2TestsOtelSemantics(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, openTelemetrySemanticsEnabled: true)
        {
        }
    }

    /// <summary>
    /// The self-hosted counterpart of <see cref="OtlpAspNetWebApi2Tests"/>, using the
    /// <c>Samples.Owin.WebApi2</c> sample. Without System.Web there is no <c>aspnet.request</c> span
    /// to enrich, so here the <c>aspnet-webapi.request</c> span is itself the one HTTP server span the
    /// <see href="https://opentelemetry.io/docs/specs/semconv/http/http-spans/#http-server">HTTP
    /// server conventions</see> describe, and has to carry the whole server attribute set. That is a
    /// separate code path from the IIS-hosted suites, which never reach it.
    /// </summary>
    public abstract class OtlpOwinWebApi2Tests : OtlpServerTestBase, IClassFixture<OwinFixture>
    {
        private readonly OwinFixture _fixture;

        protected OtlpOwinWebApi2Tests(OwinFixture fixture, ITestOutputHelper output, bool openTelemetrySemanticsEnabled)
            : base("Owin.WebApi2", fixture, samplePathOverride: null, output, nameof(OtlpOwinWebApi2Tests), openTelemetrySemanticsEnabled)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Gets one endpoint per requirement, as (path, status code, span count).
        /// </summary>
        public static TheoryData<string, string, int, int> Data => new()
        {
            // The baseline attribute set (http.request.method, url.path, url.scheme,
            // http.response.status_code, server.address, server.port, user_agent.original), with the
            // low-cardinality route retained in http.route, a "{method} {http.route}" span name, and
            // no url.query. Unlike the IIS-hosted samples there is no network.protocol.version: the
            // protocol the request arrived over is only reachable through System.Web.
            { "GET", "/api/delay/0", 200, 1 },

            // url.query is reported when the request carries a query string.
            { "GET", "/api/delay/0?id=1", 200, 1 },

            // ...and its sensitive values are obfuscated first.
            { "GET", "/api/delay/0?token=SUPER-SECRET-TOKEN-VALUE", 200, 1 },

            // A convention-based route, which resolves the template from the route table rather than
            // from an attribute.
            { "GET", "/api2/delay/0", 200, 1 },

            // A method outside RFC 9110 is reported as _OTHER, with the verb the client sent kept in
            // http.request.method_original, and the span name falls back to "HTTP".
            { "FOO", "/api2/delay/0", 405, 1 },

            // 3xx is not an error
            { "GET", "/api/statuscode/302", 302, 1 },

            // 4xx is not an error on a server span, unlike on a client span.
            { "GET", "/api/statuscode/400", 400, 1 },

            // 5xx is an error, with no exception involved.
            { "GET", "/api/statuscode/500", 500, 1 },

            // An unhandled exception rather than a status code, recorded as an exception span event.
            // The sample's IExceptionHandler is itself traced, which is the second span.
            { "GET", "/api/transient-failure/false", 500, 2 },
        };

        /// <inheritdoc />
        protected override string WarmupPath => "/alive-check";

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        // [Trait("RunOnWindows", "True")] // TODO: Run tests when our in-process test agent supports OTLP traces
        [Trait("RequiresDockerDependency", "true")]
        [MemberData(nameof(Data))]
        public Task SubmitsOtlpTraces(string httpMethod, string path, int statusCode, int expectedSpanCount)
            => RunTestCaseAsync(httpMethod, path, statusCode, expectedSpanCount);

        /// <inheritdoc />
        protected override Task StartServerAsync() => _fixture.TryStartApp(this, Output);

        /// <inheritdoc />
        protected override string GetRequestUrl(string path) => $"http://localhost:{_fixture.HttpPort}{path}";
    }
}
#endif
