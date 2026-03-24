// <copyright file="ServiceBusMinimalNServiceBusTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    public class ServiceBusMinimalNServiceBusTest : SmokeTestBase, IClassFixture<SqlServerFixture>
    {
        public ServiceBusMinimalNServiceBusTest(ITestOutputHelper output, SqlServerFixture sqlServerFixture)
            : base(output, "ServiceBus.Minimal.NServiceBus", maxTestRunSeconds: 90)
        {
            foreach (var kvp in sqlServerFixture.GetEnvironmentVariables())
            {
                SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task NoExceptions()
        {
            await CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
