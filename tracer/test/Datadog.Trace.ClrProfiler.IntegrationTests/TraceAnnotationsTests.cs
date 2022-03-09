// <copyright file="TraceAnnotationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class TraceAnnotationsTests : TestHelper
    {
        private static readonly string[] TestTypes = { "Samples.TraceAnnotations.TestType", "Samples.TraceAnnotations.TestTypeGeneric`1", "Samples.TraceAnnotations.TestTypeStruct", "Samples.TraceAnnotations.TestTypeStatic" };

        public TraceAnnotationsTests(ITestOutputHelper output)
            : base("TraceAnnotations", output)
        {
            SetServiceVersion("1.0.0");

            var ddTraceMethodsString = "Samples.TraceAnnotations.Program[RunTestsAsync]";
            foreach (var type in TestTypes)
            {
                ddTraceMethodsString += $";{type}[VoidMethod,ReturnValueMethod,ReturnReferenceMethod,ReturnGenericMethod,ReturnTaskMethod,ReturnTaskTMethod]";
            }

            SetEnvironmentVariable("DD_TRACE_METHODS", ddTraceMethodsString);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTraces()
        {
            var expectedSpanCount = 28;

            const string expectedOperationName = "trace.annotation";

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                spans.Count.Should().Be(expectedSpanCount);

                var orderedSpans = spans.OrderBy(s => s.Start);
                var rootSpan = orderedSpans.First();
                var remainingSpans = orderedSpans.Skip(1).ToList();

                remainingSpans.Should()
                              .OnlyContain(span => span.ParentId == rootSpan.SpanId)
                              .And.OnlyContain(span => span.TraceId == rootSpan.TraceId);

                // Assert that the child spans do not overlap
                long? lastStartTime = null;
                long? lastEndTime = null;
                foreach (var span in remainingSpans)
                {
                    if (lastEndTime.HasValue)
                    {
                        span.Start.Should().BeGreaterThan(lastEndTime.Value);
                    }

                    lastStartTime = span.Start;
                    lastEndTime = lastStartTime + span.Duration;
                }

                // Assert that the root span encloses child spans
                rootSpan.Start.Should().BeLessThan(remainingSpans.First().Start);
                (rootSpan.Start + rootSpan.Duration).Should().BeGreaterThan(lastEndTime.Value);

                // Run snapshot verification
                var settings = VerifyHelper.GetSpanVerifierSettings();
                await Verifier.Verify(orderedSpans, settings)
                              .UseMethodName("_");
            }
        }
    }
}
