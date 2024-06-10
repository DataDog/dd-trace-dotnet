// <copyright file="AspNetVersionConflictTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.VersionConflict
{
    [Collection("IisTests")]
    public class AspNetVersionConflictTests : TestHelper, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;

        public AspNetVersionConflictTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNet.VersionConflict", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task SubmitsTraces()
        {
            // 4 spans for the base request: aspnet.request / aspnet-mvc.request / Manual / Manual-Inner / http.request
            // + 2 spans for the outgoing request: aspnet.request / aspnet-mvc.request
            const int expectedSpans = 8;

            var spans = await GetWebServerSpans("/home/sendrequest", _iisFixture.Agent, _iisFixture.HttpPort, System.Net.HttpStatusCode.OK, expectedSpans, filterServerSpans: false);

            foreach (var span in spans)
            {
                Output.WriteLine($"Name:{span.Name} - TraceId:{span.TraceId} - SpanID:{span.SpanId} - ParentId:{span.ParentId} - Resource:{span.Resource}");
            }

            spans.Should().HaveCount(expectedSpans);

            // Using Single to make sure there is no orphaned span
            var rootSpan = spans.Single(s => s.ParentId == null);

            rootSpan.Name.Should().Be("aspnet.request");
            rootSpan.Tags.Should().ContainKey(Tags.RuntimeId);

            var runtimeId = rootSpan.Tags[Tags.RuntimeId];
            Guid.TryParse(runtimeId, out _).Should().BeTrue();

            spans.Where(s => s.Name == "aspnet.request")
                 .Should()
                 .OnlyContain(s => s.Tags[Tags.RuntimeId] == runtimeId);

            var mvcSpan = spans.Single(s => s.ParentId == rootSpan.SpanId);

            mvcSpan.TraceId.Should().Be(rootSpan.TraceId);
            mvcSpan.Name.Should().Be("aspnet-mvc.request");

            var manualSpan = spans.Single(s => s.ParentId == mvcSpan.SpanId);

            manualSpan.TraceId.Should().Be(rootSpan.TraceId);
            manualSpan.Name.Should().Be("Manual");

            var manualInnerSpan = spans.Single(s => s.ParentId == manualSpan.SpanId);

            manualInnerSpan.TraceId.Should().Be(rootSpan.TraceId);
            manualInnerSpan.Name.Should().Be("Manual-Inner");

            var automaticOuterSpan = spans.Single(s => s.ParentId == manualInnerSpan.SpanId);

            automaticOuterSpan.TraceId.Should().Be(rootSpan.TraceId);
            automaticOuterSpan.Name.Should().Be("Automatic-Outer");
            automaticOuterSpan.Tags[Tags.RuntimeId].Should().Be(runtimeId);

            var httpSpan = spans.Single(s => s.ParentId == automaticOuterSpan.SpanId);

            httpSpan.TraceId.Should().Be(rootSpan.TraceId);
            httpSpan.Name.Should().Be("http.request");
            httpSpan.Tags[Tags.RuntimeId].Should().Be(runtimeId);
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task Sampling(bool parentTrace)
        {
            // 6 spans for the base request: aspnet.request / aspnet-mvc.request / Manual / http.request / http.request / Child
            // + 2 * 2 spans for the outgoing requests: aspnet.request / aspnet-mvc.request
            const int expectedSpans = 10;

            var spans = await GetWebServerSpans($"/home/sampling?parentTrace={parentTrace}", _iisFixture.Agent, _iisFixture.HttpPort, System.Net.HttpStatusCode.OK, expectedSpans, filterServerSpans: false);

            foreach (var span in spans)
            {
                var samplingPriority = string.Empty;

                if (span.Metrics.ContainsKey(Metrics.SamplingPriority))
                {
                    samplingPriority = span.Metrics[Metrics.SamplingPriority].ToString();
                }

                Output.WriteLine($"{span.Name} - {span.TraceId} - {span.SpanId} - {span.ParentId} - {span.Resource} - {samplingPriority}");
            }

            // Validate the correct hierarchy of spans
            var rootSpan = spans.Single(s => s.ParentId == null && s.Name == "aspnet.request");
            var mvcSpan = spans.Single(s => s.ParentId == rootSpan.SpanId);
            mvcSpan.TraceId.Should().Be(rootSpan.TraceId);
            mvcSpan.Name.Should().Be("aspnet-mvc.request");

            // The manual span will be in the same trace when parentTrace=true,
            // or the start of a new trace when parentTrace=false
            var manualSpan = spans.Single(s => s.Name == "Manual");
            var secondTraceId = parentTrace ? rootSpan.TraceId : manualSpan.TraceId;

            manualSpan.TraceId.Should().Be(secondTraceId);
            manualSpan.Name.Should().Be("Manual");

            var nestedSpans = spans.Where(s => s.ParentId == manualSpan.SpanId).ToArray();
            nestedSpans.Should()
                       .HaveCount(3)
                       .And.OnlyContain(s => s.TraceId == secondTraceId);

            nestedSpans.Where(s => s.Name == "http.request").Should().HaveCount(2);
            nestedSpans.Where(s => s.Name == "Child").Should().HaveCount(1);

            // Make sure there is no extra root span
            var rootTraces = spans.Where(s => s.ParentId == null).ToList();

            rootTraces.Should().HaveCount(parentTrace ? 1 : 2);

            // One HttpClient span should be UserKeep, the other UserReject
            var httpSpans = spans.Where(s => s.Name == "http.request");
            httpSpans.Should()
                .HaveCount(2)
                .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == SamplingPriorityValues.UserKeep)
                .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == SamplingPriorityValues.UserReject);

            // Check that the sampling priority got propagated to the target service
            var targetSpans = spans.Where(s => s.Name == "aspnet.request" && s.ParentId != null);
            targetSpans.Should()
                .HaveCount(2)
                .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == SamplingPriorityValues.UserKeep)
                .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == SamplingPriorityValues.UserReject);

            // The sampling priority for the root trace should be UserReject
            // Depending on the parentTrace argument, the root trace is either the manual one or the automatic one
            var rootTrace = parentTrace ? rootTraces.Single() : rootTraces.First(s => s.Name == "Manual");
            rootTrace.Metrics[Metrics.SamplingPriority].Should().Be((double)SamplingPriority.UserReject);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]

        public async Task ParentScope()
        {
            // aspnet.request + aspnet-mvc.request
            const int expectedSpans = 2;

            var testStart = DateTime.UtcNow;
            using var client = new HttpClient();

            var response = await client.GetAsync($"http://localhost:{_iisFixture.HttpPort}/home/parentScope");
            var content = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, $"server returned an error: {content}");

            var spans = _iisFixture.Agent.WaitForSpans(expectedSpans, minDateTime: testStart, returnAllOperations: true);

            spans.Should().HaveCount(expectedSpans);

            var mvcSpan = spans.Single(s => s.Name == "aspnet-mvc.request");

            mvcSpan.Tags.Should().Contain(new KeyValuePair<string, string>("Test", "OK"));

            var rootSpan = spans.Single(s => s.Name == "aspnet.request");

            rootSpan.Metrics.Should().Contain(new KeyValuePair<string, double>(Metrics.SamplingPriority, SamplingPriorityValues.UserKeep));

            var result = JObject.Parse(content);

            result.Should().NotBeNull();

            result["OperationName"].Value<string>().Should().Be(mvcSpan.Name);
            result["ResourceName"].Value<string>().Should().Be(mvcSpan.Resource);
            result["ServiceName"].Value<string>().Should().Be(mvcSpan.Service);
        }

        // There is an issue in the TracingHttpModule that causes the parent trace to be locked, making the test useless
        // The code causing the issue is not executed when IIS is in classic mode, so using that as a workaround
        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetClassic);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}

#endif
