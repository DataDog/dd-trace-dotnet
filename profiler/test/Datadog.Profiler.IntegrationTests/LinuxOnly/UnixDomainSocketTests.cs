// <copyright file="UnixDomainSocketTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    // Linux counterpart of WindowsOnly/NamedPipeTestcs: the profiler uploads over a
    // Unix domain socket (the agent URL is configured as unix://<socket path>, the
    // same transport the system tests exercise with "profiler + UDS").
    [Trait("Category", "LinuxOnly")]
    public class UnixDomainSocketTests
    {
        private const int RetryCount = 3;

        private readonly ITestOutputHelper _output;

        public UnixDomainSocketTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckProfilesSentThroughUnixDomainSocket(string appName, string framework, string appAssembly)
        {
            string[] errorExceptions =
            {
                // transient upload failures that can be retried (same as the named pipe test)
                "ddog_prof_exporter_send_blocking failed: Failed to send HTTP request: error sending request for url (http://localhost/profiling/v1/input): operation timed out",
                "ddog_prof_exporter_send_blocking failed: operation was canceled"
            };

            var runner = new SmokeTestRunner(
                appName,
                framework,
                appAssembly,
                commandLine: "--scenario 1",
                output: _output,
                transportType: TransportType.UnixDomainSocket);

            runner.RunAndCheckWithRetries(RetryCount, errorExceptions);
        }
    }
}
