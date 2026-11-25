// <copyright file="NamedPipeTestcs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Xunit;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.WindowsOnly
{
    [Trait("Category", "WindowsOnly")]
    public class NamedPipeTestcs
    {
        private const int RetryCount = 3;
        private readonly ITestOutputHelper _output;

        public NamedPipeTestcs(ITestOutputHelper output)
        {
            _output = output;
        }

        // NOTE: now that .NET Framework is supported by default, the profiler tries to connect
        //       to connect to the Agent using namedpipe. Since the Agent does not exist in CI,
        //       the ETW support is disabled in the tests for .NET Framework.

        [Flaky("Named pipes seem to be flaky in CI", RetryCount)]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckProfilesSentThroughNamedPipe(TestAppContext app)
        {
            string[] errorExceptions =
            {
                "ddog_prof_Exporter_send_blocking failed: Failed to send HTTP request: error sending request for url (http://localhost/profiling/v1/input): operation timed out",
                "ddog_prof_Exporter_send_blocking failed: operation was canceled"
            };
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 1", output: _output, transportType: TransportType.NamedPipe);
            if (app.IsNetFramework)
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            runner.RunAndCheck(errorExceptions);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckExporterDoesUsePipeNameEvenIfItDoesNotExist(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1");

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable(EnvironmentVariables.NamedPipeName, "ForSureThisPipeDoesNotExist__I_Hope");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            ProfilerLogSet.Read(runner.Environment.LogDir).NativeProfiler.Should()
                .ContainLine("Using agent endpoint windows:\\\\.\\pipe\\ForSureThisPipeDoesNotExist__I_Hope")
                .And.ContainLine("ddog_prof_Exporter_send_blocking failed")
                .And.ContainLine("The system cannot find the file specified");
        }
    }
}
