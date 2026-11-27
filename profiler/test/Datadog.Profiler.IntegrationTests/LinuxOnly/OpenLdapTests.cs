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

        // Frameworks run sequentially in one container and open a new TCP connection per Bind
        // (see OpenLdapCrash.ConnectToLdapServer); docker-compose.yml sets net.ipv4.tcp_tw_reuse=1
        // to avoid client-side TIME_WAIT port exhaustion ("The LDAP server is unavailable").
        // Outside Compose against a non-loopback LDAP IP, set tcp_tw_reuse=1 (default 2 is loopback-only).
        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net7, TestFramework.Net8, TestFramework.Net9, TestFramework.Net10)]
        public void CheckOpenLdapCrash(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 21", output: _output);
            runner.RunAndCheck();
        }
    }
}
