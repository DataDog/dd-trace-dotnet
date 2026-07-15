// <copyright file="AspNetCoreNetFrameworkTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public abstract class AspNetCoreNetFrameworkTestBase : TestHelper
    {
        private const ulong IncomingTraceId = AspNetCoreNetFrameworkTopology.IncomingTraceId;
        private const ulong IncomingParentId = AspNetCoreNetFrameworkTopology.IncomingParentId;
        private const string MongoResource = "find aspnet-core-net-framework-repro";

        private readonly AspNetCoreTestFixture _fixture;
        private readonly string _snapshotPrefix;

        protected AspNetCoreNetFrameworkTestBase(string sampleName, string snapshotPrefix, AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(sampleName, output)
        {
            _fixture = fixture;
            _snapshotPrefix = snapshotPrefix;
            _fixture.SetOutput(output);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, "true");
            SetEnvironmentVariable("DD_TRACE_ASPNETCORE_ENABLED", "true");
            SetEnvironmentVariable(ConfigurationKeys.HeaderTags, AspNetCoreNetFrameworkTopology.HeaderTags);
            SetEnvironmentVariable(ConfigurationKeys.PropagationStyleExtract, AspNetCoreNetFrameworkTopology.PropagationStyleExtract);
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "false");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task NoContextCreatesRootRequestAndMakesMongoDbAChild()
        {
            var spans = await AssertRequestAndMongoSpans(
                            headers: null,
                            expectedTraceId: null,
                            expectedParentId: null,
                            expectedBaggage: null,
                            expectedRequestHeader: null);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName($"{_snapshotPrefix}.Topology.Enabled");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task FeatureEnabledConnectsIncomingRequestAndMongoDbTopology()
        {
            var spans = await AssertRequestAndMongoSpans(
                            AspNetCoreNetFrameworkTopology.CreateIncomingHeaders(),
                            IncomingTraceId,
                            IncomingParentId,
                            expectedBaggage: null,
                            expectedRequestHeader: null);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(
                                  AspNetCoreNetFrameworkTopology.IncludeUpstreamSpan(spans),
                                  settings,
                                  AspNetCoreNetFrameworkTopology.OrderSpans)
                              .UseFileName($"{_snapshotPrefix}.PropagationTopology.Enabled");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task DatadogContextMakesMongoDbAChildOfRequest()
        {
            var headers = new Dictionary<string, string>
            {
                ["x-datadog-trace-id"] = IncomingTraceId.ToString(),
                ["x-datadog-parent-id"] = IncomingParentId.ToString(),
                ["x-datadog-sampling-priority"] = "1",
                ["baggage"] = "user.id=legacy-user",
                ["x-legacy-test-header"] = "header-value",
            };

            await AssertRequestAndMongoSpans(headers, IncomingTraceId, IncomingParentId, "legacy-user", "header-value");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task W3CContextMakesMongoDbAChildOfRequest()
        {
            var headers = new Dictionary<string, string>
            {
                ["traceparent"] = "00-000000000000000000000000075bcd15-000000003ade68b1-01",
                ["baggage"] = "user.id=legacy-user",
                ["x-legacy-test-header"] = "header-value",
            };

            await AssertRequestAndMongoSpans(headers, IncomingTraceId, IncomingParentId, "legacy-user", "header-value");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task B3ContextMakesMongoDbAChildOfRequest()
        {
            var headers = new Dictionary<string, string>
            {
                ["x-b3-traceid"] = "00000000075bcd15",
                ["x-b3-spanid"] = "000000003ade68b1",
                ["x-b3-sampled"] = "1",
                ["baggage"] = "user.id=legacy-user",
                ["x-legacy-test-header"] = "header-value",
            };

            await AssertRequestAndMongoSpans(headers, IncomingTraceId, IncomingParentId, "legacy-user", "header-value");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task BaggageIsActiveInApplicationCodeAndTaggedOnRequest()
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var headers = new Dictionary<string, string>
            {
                ["x-datadog-trace-id"] = IncomingTraceId.ToString(),
                ["x-datadog-parent-id"] = IncomingParentId.ToString(),
                ["x-datadog-sampling-priority"] = "1",
                ["baggage"] = "user.id=legacy-user",
            };
            var startTime = DateTimeOffset.UtcNow;

            using (var client = new HttpClient())
            using (var request = _fixture.CreateRequest(HttpMethod.Get, "/attribute/baggage/user.id", headers))
            using (var response = await client.SendAsync(request))
            {
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                (await response.Content.ReadAsStringAsync()).Should().Be("legacy-user");
            }

            var spans = await _fixture.Agent.WaitForSpansAsync(
                            count: 1,
                            timeoutInMilliseconds: 20_000,
                            minDateTime: startTime,
                            returnAllOperations: true);
            var requestSpan = spans.Single(span => span.Name == "aspnet_core.request" && span.TraceId == IncomingTraceId);

            requestSpan.ParentId.Should().Be(IncomingParentId);
            requestSpan.GetTag("baggage.user.id").Should().Be("legacy-user");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task UnhandledExceptionMarksRequestSpan()
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var startTime = DateTimeOffset.UtcNow;
            using (var request = _fixture.CreateRequest(HttpMethod.Get, "/error"))
            {
                var statusCode = await _fixture.SendHttpRequest(request);
                statusCode.Should().Be(HttpStatusCode.InternalServerError);
            }

            var spans = await _fixture.Agent.WaitForSpansAsync(
                            count: 1,
                            timeoutInMilliseconds: 20_000,
                            minDateTime: startTime,
                            returnAllOperations: true);
            var requestSpan = spans.Single(span => span.Name == "aspnet_core.request");

            requestSpan.Error.Should().Be(1);
            requestSpan.GetTag("http.status_code").Should().Be("500");
            requestSpan.GetTag("error.type").Should().Contain(nameof(InvalidOperationException));
            requestSpan.GetTag("error.msg").Should().Be("Unhandled request failure");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task RoutingHeadersAndStatusCodesMatchExpectedBehavior()
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var startTime = DateTimeOffset.UtcNow;
            await SendRequest("/attribute/items/42", HttpStatusCode.OK, new Dictionary<string, string> { ["x-legacy-test-header"] = "request-value" });
            await SendRequest("/attribute/response-header", HttpStatusCode.OK);
            await SendRequest("/ConventionalRoutes/Index", HttpStatusCode.OK);
            await SendRequest("/Admin/AreaRoutes/Index", HttpStatusCode.OK);
            await SendRequest("/missing-route", HttpStatusCode.NotFound);
            await SendRequest("/attribute/error", HttpStatusCode.InternalServerError);

            var spans = await _fixture.Agent.WaitForSpansAsync(
                            count: 6,
                            timeoutInMilliseconds: 20_000,
                            minDateTime: startTime,
                            returnAllOperations: true);
            var requestSpans = spans.Where(span => span.Name == "aspnet_core.request").ToImmutableList();
            requestSpans.Should().HaveCount(6);

            var attributeSpan = GetSpan(requestSpans, "GET attribute/items/{id}");
            AssertMvcTags(attributeSpan, "attribute/items/{id}", "AttributeRoutes", "Item", area: null);
            attributeSpan.GetTag("legacy.request.header").Should().Be("request-value");
            attributeSpan.GetTag("http.status_code").Should().Be("200");
            attributeSpan.GetTag("http.url").Should().EndWith("/attribute/items/42");
            attributeSpan.GetTag("http.useragent").Should().Be("testhelper");

            var responseHeaderSpan = GetSpan(requestSpans, "GET attribute/response-header");
            AssertMvcTags(responseHeaderSpan, "attribute/response-header", "AttributeRoutes", "AddResponseHeader", area: null);
            responseHeaderSpan.GetTag("legacy.response.header").Should().Be("response-value");
            responseHeaderSpan.GetTag("http.status_code").Should().Be("200");

            var conventionalSpan = GetSpan(requestSpans, "GET ConventionalRoutes/Index");
            AssertMvcTags(conventionalSpan, "ConventionalRoutes/Index", "ConventionalRoutes", "Index", area: null);
            conventionalSpan.GetTag("http.status_code").Should().Be("200");

            var areaSpan = GetSpan(requestSpans, "GET Admin/AreaRoutes/Index");
            AssertMvcTags(areaSpan, "Admin/AreaRoutes/Index", "AreaRoutes", "Index", "Admin");
            areaSpan.GetTag("http.status_code").Should().Be("200");

            var notFoundSpan = GetSpan(requestSpans, "GET /missing-route");
            notFoundSpan.GetTag("aspnet_core.route").Should().BeNull();
            notFoundSpan.GetTag("http.status_code").Should().Be("404");
            notFoundSpan.Error.Should().Be(0);

            var errorSpan = GetSpan(requestSpans, "GET attribute/error");
            AssertMvcTags(errorSpan, "attribute/error", "AttributeRoutes", "Error", area: null);
            errorSpan.GetTag("http.status_code").Should().Be("500");
            errorSpan.Error.Should().Be(1);
            errorSpan.GetTag("error.type").Should().Contain(nameof(InvalidOperationException));
            errorSpan.GetTag("error.msg").Should().Be("Unhandled MVC request failure");

            var settings = VerifyHelper.GetSpanVerifierSettings();
            await VerifyHelper.VerifySpans(requestSpans, settings)
                              .UseFileName($"{_snapshotPrefix}.MvcBehavior.Enabled");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task ConcurrentRequestsKeepTheirOwnContextAndTags()
        {
            const ulong firstTraceId = 111111111;
            const ulong firstParentId = 222222222;
            const ulong secondTraceId = 333333333;
            const ulong secondParentId = 444444444;

            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var startTime = DateTimeOffset.UtcNow;
            using (var firstRequest = _fixture.CreateRequest(HttpMethod.Get, "/attribute/delay/200", CreateDatadogHeaders(firstTraceId, firstParentId, "first")))
            using (var secondRequest = _fixture.CreateRequest(HttpMethod.Get, "/attribute/delay/50", CreateDatadogHeaders(secondTraceId, secondParentId, "second")))
            {
                var responses = await Task.WhenAll(
                                    _fixture.SendHttpRequest(firstRequest),
                                    _fixture.SendHttpRequest(secondRequest));
                responses.Should().OnlyContain(statusCode => statusCode == HttpStatusCode.OK);
            }

            var spans = await _fixture.Agent.WaitForSpansAsync(
                            count: 2,
                            timeoutInMilliseconds: 20_000,
                            minDateTime: startTime,
                            returnAllOperations: true);
            var requestSpans = spans.Where(span => span.Name == "aspnet_core.request").ToImmutableList();
            requestSpans.Should().HaveCount(2);

            AssertConcurrentRequest(requestSpans, firstTraceId, firstParentId, "first");
            AssertConcurrentRequest(requestSpans, secondTraceId, secondParentId, "second");
        }

        public override void Dispose()
        {
            _fixture.SetOutput(null);
            base.Dispose();
        }

        private async Task<IImmutableList<MockSpan>> AssertRequestAndMongoSpans(
            Dictionary<string, string> headers,
            ulong? expectedTraceId,
            ulong? expectedParentId,
            string expectedBaggage,
            string expectedRequestHeader)
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var spans = await SendRequestAndWaitForMongoSpan("/baseline/mongo?item=42", headers, expectedTraceId);
            var requestSpan = spans.Single(
                span => span.Name == "aspnet_core.request"
                     && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value));
            var mongoSpan = spans.Single(span => IsMongoQuerySpan(span) && span.TraceId == requestSpan.TraceId);

            requestSpan.Resource.Should().Be("GET /baseline/mongo");
            requestSpan.TraceId.Should().NotBe(0);
            requestSpan.ParentId.Should().Be(expectedParentId);
            requestSpan.GetTag("span.kind").Should().Be("server");
            requestSpan.GetTag("component").Should().Be("aspnet_core");
            requestSpan.GetTag("http.method").Should().Be("GET");
            requestSpan.GetTag("http.status_code").Should().Be("200");
            requestSpan.GetTag("baggage.user.id").Should().Be(expectedBaggage);
            requestSpan.GetTag("legacy.request.header").Should().Be(expectedRequestHeader);
            requestSpan.GetTag("http.url").Should().EndWith("/baseline/mongo?item=42");

            mongoSpan.TraceId.Should().Be(requestSpan.TraceId);
            mongoSpan.ParentId.Should().Be(requestSpan.SpanId);

            return spans.Where(span => span.TraceId == requestSpan.TraceId).ToImmutableList();
        }

        private Dictionary<string, string> CreateDatadogHeaders(ulong traceId, ulong parentId, string correlationIdentifier) =>
            new Dictionary<string, string>
            {
                ["x-datadog-trace-id"] = traceId.ToString(),
                ["x-datadog-parent-id"] = parentId.ToString(),
                ["x-datadog-sampling-priority"] = "1",
                ["x-legacy-correlation-id"] = correlationIdentifier,
            };

        private MockSpan GetSpan(IEnumerable<MockSpan> spans, string resource) =>
            spans.Single(span => span.Resource == resource);

        private void AssertMvcTags(MockSpan span, string route, string controller, string action, string area)
        {
            span.GetTag("aspnet_core.route").Should().Be(route);
            span.GetTag("http.route").Should().Be(route);
            span.GetTag("aspnet_core.controller").Should().Be(controller);
            span.GetTag("aspnet_core.action").Should().Be(action);
            span.GetTag("aspnet_core.area").Should().Be(area);
            span.GetTag("http.method").Should().Be("GET");
        }

        private void AssertConcurrentRequest(
            IEnumerable<MockSpan> spans,
            ulong traceId,
            ulong parentId,
            string correlationIdentifier)
        {
            var span = spans.Single(candidate => candidate.TraceId == traceId);
            span.ParentId.Should().Be(parentId);
            span.Resource.Should().Be("GET attribute/delay/{milliseconds:int}");
            span.GetTag("http.request.headers.x-legacy-correlation-id").Should().Be(correlationIdentifier);
            span.GetTag("http.response.headers.x-legacy-correlation-id").Should().Be(correlationIdentifier);
            span.GetTag("http.status_code").Should().Be("200");
        }

        private async Task SendRequest(string path, HttpStatusCode expectedStatusCode, Dictionary<string, string> headers = null)
        {
            using (var request = _fixture.CreateRequest(HttpMethod.Get, path, headers))
            {
                var statusCode = await _fixture.SendHttpRequest(request);
                statusCode.Should().Be(expectedStatusCode);
            }
        }

        private async Task<IImmutableList<MockSpan>> SendRequestAndWaitForMongoSpan(
            string path,
            Dictionary<string, string> headers,
            ulong? expectedTraceId)
        {
            var startTime = DateTimeOffset.UtcNow;
            using (var request = _fixture.CreateRequest(HttpMethod.Get, path, headers))
            {
                var statusCode = await _fixture.SendHttpRequest(request);
                statusCode.Should().Be(HttpStatusCode.OK);
            }

            var deadline = DateTime.UtcNow.AddSeconds(20);
            IImmutableList<MockSpan> spans = ImmutableList<MockSpan>.Empty;
            do
            {
                spans = await _fixture.Agent.WaitForSpansAsync(
                            count: 2,
                            timeoutInMilliseconds: 500,
                            minDateTime: startTime,
                            returnAllOperations: true,
                            assertExpectedCount: false);

                if (spans.Any(
                        requestSpan => requestSpan.Name == "aspnet_core.request"
                                    && (!expectedTraceId.HasValue || requestSpan.TraceId == expectedTraceId.Value)
                                    && spans.Any(
                                           mongoSpan => IsMongoQuerySpan(mongoSpan)
                                                     && mongoSpan.TraceId == requestSpan.TraceId
                                                     && mongoSpan.ParentId == requestSpan.SpanId)))
                {
                    return spans;
                }
            }
            while (DateTime.UtcNow < deadline);

            spans.Should().Contain(
                span => span.Name == "aspnet_core.request"
                     && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value));
            var requestSpan = spans.Single(
                span => span.Name == "aspnet_core.request"
                     && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value));
            spans.Should().Contain(
                span => IsMongoQuerySpan(span)
                     && span.TraceId == requestSpan.TraceId
                     && span.ParentId == requestSpan.SpanId);
            return spans;
        }

        private bool IsMongoQuerySpan(MockSpan span) => span.Name == "mongodb.query" && span.Resource == MongoResource;
    }
}

#endif
