// <copyright file="OpenLdapTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
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
                // FIXME: .NET 10 skipping on .NET 10 for now as it crashes with '[Error] An error occured while trying to connect to the LDAP server `openldap-server:389`. Message: The type initializer for 'Ldap' threw an exception.'
                return;
            }

            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 21", output: _output);
            runner.RunAndCheck();
        }
    }
}
