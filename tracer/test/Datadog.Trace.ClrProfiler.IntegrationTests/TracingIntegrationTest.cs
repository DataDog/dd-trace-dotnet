// <copyright file="TracingIntegrationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TracingIntegrationTest : TestHelper
    {
        protected TracingIntegrationTest(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
        }

        protected TracingIntegrationTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
            : base(sampleAppName, samplePathOverrides, output)
        {
        }

        public abstract Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion);

        public void ValidateIntegrationSpans(IEnumerable<MockSpan> spans, string metadataSchemaVersion, string expectedServiceName, bool isExternalSpan)
        {
            foreach (var span in spans)
            {
                var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                Assert.True(result.Success, result.ToString());
                Assert.Equal(expectedServiceName, span.Service);

                if (isExternalSpan)
                {
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }

        internal async Task RunSampleWithAllTracingDisabled(
            int timeoutInMilliseconds = 500,
            string packageVersion = "",
            Func<MockSpan, bool> spanFilter = null)
        {
            SetEnvironmentVariable("DD_TRACE_ENABLED", "false");

            var spans = await RunDisabled(packageVersion, timeoutInMilliseconds, spanFilter);

            spans.Should().BeEmpty("no spans should be created when DD_TRACE_ENABLED=false");
        }

        internal async Task RunSampleWithIntegrationDisabled(
            IntegrationId id,
            int timeoutInMilliseconds = 500,
            string packageVersion = "",
            Func<MockSpan, bool> spanFilter = null)
        {
            var integrationName = id.ToStringFast();
            SetEnvironmentVariable($"DD_TRACE_{integrationName}_ENABLED", "false");

            var spans = await RunDisabled(packageVersion, timeoutInMilliseconds, spanFilter);

            spans.Should().BeEmpty($"no {integrationName} spans should be created when DD_TRACE_{integrationName}_ENABLED=false");
        }

        private async Task<IEnumerable<MockSpan>> RunDisabled(
            string packageVersion,
            int timeoutInMilliseconds,
            Func<MockSpan, bool> spanFilter)
        {
            using var agent = EnvironmentHelper.GetMockAgent();

            if (spanFilter != null)
            {
                agent.SpanFilters.Add(spanFilter);
            }

            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            // count: 1, to force the agent to wait until timeout. count: 0 would return immediately.
            // assertExpectedCount: false, because we will never get any spans.
            return agent.WaitForSpans(
                count: 1,
                timeoutInMilliseconds: timeoutInMilliseconds,
                assertExpectedCount: false);
        }
    }
}
