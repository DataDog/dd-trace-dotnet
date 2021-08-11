// <copyright file="AzureFunctionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
using System;
using System.Linq;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AzureFunctionsTests : TestHelper
    {
        private const string ExpectedServiceName = "Samples.AzureFunctions.AllTriggers";

        public AzureFunctionsTests(ITestOutputHelper output)
            : base("AzureFunctions.AllTriggers", @"test\test-applications\azure-functions", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Fact]
        public void SubmitTraces()
        {
            SetCallTargetSettings(true);

            const int expectedMinimumSpanCount = 3;

            var agentPort = TcpPortProvider.GetOpenPort();
            using var agent = new MockTracerAgent(agentPort);

            var functionsPort = TcpPortProvider.GetOpenPort();
            using var process = RunFunctionsHost(functionsPort, agent.Port);

            var spans = agent.WaitForSpans(expectedMinimumSpanCount);

            process.CloseMainWindow();
            process.Close();
            CloseOutProcess(process);

            // var functionsSpans = spans.Where(span => string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));
            foreach (var span in spans)
            {
                span.Type.Should().Be(SpanTypes.Serverless);
                span.Service.Should().Be(ExpectedServiceName);
                span.Tags.Should().Contain(new System.Collections.Generic.KeyValuePair<string, string>(Tags.InstrumentationName, "AzureFunctions"));
                span.Name.Should().Be("azure.function");
                // span.Tags?.ContainsKey(Tags.Version).Should().BeFalse("External service span should not have service version tag.");
                // span.Tags[Tags.SpanKind].Should().Be(SpanKinds.Client);
                // span.Resource.Should().Be($"msmq.purge {span.Tags[Tags.MsmqQueuePath]}");
                // purgeCount++;
            }

            // spanCount.Should().Be(expectedSpanCount);
        }
    }
}
#endif
