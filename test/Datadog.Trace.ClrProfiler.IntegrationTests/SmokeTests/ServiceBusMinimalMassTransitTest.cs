// <copyright file="ServiceBusMinimalMassTransitTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class ServiceBusMinimalMassTransitTest : SmokeTestBase
    {
        public ServiceBusMinimalMassTransitTest(ITestOutputHelper output)
            : base(output, "ServiceBus.Minimal.MassTransit", maxTestRunSeconds: 60)
        {
            AssumeSuccessOnTimeout = true;
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
