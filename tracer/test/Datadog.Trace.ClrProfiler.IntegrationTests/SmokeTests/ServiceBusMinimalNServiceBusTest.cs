// <copyright file="ServiceBusMinimalNServiceBusTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Trait("RequiresDockerDependency", "true")]
    public class ServiceBusMinimalNServiceBusTest : SmokeTestBase
    {
        public ServiceBusMinimalNServiceBusTest(ITestOutputHelper output)
            : base(output, "ServiceBus.Minimal.NServiceBus", maxTestRunSeconds: 90)
        {
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task NoExceptions()
        {
            await CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
