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
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public abstract class AspNetCoreNetFrameworkTestBase : TestHelper
    {
        private const ulong IncomingTraceId = 123456789;
        private const ulong IncomingParentId = 987654321;
        private const string MongoResource = "find aspnet-core-net-framework-repro";

        private readonly AspNetCoreTestFixture _fixture;

        protected AspNetCoreNetFrameworkTestBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(sampleName, output)
        {
            _fixture = fixture;
            _fixture.SetOutput(output);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, "true");
            SetEnvironmentVariable(ConfigurationKeys.HeaderTags, "x-legacy-test-header:legacy.request.header");
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "false");
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

            await AssertRequestAndMongoSpans(headers);
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

            await AssertRequestAndMongoSpans(headers);
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

        public override void Dispose()
        {
            _fixture.SetOutput(null);
            base.Dispose();
        }

        private async Task AssertRequestAndMongoSpans(Dictionary<string, string> headers)
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var spans = await SendRequestAndWaitForMongoSpan("/baseline/mongo?item=42", headers);
            var requestSpan = spans.Single(span => span.Name == "aspnet_core.request" && span.TraceId == IncomingTraceId);
            var mongoSpan = spans.Single(span => IsMongoQuerySpan(span) && span.TraceId == IncomingTraceId);

            requestSpan.Resource.Should().Be("GET /baseline/mongo");
            requestSpan.ParentId.Should().Be(IncomingParentId);
            requestSpan.GetTag("span.kind").Should().Be("server");
            requestSpan.GetTag("component").Should().Be("aspnet_core");
            requestSpan.GetTag("http.method").Should().Be("GET");
            requestSpan.GetTag("http.status_code").Should().Be("200");
            requestSpan.GetTag("baggage.user.id").Should().Be("legacy-user");
            requestSpan.GetTag("legacy.request.header").Should().Be("header-value");
            requestSpan.GetTag("http.url").Should().EndWith("/baseline/mongo?item=42");

            mongoSpan.ParentId.Should().Be(requestSpan.SpanId);
        }

        private async Task<IImmutableList<MockSpan>> SendRequestAndWaitForMongoSpan(string path, Dictionary<string, string> headers)
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

                if (spans.Any(span => IsMongoQuerySpan(span) && span.TraceId == IncomingTraceId)
                 && spans.Any(span => span.Name == "aspnet_core.request" && span.TraceId == IncomingTraceId))
                {
                    return spans;
                }
            }
            while (DateTime.UtcNow < deadline);

            spans.Should().Contain(span => IsMongoQuerySpan(span) && span.TraceId == IncomingTraceId);
            spans.Should().Contain(span => span.Name == "aspnet_core.request" && span.TraceId == IncomingTraceId);
            return spans;
        }

        private bool IsMongoQuerySpan(MockSpan span) => span.Name == "mongodb.query" && span.Resource == MongoResource;
    }
}

#endif
