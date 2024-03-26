// <copyright file="VersionConflict1xTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.VersionConflict
{
    public class VersionConflict1xTests : TestHelper
    {
        public VersionConflict1xTests(ITestOutputHelper output)
            : base("VersionConflict.1x", output)
        {
        }

        [SkippableFact]
        public async Task SubmitTraces()
        {
            // 1 manual span + 1 http span
            const int expectedSpanCount = 2;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(expectedSpanCount);

                spans.Should().HaveCount(expectedSpanCount);

                // The version conflict fix does not work with the 1.x branch, so http traces should be orphaned

                var httpSpan = spans.Single(s => s.Name == "http.request");

                httpSpan.ParentId.Should().BeNull();

                var manualSpan = spans.Single(s => s.Name == "Manual");

                manualSpan.Metrics[Metrics.SamplingPriority].Should().Be(SamplingPriorityValues.UserReject);
            }
        }
    }
}
