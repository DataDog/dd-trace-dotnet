// <copyright file="TracingIntegrationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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
            string packageVersion = "",
            Func<MockSpan, bool> spanFilter = null)
        {
            SetEnvironmentVariable("DD_TRACE_ENABLED", "false");

            var spans = await RunDisabled(packageVersion, spanFilter);

            spans.Should().BeEmpty("no spans should be created when DD_TRACE_ENABLED=false");
        }

        internal async Task RunSampleWithIntegrationDisabled(
            IntegrationId id,
            string packageVersion = "",
            Func<MockSpan, bool> spanFilter = null)
        {
            var integrationName = id.ToStringFast();
            SetEnvironmentVariable($"DD_TRACE_{integrationName}_ENABLED", "false");

            var spans = await RunDisabled(packageVersion, spanFilter);

            spans.Should().BeEmpty($"no {integrationName} spans should be created when DD_TRACE_{integrationName}_ENABLED=false");
        }

        private async Task<IEnumerable<MockSpan>> RunDisabled(
            string packageVersion,
            Func<MockSpan, bool> spanFilter)
        {
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);

            IEnumerable<MockSpan> spans = agent.Spans;

            if (spanFilter != null)
            {
                spans = spans.Where(spanFilter);
            }

            return spans;
        }
    }
}
