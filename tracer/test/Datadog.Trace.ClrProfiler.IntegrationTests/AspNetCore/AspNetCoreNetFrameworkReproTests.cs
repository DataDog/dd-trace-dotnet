// <copyright file="AspNetCoreNetFrameworkReproTests.cs" company="Datadog">
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
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    public class AspNetCoreNetFrameworkReproTests : TestHelper, IClassFixture<AspNetCoreTestFixture>
    {
        private const ulong IncomingTraceId = 123456789;
        private const ulong IncomingParentId = 987654321;
        private const string MongoResource = "find aspnet-core-net-framework-repro";

        private readonly AspNetCoreTestFixture _fixture;

        public AspNetCoreNetFrameworkReproTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreNetFramework", output)
        {
            _fixture = fixture;
            _fixture.SetOutput(output);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task GateDisabledProducesNoRequestSpanAndPreservesManualReproduction()
        {
            await _fixture.TryStartApp(this, sendHealthCheck: false);

            var headers = new Dictionary<string, string>
            {
                ["x-datadog-trace-id"] = IncomingTraceId.ToString(),
                ["x-datadog-parent-id"] = IncomingParentId.ToString(),
                ["x-datadog-sampling-priority"] = "1",
            };

            var baselineSpans = await SendRequestAndWaitForMongoSpan("/baseline/mongo", headers, expectedTraceId: null);
            var baselineMongoSpan = baselineSpans.Single(IsMongoQuerySpan);

            baselineSpans.Should().NotContain(span => span.Name == "aspnet_core.request");
            baselineMongoSpan.TraceId.Should().NotBe(IncomingTraceId);
            baselineMongoSpan.ParentId.Should().BeNull();

            var manualSpans = await SendRequestAndWaitForMongoSpan("/manual/mongo", headers, IncomingTraceId);
            var requestSpan = manualSpans.Single(span => span.Name == "aspnet_core.request");
            var manualMongoSpan = manualSpans.Single(span => IsMongoQuerySpan(span) && span.TraceId == IncomingTraceId);

            requestSpan.Resource.Should().Be("GET /manual/mongo");
            requestSpan.TraceId.Should().Be(IncomingTraceId);
            requestSpan.ParentId.Should().Be(IncomingParentId);
            requestSpan.GetTag("span.kind").Should().Be("server");
            requestSpan.GetTag("component").Should().Be("aspnet_core");
            requestSpan.GetTag("http.method").Should().Be("GET");
            requestSpan.GetTag("http.status_code").Should().Be("200");

            manualMongoSpan.TraceId.Should().Be(requestSpan.TraceId);
            manualMongoSpan.ParentId.Should().Be(requestSpan.SpanId);
        }

        public override void Dispose()
        {
            _fixture.SetOutput(null);
            base.Dispose();
        }

        private async Task<IImmutableList<MockSpan>> SendRequestAndWaitForMongoSpan(string path, Dictionary<string, string> headers, ulong? expectedTraceId)
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
                            count: 1,
                            timeoutInMilliseconds: 500,
                            minDateTime: startTime,
                            returnAllOperations: true,
                            assertExpectedCount: false);

                if (spans.Any(span => IsMongoQuerySpan(span) && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value)))
                {
                    return spans;
                }
            }
            while (DateTime.UtcNow < deadline);

            spans.Should().Contain(
                span => IsMongoQuerySpan(span) && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value),
                $"because {path} should generate a MongoDB query span");
            return spans;
        }

        private bool IsMongoQuerySpan(MockSpan span) => span.Name == "mongodb.query" && span.Resource == MongoResource;
    }
}

#endif
