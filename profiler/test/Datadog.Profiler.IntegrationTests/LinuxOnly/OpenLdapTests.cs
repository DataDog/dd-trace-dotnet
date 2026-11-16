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

        [TestAppFact("Samples.Computer01", Frameworks = new[] { "net6.0", "net7.0" })]
        public void CheckOpenLdapCrash(string appName, string framework, string appAssembly)
        {
            if (EnvironmentHelper.IsAlpine)
            {
                throw new SkipTestException("Alpine is not supported: connecting to openldap-server:389 crashes in the LDAP type initializer.");
            }

            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 21", output: _output);
            runner.RunAndCheck();
        }
    }
}
