// <copyright file="OpenLdapTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Xunit;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class OpenLdapTests
    {
        private readonly ITestOutputHelper _output;

        public OpenLdapTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // The scenario opens a new TCP connection for each Bind in a tight loop: each run leaves ~15k
        // sockets in TIME_WAIT (60s) and the container has ~28k ephemeral ports (32768-60999).
        // All frameworks run back to back in the same container, so adding frameworks can exhaust the
        // ports and the test fails with 'The LDAP server is unavailable' (even without the profiler).
        // If that happens, throttle the scenario (see OpenLdapCrash.ConnectToLdapServer) or set
        // net.ipv4.tcp_tw_reuse=1 (sysctls) on the ProfilerIntegrationTests service in docker-compose.yml.
        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net9, TestFramework.Net10)]
        public void CheckOpenLdapCrash(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 21", output: _output);
            runner.RunAndCheck();
        }
    }
}
