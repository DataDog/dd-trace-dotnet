// <copyright file="ServiceBusMinimalRebusTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class ServiceBusMinimalRebusTest : SmokeTestBase
    {
        public ServiceBusMinimalRebusTest(ITestOutputHelper output)
            : base(output, "ServiceBus.Minimal.Rebus", maxTestRunSeconds: 90)
        {
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
