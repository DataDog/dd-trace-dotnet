// <copyright file="SocketTimeoutTests.cs" company="Datadog">
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
    public class SocketTimeoutTests
    {
        private readonly ITestOutputHelper _output;

        public SocketTimeoutTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", Frameworks = new[] { "net6.0", "net7.0" })]
        public void CheckSocketOperationsTimeoutIsFullfilled(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 22", output: _output);
            // TODO: On Alpine, we need to investigate why there is only one pprof file and 2-3 empty profiles.
            // In the log we can see this message (seen with net6.0):
            // The profiler for application IntegrationTest-Samples.Computer01 (runtime id:b9f1d85c-6df4-46f4-83eb-f2c9f6722aea) have empty profile. Nothing will be sent.
            if (EnvironmentHelper.IsAlpine)
            {
                runner.MinimumExpectedNbPprofFiles = 1;
            }

            runner.RunAndCheck();
        }
    }
}
