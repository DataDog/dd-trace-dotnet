// <copyright file="VersionConflict2xTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
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

        [SkippableFact]
        public async Task SubmitTraces()
        {
            // 1 manual span + 2 http spans
            const int expectedSpanCount = 3;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(expectedSpanCount);

                foreach (var span in spans)
                {
                    var samplingPriority = string.Empty;

                    if (span.Metrics.ContainsKey(Metrics.SamplingPriority))
                    {
                        samplingPriority = span.Metrics[Metrics.SamplingPriority].ToString();
                    }

                    Output.WriteLine($"{span.Name} - {span.TraceId} - {span.SpanId} - {span.ParentId} - {span.Resource} - {samplingPriority}");
                }

                spans.Should().HaveCount(expectedSpanCount);

                // Check that no trace is orphaned
                var rootSpan = spans.Single(s => s.ParentId == null);

                rootSpan.Name.Should().Be("Manual");
                rootSpan.Metrics.Should().ContainKey(Metrics.SamplingPriority);
                rootSpan.Metrics[Metrics.SamplingPriority].Should().Be((double)SamplingPriority.UserReject);

                var httpSpans = spans.Where(s => s.Name == "http.request").ToList();

                // There is a difference in behavior between .NET Framework and .NET Core
                // This happens because the version of the nuget is higher than the version of the automatic tracer
                // When that's the case, only the nuget tracer is loaded, and we're not actually in a version conflict situation.
                // When version 2.1 of the tracer ships, we can go back to the test and add a case using nuget 2.0,
                // which will actually test the version conflict behavior.

#if NETCOREAPP
                httpSpans.Should()
                    .HaveCount(2)
                    .And.OnlyContain(s => s.ParentId == rootSpan.SpanId && s.TraceId == rootSpan.TraceId)
                    .And.OnlyContain(s => !s.Metrics.ContainsKey(Metrics.SamplingPriority));
#else
                httpSpans.Should()
                    .HaveCount(2)
                    .And.OnlyContain(s => s.ParentId == rootSpan.SpanId && s.TraceId == rootSpan.TraceId)
                    .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == SamplingPriorityValues.UserKeep)
                    .And.ContainSingle(s => s.Metrics[Metrics.SamplingPriority] == SamplingPriorityValues.UserReject);
#endif

                // Check the headers of the outbound http requests
                var outputLines = processResult.StandardOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                outputLines.Should().HaveCount(2);

                var firstHttpSpan = httpSpans.Single(s => s.Resource.EndsWith("/a"));
                var secondHttpSpan = httpSpans.Single(s => s.Resource.EndsWith("/b"));

                outputLines[0].Should().Be($"{firstHttpSpan.TraceId}/{firstHttpSpan.SpanId}/2");
                outputLines[1].Should().Be($"{secondHttpSpan.TraceId}/{secondHttpSpan.SpanId}/-1");

                rootSpan.Tags.Should().ContainKey(Tags.RuntimeId);

                var runtimeId = rootSpan.Tags[Tags.RuntimeId];
                Guid.TryParse(runtimeId, out _).Should().BeTrue();

                httpSpans.Should().OnlyContain(
                    s => s.Tags[Tags.RuntimeId] == runtimeId,
                    "runtime id should be synchronized across versions of the tracer");
            }
        }
    }
}
