// <copyright file="MetricsTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

using System.Collections.Concurrent;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// These tests are as much a verification of our tracer send code, as it is of our mock agent code.
    /// </summary>
    public class MetricsTransportTests : TestHelper
    {
        private const string Arguments = " -q -t  --ttl 5";

        public MetricsTransportTests(ITestOutputHelper output)
            : base("TransportsTester", @"test\test-applications\regression", output, prependSamplesToAppName: false)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_RUNTIME_METRICS_ENABLED", "true");
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_TRACE_METRICS_ENABLED", "true");
        }

        [Fact]
        [Trait("RunOnWindows", "True")]
        public void MetricsComeThroughTcp()
        {
            EnvironmentHelper.EnableDefaultTcp();
            RunTest();
        }

        [Fact]
        [Trait("RunOnWindows", "False")]
        public void MetricsComeThroughUds()
        {
            if (EnvironmentTools.IsWindows())
            {
                return;
            }

            EnvironmentHelper.EnableUnixDomainSockets();
            RunTest();
        }

        private void RunTest()
        {
            ConcurrentQueue<string> statsRequests;
            using (var agent = EnvironmentHelper.GetMockAgent(useStatsD: true))
            using (var sample = RunSampleAndWaitForExit(agent, arguments: Arguments))
            {
                statsRequests = agent.StatsdRequests;
            }

            Assert.False(statsRequests.IsEmpty);
        }
    }
}

#endif
