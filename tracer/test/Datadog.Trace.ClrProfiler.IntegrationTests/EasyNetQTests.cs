// <copyright file="EasyNetQTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    public class EasyNetQTests : TracingIntegrationTest
    {
        public EasyNetQTests(ITestOutputHelper output)
        : base("EasyNetQ", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsRabbitMQ();

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitTraces()
        {
            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent))
            {
                const int expectedSpanCount = 7; // Sync + async
                var spans = agent.WaitForSpans(expectedSpanCount);

                ValidateIntegrationSpans(spans, expectedServiceName: "Samples.EasyNetQ-rabbitmq");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(EasyNetQTests));

                telemetry.AssertIntegrationEnabled(IntegrationId.RabbitMQ);
            }
        }
    }
}
