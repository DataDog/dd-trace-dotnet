// <copyright file="UnixDomainSocketBug.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    public class UnixDomainSocketBug
    {
        private readonly ITestOutputHelper _output;

        public UnixDomainSocketBug(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void MustUseHttpIfUDS_DoesNotExist(string appName, string framework, string appAssembly)
        {
            var runner = new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", output: _output);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.AgentUrl, "unix:///non_existent/socket");
            runner.RunAndCheck();

            var logFile = Directory.GetFiles(runner.EnvironmentHelper.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var nbSignalHandlerInstallation = File.ReadLines(logFile)
                .Count(l => l.Contains("Env var 'DD_TRACE_AGENT_URL' contains a path to a non-existent UDS 'unix:///non_existent/socket'. Fallback to default (HTTP)."));
        }
    }
}
