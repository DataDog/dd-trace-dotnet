// <copyright file="RuntimeMetricsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(RuntimeMetricsTests), DisableParallelization = true)]
    public class RuntimeMetricsTests : TestHelper
    {
        public RuntimeMetricsTests(ITestOutputHelper output)
            : base("RuntimeMetrics", output)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void MetricsDisabled()
        {
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "0");
            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);

            using var processResult = RunSampleAndWaitForExit(agent);
            var requests = agent.StatsdRequests;

            Assert.True(requests.Count == 0, "Received metrics despite being disabled. Metrics received: " + string.Join("\n", requests));
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void UdpSubmitsMetrics()
        {
            EnvironmentHelper.EnableDefaultTransport();
            RunTest();
        }

        // #if NETCOREAPP3_1_OR_GREATER
#if NET6_0_OR_GREATER // This should be tested on netcoreapp3.1 but we need to figure out why the tests lock up pre net6-0
        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "False")]
        public void UdsSubmitsMetrics()
        {
            EnvironmentHelper.EnableUnixDomainSockets();
            RunTest();
        }
#endif

        private void RunTest()
        {
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");
            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            using var processResult = RunSampleAndWaitForExit(agent);
            var requests = agent.StatsdRequests;

            // Check if we receive 2 kinds of metrics:
            // - exception count is gathered using common .NET APIs
            // - contention count is gathered using platform-specific APIs

            var exceptionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.exceptions.count"));

            Assert.True(exceptionRequestsCount > 0, "No exception metrics received. Metrics received: " + string.Join("\n", requests));

            // Check if .NET Framework or .NET Core 3.1+
            if (!EnvironmentHelper.IsCoreClr()
             || (Environment.Version.Major == 3 && Environment.Version.Minor == 1)
             || Environment.Version.Major >= 5)
            {
                var contentionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.threads.contention_count"));

                Assert.True(contentionRequestsCount > 0, "No contention metrics received. Metrics received: " + string.Join("\n", requests));
            }

            Assert.Empty(agent.Exceptions);
        }
    }
}
