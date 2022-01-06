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
    [Collection(nameof(MetricsTransportTests))]
    public class MetricsTransportTests : TestHelper
    {
        public MetricsTransportTests(ITestOutputHelper output)
            : base("TransportsTester", @"test\test-applications\regression", output, prependSamplesToAppName: false)
        {
        }

        /// <summary>
        /// This test is as much a verification of our tracer send code, as it is of our mock agent code.
        /// </summary>
        [Fact]
        [Trait("RunOnWindows", "False")]
        public void MetricsMatchBetweenUdpAndUds()
        {
            ConcurrentQueue<string> tcpRequests;
            ConcurrentQueue<string> udsRequests;
            var args = " -q -t 0 --ttl 5";

            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_RUNTIME_METRICS_ENABLED", "true");
            EnvironmentHelper.CustomEnvironmentVariables.Add("DD_TRACE_METRICS_ENABLED", "true");

            EnvironmentHelper.EnableDefaultTcp();
            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                using (var sample = RunSampleAndWaitForExit(agent, arguments: args))
                {
                    tcpRequests = agent.StatsdRequests;
                }
            }

            EnvironmentHelper.EnableUnixDomainSockets();
            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                using (var sample = RunSampleAndWaitForExit(agent, arguments: args))
                {
                    udsRequests = agent.StatsdRequests;
                }
            }

            Assert.False(tcpRequests.IsEmpty);
            Assert.False(udsRequests.IsEmpty);
        }
    }
}

#endif
