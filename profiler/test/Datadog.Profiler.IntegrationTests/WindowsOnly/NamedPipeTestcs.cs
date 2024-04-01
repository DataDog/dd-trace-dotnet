// <copyright file="NamedPipeTestcs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using FluentAssertions;
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

        [TestAppFact("Samples.Computer01")]
        public void CheckProfilesSentThroughNamedPipe(string appName, string framework, string appAssembly)
        {
            string[] errorExceptions =
            {
                "failed ddog_prof_Exporter_send: operation timed out",
                "failed ddog_prof_Exporter_send: operation was canceled"
            };
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", output: _output, transportType: TransportType.NamedPipe);
            runner.RunAndCheckWithRetries(RetryCount, errorExceptions);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckExporterDoesUsePipeNameEvenIfItDoesNotExist(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1");

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable(EnvironmentVariables.NamedPipeName, "ForSureThisPipeDoesNotExist__I_Hope");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*Using agent endpoint windows:\\\\.\\pipe\\ForSureThisPipeDoesNotExist__I_Hope*");
            lines.Should().ContainMatch("*failed ddog_prof_Exporter_send: error trying to connect: The system cannot find the file specified. (os error 2):*");
        }
    }
}
