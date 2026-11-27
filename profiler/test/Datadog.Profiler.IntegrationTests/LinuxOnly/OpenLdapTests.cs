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
        // ephemeral range, 32768-60999); docker-compose.yml sets `net.ipv4.tcp_tw_reuse=1` on
        // ProfilerIntegrationTests so TIME_WAIT port numbers can be reused after 1 s instead of 60 s.
        // Note: if you run all frameworks outside docker-compose against a non-loopback LDAP server IP,
        // Linux's default `net.ipv4.tcp_tw_reuse=2` only covers loopback (localhost), so you may hit
        // client-side EADDRNOTAVAIL ('The LDAP server is unavailable' even though the server is healthy)
        // unless `net.ipv4.tcp_tw_reuse=1` is set there too.
        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net9, TestFramework.Net10)]
        public void CheckOpenLdapCrash(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 21", output: _output);
            runner.RunAndCheck();
        }
    }
}
