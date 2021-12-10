// <copyright file="VersionConflict2xTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.VersionConflict
{
    public class VersionConflict2xTests : TestHelper
    {
        public VersionConflict2xTests(ITestOutputHelper output)
            : base("VersionConflict.2x", output)
        {
        }

        [Fact]
        public void SubmitTraces()
        {
            // 1 manual span + 2 http spans
            const int expectedSpanCount = 3;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent.Port))
            {
                var spans = agent.WaitForSpans(expectedSpanCount);

                spans.Should().HaveCount(expectedSpanCount);

                // Check that no trace is orphaned
                var rootSpan = spans.Single(s => s.ParentId == null);

                rootSpan.Name.Should().Be("Manual");
                rootSpan.Metrics.Should().ContainKey(Metrics.SamplingPriority);
                rootSpan.Metrics[Metrics.SamplingPriority].Should().Be((double)SamplingPriority.UserReject);

                var httpSpans = spans.Where(s => s.Name == "http.request").ToList();

                httpSpans.Should()
                    .HaveCount(2)
                    .And.OnlyContain(s => s.ParentId == rootSpan.SpanId && s.TraceId == rootSpan.TraceId)
                    .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == (double)SamplingPriority.UserKeep)
                    .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == (double)SamplingPriority.UserReject);
            }
        }
    }
}
