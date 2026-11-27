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

        // Each Bind in --scenario 21 intentionally opens and closes a new TCP connection (to exercise the
        // connect() syscall wrapper), leaving client-side sockets in Linux's 60 s TIME_WAIT state in the
        // test container. All frameworks run sequentially in the same container (sharing one 28,232-port
        // ephemeral range, 32768-60999); OpenLdapCrash.OnProcess throttles with Thread.Sleep(5) so steady-state
        // TIME_WAIT stays <= 12,000 ports for any number of sequential frameworks.
        // If this fails with 'The LDAP server is unavailable', check for client-side EADDRNOTAVAIL port
        // exhaustion first (the server itself is usually healthy): do not run OpenLdapTests frameworks in
        // parallel, or set net.ipv4.tcp_tw_reuse=1 (sysctls) on ProfilerIntegrationTests in docker-compose.yml.
        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net9, TestFramework.Net10)]
        public void CheckOpenLdapCrash(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 21", output: _output);
            runner.RunAndCheck();
        }
    }
}
