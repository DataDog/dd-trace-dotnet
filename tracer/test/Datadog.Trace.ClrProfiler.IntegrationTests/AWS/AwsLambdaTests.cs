// <copyright file="AwsLambdaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    public class AwsLambdaTests : TestHelper, IDisposable
    {
        public AwsLambdaTests(ITestOutputHelper output)
            : base("AWS.Lambda", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "ArmUnsupported")]
        [Trait("Category", "Lambda")]
        public async Task SubmitsTraces()
        {
            // See documentation at docs/development/Serverless.md for examples and diagrams
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")))
            {
                Output.WriteLine("Skipping");
                return;
            }

            using var extensionWithContext = new MockLambdaExtension(shouldSendContext: true, port: 9004, Output);
            using var extensionNoContext = new MockLambdaExtension(shouldSendContext: false, port: 9003, Output);
            using var agent = EnvironmentHelper.GetMockAgent(fixedPort: 5002);
            agent.Output = Output;
            using (RunSampleAndWaitForExit(agent))
            {
                var requests = 9 // param tests
                                  + 3 // param with context
                                  + 5 // base instrumentation
                                  + 6 // other parameter types
                                  + 3 // throwing (manual only)
                                  + 3; // throwing with context

                var expectedSpans = requests * 2; // we manually instrument each request too

                var spans = agent.WaitForSpans(expectedSpans, 15_000).ToArray();

                // Verify all the "with context" end invocations have a corresponding start context
                using var s = new AssertionScope();
                var startWithContext = extensionWithContext.StartInvocations.ToList();
                var endWithContext = extensionWithContext.EndInvocations.ToList();
                startWithContext.Should().OnlyContain(x => x.TraceId.HasValue);
                endWithContext.Should().OnlyContain(x => x.TraceId.HasValue);
                var contextPairs = extensionWithContext.EndInvocations
                                                       .Select(x => (start: startWithContext.SingleOrDefault(y => y.TraceId == x.TraceId), end: x))
                                                       .ToList();
                contextPairs.Should().OnlyContain(x => x.start != null);
                var withContextSpans = contextPairs.Select(x => ToMockSpan(x.end, x.start.Created));

                // We can't match no-context start invocations with the end invocations, so just use the end ones
                var noContextSpans = extensionNoContext.EndInvocations.Select(x => ToMockSpan(x, startTime: null));

                // Create the complete traces
                var allSpans = withContextSpans
                              .Concat(noContextSpans)
                              .Concat(spans)
                              .ToList();

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(allSpans, settings)
                                  .UseFileName(nameof(AwsLambdaTests));
            }
        }

        private static MockSpan ToMockSpan(MockLambdaExtension.EndExtensionRequest endInvocation, DateTimeOffset? startTime)
        {
            var start = startTime ?? endInvocation.Created.AddMilliseconds(100);
            return new MockSpan
            {
                Duration = endInvocation.Created.Subtract(start).ToNanoseconds(),
                Name = "lambda.invocation",
                Service = "LambdaExtension",
                Resource = "/lambda/end-invocation",
                TraceId = endInvocation.TraceId ?? 0,
                SpanId = endInvocation.SpanId ?? 0,
                Start = start.ToUnixTimeNanoseconds(),
                Error = endInvocation.IsError ? (byte)1 : (byte)0,
                Tags = new Dictionary<string, string> { { "_sampling_priority_v1", endInvocation.SamplingPriority?.ToString("N1") } }
            };
        }
    }
}
#endif
