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
    public class AspNetCoreNetFrameworkReproTests : TestHelper, IClassFixture<AspNetCoreTestFixture>
    {
        private const ulong IncomingTraceId = 123456789;
        private const ulong IncomingParentId = 987654321;
        private const string SqlResource = "SELECT 1";

        private readonly AspNetCoreTestFixture _fixture;

        public AspNetCoreNetFrameworkReproTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreNetFramework", output)
        {
            _fixture = fixture;
            _fixture.SetOutput(output);
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "true");
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

            var baselineSpans = await SendRequestAndWaitForSqlSpan("/baseline/sql", headers, expectedTraceId: null);
            var baselineSqlSpan = baselineSpans.Single(IsSqlQuerySpan);

            baselineSpans.Should().NotContain(span => span.Name == "aspnet_core.request");
            baselineSqlSpan.TraceId.Should().NotBe(IncomingTraceId);
            baselineSqlSpan.ParentId.Should().BeNull();

            var manualSpans = await SendRequestAndWaitForSqlSpan("/manual/sql", headers, IncomingTraceId);
            var requestSpan = manualSpans.Single(span => span.Name == "aspnet_core.request");
            var manualSqlSpan = manualSpans.Single(span => IsSqlQuerySpan(span) && span.TraceId == IncomingTraceId);

            requestSpan.Resource.Should().Be("GET /manual/sql");
            requestSpan.TraceId.Should().Be(IncomingTraceId);
            requestSpan.ParentId.Should().Be(IncomingParentId);
            requestSpan.GetTag("span.kind").Should().Be("server");
            requestSpan.GetTag("component").Should().Be("aspnet_core");
            requestSpan.GetTag("http.method").Should().Be("GET");
            requestSpan.GetTag("http.status_code").Should().Be("200");

            manualSqlSpan.TraceId.Should().Be(requestSpan.TraceId);
            manualSqlSpan.ParentId.Should().Be(requestSpan.SpanId);
        }

        public override void Dispose()
        {
            _fixture.SetOutput(null);
            base.Dispose();
        }

        private async Task<IImmutableList<MockSpan>> SendRequestAndWaitForSqlSpan(string path, Dictionary<string, string> headers, ulong? expectedTraceId)
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

                if (spans.Any(span => IsSqlQuerySpan(span) && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value)))
                {
                    return spans;
                }
            }
            while (DateTime.UtcNow < deadline);

            spans.Should().Contain(
                span => IsSqlQuerySpan(span) && (!expectedTraceId.HasValue || span.TraceId == expectedTraceId.Value),
                $"because {path} should generate a SQL query span");
            return spans;
        }

        private bool IsSqlQuerySpan(MockSpan span) => span.Name == "sql-server.query" && span.Resource == SqlResource;
    }
}

#endif
