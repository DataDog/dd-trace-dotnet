// <copyright file="TraceAnnotationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

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
    public class TraceAnnotationsAutomaticOnlyTests : TraceAnnotationsTests
    {
        public TraceAnnotationsAutomaticOnlyTests(ITestOutputHelper output)
            : base("TraceAnnotations", twoAssembliesLoaded: false, enableTelemetry: true, output)
        {
        }
    }

    public class TraceAnnotationsVersionMismatchAfterFeatureTests : TraceAnnotationsTests
    {
        public TraceAnnotationsVersionMismatchAfterFeatureTests(ITestOutputHelper output)
            : base("TraceAnnotations.VersionMismatch.AfterFeature", twoAssembliesLoaded: true, enableTelemetry: false, output)
        {
        }
    }

    public class TraceAnnotationsVersionMismatchBeforeFeatureTests : TraceAnnotationsTests
    {
        public TraceAnnotationsVersionMismatchBeforeFeatureTests(ITestOutputHelper output)
            : base("TraceAnnotations.VersionMismatch.BeforeFeature", twoAssembliesLoaded: true, enableTelemetry: false, output)
        {
        }
    }

    public class TraceAnnotationsVersionMismatchNewerNuGetTests : TraceAnnotationsTests
    {
        public TraceAnnotationsVersionMismatchNewerNuGetTests(ITestOutputHelper output)
#if NETFRAMEWORK
            : base("TraceAnnotations.VersionMismatch.NewerNuGet", twoAssembliesLoaded: true, enableTelemetry: false, output)
#else
            : base("TraceAnnotations.VersionMismatch.NewerNuGet", twoAssembliesLoaded: false, enableTelemetry: false, output)
#endif
        {
        }
    }

    [UsesVerify]
    public abstract class TraceAnnotationsTests : TestHelper
    {
        private static readonly string[] TestTypes = { "Samples.TraceAnnotations.TestType", "Samples.TraceAnnotations.TestTypeGeneric`1", "Samples.TraceAnnotations.TestTypeStruct", "Samples.TraceAnnotations.TestTypeStatic" };

        private readonly bool _twoAssembliesLoaded;
        private readonly bool _enableTelemetry;

        public TraceAnnotationsTests(string sampleAppName, bool twoAssembliesLoaded, bool enableTelemetry, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
            SetServiceVersion("1.0.0");

            _twoAssembliesLoaded = twoAssembliesLoaded;
            _enableTelemetry = enableTelemetry;
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTraces()
        {
            var expectedSpanCount = 50;

            var ddTraceMethodsString = string.Empty;
            foreach (var type in TestTypes)
            {
                ddTraceMethodsString += $";{type}[*,get_Name]";
            }

            ddTraceMethodsString += ";Samples.TraceAnnotations.ExtensionMethods[ExtensionMethodForTestType,ExtensionMethodForTestTypeGeneric,ExtensionMethodForTestTypeTypeStruct];System.Net.Http.HttpRequestMessage[set_Method]";

            SetEnvironmentVariable("DD_TRACE_METHODS", ddTraceMethodsString);
            // Don't bother with telemetry in version mismatch scenarios because older versions may only support V1 telemetry
            // which we no longer support in our mock telemetry agent
            // FIXME: Could be fixed with an upgrade to the NuGet package (after .NET 8?)
            MockTelemetryAgent telemetry = _enableTelemetry ? this.ConfigureTelemetry() : null;
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(expectedSpanCount);

                var orderedSpans = spans.OrderBy(s => s.Start);
                var rootSpan = orderedSpans.First();
                var remainingSpans = orderedSpans.Skip(1).ToList();

                remainingSpans.Should()
                              .OnlyContain(span => span.ParentId == rootSpan.SpanId)
                              .And.OnlyContain(span => span.TraceId == rootSpan.TraceId)
                              .And.OnlyContain(span => span.Name == "trace.annotation" || span.Name == "overridden.attribute");

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
                        // Assert that these methods have a 100ms delay, with a somewhat generous tolerance just to assert the span doesn't end immediately
                        span.Duration.Should().BeGreaterThan(70_000_000);
                    }
#else
                    // Only perform a 100ms duration assertion on Task/Task<T>.
                    // Builds lower than netcoreapp3.1 do not correctly close ValueTask/ValueTask<TResult> asynchronously
                    if (span.Resource == "ReturnTaskMethod")
                    {
                        // Assert that these methods have a 100ms delay, with a somewhat generous tolerance just to assert the span doesn't end immediately
                        span.Duration.Should().BeGreaterThan(70_000_000);
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

                telemetry?.AssertIntegrationEnabled(IntegrationId.TraceAnnotations);
                telemetry?.AssertConfiguration(ConfigTelemetryData.TraceMethods);

                // Run snapshot verification
                var settings = VerifyHelper.GetSpanVerifierSettings();
                await Verifier.Verify(orderedSpans, settings)
                              .UseMethodName("_");
            }

            telemetry?.Dispose();
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            // Don't bother with telemetry in version mismatch scenarios because older versions may only support V1 telemetry
            // which we no longer support in our mock telemetry agent
            // FIXME: Could be fixed with an upgrade to the NuGet package (after .NET 8?)
            MockTelemetryAgent telemetry = _enableTelemetry ? this.ConfigureTelemetry() : null;
            SetEnvironmentVariable("DD_TRACE_METHODS", string.Empty);
            SetEnvironmentVariable("DD_TRACE_ANNOTATIONS_ENABLED", "false");

            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.Spans;

            Assert.Empty(spans);
            telemetry?.AssertIntegration(IntegrationId.TraceAnnotations, enabled: false, autoEnabled: false);
            telemetry?.Dispose();
        }
    }
}
