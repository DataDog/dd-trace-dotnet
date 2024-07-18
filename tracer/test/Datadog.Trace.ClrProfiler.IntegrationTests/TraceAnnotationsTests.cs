// <copyright file="TraceAnnotationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
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
            : base("TraceAnnotations", enableTelemetry: true, output)
        {
        }
    }

    public class TraceAnnotationsVersionMismatchAfterFeatureTests : TraceAnnotationsTests
    {
        public TraceAnnotationsVersionMismatchAfterFeatureTests(ITestOutputHelper output)
            : base("TraceAnnotations.VersionMismatch.AfterFeature", enableTelemetry: false, output)
        {
        }
    }

    public class TraceAnnotationsVersionMismatchBeforeFeatureTests : TraceAnnotationsTests
    {
        public TraceAnnotationsVersionMismatchBeforeFeatureTests(ITestOutputHelper output)
            : base("TraceAnnotations.VersionMismatch.BeforeFeature", enableTelemetry: false, output)
        {
#if NET8_0_OR_GREATER
            // The .NET 8 runtime is more aggressive in optimising structs
            // so if you reference a version of the .NET tracer prior to this fix:
            // https://github.com/DataDog/dd-trace-dotnet/pull/4608 you may get
            // struct tearing issues. Bumping the TraceAnnotations.VersionMismatch.AfterFeature project to a version
            // with the issue solves the problem.
            // _However_ Duck-typing is broken on .NET 8 prior to when we added explicit support, so there will never
            // be an "older" package version we can test with
            throw new SkipException("Tracer versions before TraceAnnotations was supported do not support .NET 8");
#endif
        }
    }

    public class TraceAnnotationsVersionMismatchNewerNuGetTests : TraceAnnotationsTests
    {
        public TraceAnnotationsVersionMismatchNewerNuGetTests(ITestOutputHelper output)
            : base("TraceAnnotations.VersionMismatch.NewerNuGet", enableTelemetry: false, output)
        {
        }
    }

    [UsesVerify]
    public abstract class TraceAnnotationsTests : TestHelper
    {
        private static readonly string[] TestTypes =
        [
            "Samples.TraceAnnotations.TestType",
            "Samples.TraceAnnotations.TestTypeGeneric`1",
            "Samples.TraceAnnotations.TestTypeStruct",
            "Samples.TraceAnnotations.TestTypeStatic"
        ];

        private readonly bool _enableTelemetry;

        protected TraceAnnotationsTests(string sampleAppName, bool enableTelemetry, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
            SetServiceVersion("1.0.0");

            _enableTelemetry = enableTelemetry;
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTraces()
        {
            const int expectedSpanCount = 50;
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
            using (await RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(expectedSpanCount);

                var orderedSpans = spans.OrderBy(s => s.Start).ToList();
                var rootSpan = orderedSpans.First();
                var remainingSpans = orderedSpans.Skip(1).ToList();

                remainingSpans.Should()
                              .OnlyContain(span => span.ParentId == rootSpan.SpanId)
                              .And.OnlyContain(span => span.TraceId == rootSpan.TraceId)
                              .And.OnlyContain(span => span.Name == "trace.annotation" || span.Name == "overridden.attribute");

                long lastEndTime = 0;

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

                    // Assert that the child spans do not overlap
                    span.Start.Should().BeGreaterThan(lastEndTime);

                    var lastStartTime = span.Start;
                    lastEndTime = lastStartTime + span.Duration;
                }

                // Assert that the root span encloses child spans
                rootSpan.Start.Should().BeLessThan(remainingSpans.First().Start);
                (rootSpan.Start + rootSpan.Duration).Should().BeGreaterThan(lastEndTime);

                telemetry?.AssertIntegrationEnabled(IntegrationId.TraceAnnotations);
                telemetry?.AssertConfiguration("DD_TRACE_METHODS"); // normalised to trace_methods in the backend

                // Run snapshot verification
                var settings = VerifyHelper.GetSpanVerifierSettings(
                    scrubbers: null,
                    parameters: [],
                    apmStringTagsScrubber: VerifyHelper.ScrubStringTags, // remove "_dd.agent_psr" to prevent flake
                    apmNumericTagsScrubber: ApmNumericTagsScrubber,
                    ciVisStringTagsScrubber: null,
                    ciVisNumericTagsScrubber: null);

                await Verifier.Verify(orderedSpans, settings)
                              .UniqueForRuntime()
                              .UseMethodName("_");
            }

            telemetry?.Dispose();
            return;

            // remove "_dd.agent_psr"
            static Dictionary<string, double> ApmNumericTagsScrubber(MockSpan target, Dictionary<string, double> tags)
            {
                return tags
                     ?.Where(kvp => !string.Equals(kvp.Key, Metrics.SamplingAgentDecision))
                      .OrderBy(x => x.Key)
                      .ToDictionary(x => x.Key, x => x.Value);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task IntegrationDisabled()
        {
            // Don't bother with telemetry in version mismatch scenarios because older versions may only support V1 telemetry
            // which we no longer support in our mock telemetry agent
            // FIXME: Could be fixed with an upgrade to the NuGet package (after .NET 8?)
            MockTelemetryAgent telemetry = _enableTelemetry ? this.ConfigureTelemetry() : null;
            SetEnvironmentVariable("DD_TRACE_METHODS", string.Empty);
            SetEnvironmentVariable("DD_TRACE_ANNOTATIONS_ENABLED", "false");

            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent);
            var spans = agent.Spans;

            Assert.Empty(spans);
            telemetry?.AssertIntegration(IntegrationId.TraceAnnotations, enabled: false, autoEnabled: false);
            telemetry?.Dispose();
        }
    }
}
