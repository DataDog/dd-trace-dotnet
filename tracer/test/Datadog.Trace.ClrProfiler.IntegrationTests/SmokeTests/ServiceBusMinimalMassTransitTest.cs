// <copyright file="ServiceBusMinimalMassTransitTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Collection(SqlServerCollection.Name)]
    public class ServiceBusMinimalMassTransitTest : SmokeTestBase
    {
        public ServiceBusMinimalMassTransitTest(ITestOutputHelper output, SqlServerFixture sqlServerFixture)
            : base(output, "ServiceBus.Minimal.MassTransit", maxTestRunSeconds: 60)
        {
            foreach (var kvp in sqlServerFixture.GetEnvironmentVariables())
            {
                SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

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
