// <copyright file="ServiceBusMinimalMassTransitTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Trait("RequiresDockerDependency", "true")]
    public class ServiceBusMinimalMassTransitTest : SmokeTestBase
    {
        public ServiceBusMinimalMassTransitTest(ITestOutputHelper output)
            : base(output, "ServiceBus.Minimal.MassTransit", maxTestRunSeconds: 60)
        {
            AssumeSuccessOnTimeout = true;
        }

        [SkippableFact(Skip = "Testing to see if this solves weird flake issues")]
        [Trait("Category", "Smoke")]
        public async Task NoExceptions()
        {
            await CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
