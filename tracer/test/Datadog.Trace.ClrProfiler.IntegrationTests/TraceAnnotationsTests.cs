// <copyright file="TraceAnnotationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
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
                ddTraceMethodsString += $";{type}[VoidMethod,ReturnValueMethod,ReturnReferenceMethod,ReturnGenericMethod,ReturnTaskMethod,ReturnValueTaskMethod,ReturnTaskTMethod,ReturnValueTaskTMethod]";
            }

            SetEnvironmentVariable("DD_TRACE_METHODS", ddTraceMethodsString);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTraces()
        {
            var expectedSpanCount = 36;

            const string expectedOperationName = "trace.annotation";

            using var telemetry = this.ConfigureTelemetry();
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
                    using var scope = new AssertionScope();
                    scope.AddReportable("resource_name", span.Resource);
                    scope.AddReportable("duration", span.Duration.ToString());

#if NETCOREAPP3_1_OR_GREATER
                    // Assert a minimum 100ms duration for Task/Task<T>/ValueTask/ValueTask<TResult>
                    if (span.Resource == "ReturnTaskMethod" || span.Resource == "ReturnValueTaskMethod")
                    {
                        // Assert that these methods have a 100ms delay
                        span.Duration.Should().BeGreaterThanOrEqualTo(100_000_000);
                    }
#else
                    // Only perform a 100ms duration assertion on Task/Task<T>.
                    // Builds lower than netcoreapp3.1 do not correctly close ValueTask/ValueTask<TResult> asynchronously
                    if (span.Resource == "ReturnTaskMethod")
                    {
                        // Assert that these methods have a 100ms delay
                        span.Duration.Should().BeGreaterThanOrEqualTo(100_000_000);
                    }
#endif

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

                telemetry.AssertIntegrationEnabled(IntegrationId.TraceAnnotations);
                telemetry.AssertConfiguration(ConfigTelemetryData.TraceMethods);

                // Run snapshot verification
                var settings = VerifyHelper.GetSpanVerifierSettings();
                await Verifier.Verify(orderedSpans, settings)
                              .UseMethodName("_");
            }
        }
    }
}
