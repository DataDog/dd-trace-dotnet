// <copyright file="AspNetVersionConflictTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNet
{
    [Collection("IisTests")]
    public class AspNetVersionConflictTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetVersionConflictTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNet.VersionConflict", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            // There is an issue in the TracingHttpModule that causes the parent trace to be locked, making the test useless
            // This code is not run when IIS is in classic mode, so using that as a workaround
            _iisFixture.TryStartIis(this, IisAppType.AspNetClassic);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task SubmitsTraces()
        {
            // 4 spans for the base request: aspnet.request / aspnet-mvc.request / Manual / http.request
            // + 2 spans for the outgoing request: aspnet.request / aspnet-mvc.request
            const int expectedSpans = 6;

            var spans = await GetWebServerSpans("/home/sendrequest", _iisFixture.Agent, _iisFixture.HttpPort, System.Net.HttpStatusCode.OK, expectedSpans, filterServerSpans: false);

            spans.Should().HaveCount(expectedSpans);

            foreach (var span in spans)
            {
                Output.WriteLine($"{span.Name} - {span.TraceId} - {span.SpanId} - {span.ParentId} - {span.Resource}");
            }

            // Using Single to make sure there is no orphaned span
            var rootSpan = spans.Single(s => s.ParentId == null);

            rootSpan.Name.Should().Be("aspnet.request");

            var mvcSpan = spans.Single(s => s.ParentId == rootSpan.SpanId);

            mvcSpan.TraceId.Should().Be(rootSpan.TraceId);
            mvcSpan.Name.Should().Be("aspnet-mvc.request");

            var manualSpan = spans.Single(s => s.ParentId == mvcSpan.SpanId);

            manualSpan.TraceId.Should().Be(rootSpan.TraceId);
            manualSpan.Name.Should().Be("Manual");

            var httpSpan = spans.Single(s => s.ParentId == manualSpan.SpanId);

            httpSpan.TraceId.Should().Be(rootSpan.TraceId);
            httpSpan.Name.Should().Be("http.request");
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

            // Make sure there is no extra root span
            spans.Where(s => s.ParentId == null).Should().HaveCount(parentTrace ? 1 : 2);

            // The sampling priority should be UserKeep for all spans
            spans.Should().OnlyContain(s => VerifySpan(s, parentTrace));
        }

        private static bool VerifySpan(MockTracerAgent.Span span, bool parentTrace)
        {
            if (!span.Metrics.ContainsKey(Metrics.SamplingPriority))
            {
                return true;
            }

            if (!parentTrace)
            {
                // The root asp.net trace has an automatic priority
                if (span.Name == "aspnet.request" && span.Resource == "GET /home/sampling")
                {
                    return span.Metrics[Metrics.SamplingPriority] == 1;
                }
            }

            return span.Metrics[Metrics.SamplingPriority] == 2;
        }
    }
}

#endif
