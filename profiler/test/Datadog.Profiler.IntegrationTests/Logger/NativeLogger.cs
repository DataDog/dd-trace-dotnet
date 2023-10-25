// <copyright file="NativeLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Logger
{
    public class NativeLogger
    {
        private readonly ITestOutputHelper _output;

        public NativeLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        // only one framework suffice for this test
        [TestAppFact("Samples.Computer01", frameworks: new[] { "net7.0" })]
        public void EnsureBackCompatibilityForLogDirectory(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1");
            runner.TestDurationInSeconds = 2;

            var deprecatedLogPath = Path.Combine(Path.GetTempPath(), "deprecated", System.Environment.ProcessId.ToString());
            runner.Environment.SetVariable("DD_PROFILING_LOG_DIR", deprecatedLogPath);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            Directory.GetFiles(deprecatedLogPath)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"))
                .Should()
                .NotBeNullOrEmpty();

            Directory.GetFiles(runner.Environment.LogDir)
                .SingleOrDefault(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"))
                .Should()
                .BeNullOrEmpty();
        }
    }
}
