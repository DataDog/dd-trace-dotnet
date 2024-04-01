// <copyright file="LargePayloadTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition("LargePayloadTests", DisableParallelization = true)]
    public class LargePayloadTestBase : TestHelper
    {
        public LargePayloadTestBase(ITestOutputHelper output)
            : base("LargePayload", output)
        {
        }

        public int FillerTagLength => 100;

        public int TracesToTrigger => 100;

        public int SpansPerTrace => 99;

        public int ExpectedSpans => TracesToTrigger + (TracesToTrigger * SpansPerTrace);

        protected async Task RunTest()
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                using (var sample = await RunSampleAndWaitForExit(agent, arguments: $" -t {TracesToTrigger} -s {SpansPerTrace} -f {FillerTagLength}"))
                {
                    // Extra long time out because big payloads
                    var timeoutInMilliseconds = 60_000;
                    var spans = agent.WaitForSpans(ExpectedSpans, timeoutInMilliseconds: timeoutInMilliseconds);
                    AssertLargePayloadExpectations(spans);
                }
            }
        }

        private void AssertLargePayloadExpectations(IImmutableList<MockSpan> spans)
        {
            var message = string.Empty;

            var traceMap = new Dictionary<ulong, int>();
            var spanMap = new Dictionary<ulong, int>();
            var tagFillMap = new Dictionary<string, int>();

            foreach (var span in spans)
            {
                var fillerGuid = span.Tags.Single(kvp => kvp.Key == "fill").Value;

                if ((span.ParentId ?? 0) == 0)
                {
                    if (!traceMap.ContainsKey(span.TraceId))
                    {
                        traceMap.Add(span.TraceId, 1);
                    }
                    else
                    {
                        traceMap[span.TraceId]++;
                    }
                }

                if (!spanMap.ContainsKey(span.SpanId))
                {
                    spanMap.Add(span.SpanId, 1);
                }
                else
                {
                    spanMap[span.SpanId]++;
                }

                if (!tagFillMap.ContainsKey(fillerGuid))
                {
                    tagFillMap.Add(fillerGuid, 1);
                }
                else
                {
                    tagFillMap[fillerGuid]++;
                }
            }

            foreach (var duplicate in traceMap.Where(kvp => kvp.Value > 1))
            {
                message += $"Duplicate trace ID {duplicate.Key} with {duplicate.Value} entries {Environment.NewLine}";
            }

            foreach (var duplicate in spanMap.Where(kvp => kvp.Value > 1))
            {
                message += $"Duplicate span ID {duplicate.Key} with {duplicate.Value} entries {Environment.NewLine}";
            }

            foreach (var duplicate in tagFillMap.Where(kvp => kvp.Value > 1))
            {
                message += $"Duplicate fill ID {duplicate.Key} with {duplicate.Value} entries {Environment.NewLine}";
            }

            if (spans.Count != ExpectedSpans)
            {
                message += $"Expected {ExpectedSpans} spans but received {spans.Count} entries {Environment.NewLine}";
            }

            Assert.True(string.Empty.Equals(message), userMessage: message);
        }
    }
}
