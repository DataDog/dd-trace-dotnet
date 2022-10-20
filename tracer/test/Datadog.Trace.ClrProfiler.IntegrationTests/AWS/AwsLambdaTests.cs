// <copyright file="AwsLambdaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    public class AwsLambdaTests : TestHelper
    {
        public AwsLambdaTests(ITestOutputHelper output)
            : base("AWS.Lambda", output)
        {
        }

        /// <summary>
        /// The environment variables containing the list of handlers we call
        /// </summary>
        public List<string> HandlerVariables { get; } = new()
        {
            // standard parameters
            "AWS_LAMBDA_ENDPOINT_NO_PARAM_SYNC",
            "AWS_LAMBDA_ENDPOINT_ONE_PARAM_SYNC",
            "AWS_LAMBDA_ENDPOINT_TWO_PARAMS_SYNC",
            "AWS_LAMBDA_ENDPOINT_NO_PARAM_ASYNC",
            "AWS_LAMBDA_ENDPOINT_ONE_PARAM_ASYNC",
            "AWS_LAMBDA_ENDPOINT_TWO_PARAMS_ASYNC",
            "AWS_LAMBDA_ENDPOINT_NO_PARAM_VOID",
            "AWS_LAMBDA_ENDPOINT_ONE_PARAM_VOID",
            "AWS_LAMBDA_ENDPOINT_TWO_PARAMS_VOID",
            // with context
            "AWS_LAMBDA_ENDPOINT_NO_PARAM_SYNC_WITH_CONTEXT",
            "AWS_LAMBDA_ENDPOINT_ONE_PARAM_SYNC_WITH_CONTEXT",
            "AWS_LAMBDA_ENDPOINT_TWO_PARAMS_SYNC_WITH_CONTEXT",
            // base functions
            "AWS_LAMBDA_ENDPOINT_BASE_NO_PARAM_SYNC",
            "AWS_LAMBDA_ENDPOINT_BASE_TWO_PARAMS_SYNC",
            "AWS_LAMBDA_ENDPOINT_BASE_ONE_PARAM_SYNC_WITH_CONTEXT",
            "AWS_LAMBDA_ENDPOINT_BASE_ONE_PARAM_ASYNC",
            "AWS_LAMBDA_ENDPOINT_BASE_TWO_PARAMS_VOID",
            // tricky parameter types
            "AWS_LAMBDA_ENDPOINT_STRUCT_PARAM",
            "AWS_LAMBDA_ENDPOINT_NESTED_CLASS_PARAM",
            "AWS_LAMBDA_ENDPOINT_NESTED_STRUCT_PARAM",
            "AWS_LAMBDA_ENDPOINT_GENERIC_DICT_PARAM",
            "AWS_LAMBDA_ENDPOINT_NESTED_GENERIC_DICT_PARAM",
            "AWS_LAMBDA_ENDPOINT_DOUBLY_NESTED_GENERIC_DICT_PARAM",
            // throwing handlers
            "AWS_LAMBDA_ENDPOINT_THROWING",
            "AWS_LAMBDA_ENDPOINT_THROWING_ASYNC",
            "AWS_LAMBDA_ENDPOINT_THROWING_ASYNC_TASK",
            "AWS_LAMBDA_ENDPOINT_THROWING_WITH_CONTEXT",
            "AWS_LAMBDA_ENDPOINT_THROWING_ASYNC_WITH_CONTEXT",
            "AWS_LAMBDA_ENDPOINT_THROWING_ASYNC_TASK_WITH_CONTEXT",
            // top level functions
            "AWS_LAMBDA_ENDPOINT_TOP_LEVEL_PROGRAM_WITH_CONTEXT",
        };

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
            
            // See documentation at docs/development/Serverless.md for structure
            // Each variable points to a different lambda handler instance
            await SendRequests(HandlerVariables);
            var expectedSpans = HandlerVariables.Count * 3; // Each handler generates 2 spans + the extension span 

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

        private async Task SendRequests(IEnumerable<string> hostEnvVars)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders
                  .Accept
                  .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-datadog-tracing-enabled", "false");
            var content = new StringContent("{}", Encoding.UTF8, "application/json");

            foreach (var hostEnvVar in hostEnvVars)
            {
                var hostName = Environment.GetEnvironmentVariable(hostEnvVar);
                Output?.WriteLine($"Env var {hostEnvVar} set to {hostName}");

                var url = $"{hostName}/2015-03-31/functions/function/invocations";
                var response = await client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();
                Output?.WriteLine($"Response sent to {url}: {response.StatusCode} {result}");
            }
        }
    }
}
#endif
