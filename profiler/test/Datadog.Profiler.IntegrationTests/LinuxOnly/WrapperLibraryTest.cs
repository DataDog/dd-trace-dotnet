// <copyright file="WrapperLibraryTest.cs" company="Datadog">
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

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class WrapperLibraryTest
    {
        private readonly ITestOutputHelper _output;

        public WrapperLibraryTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void EnsureProfilerIsDeactivatedIfNoWrapperLibrary(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output);

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable("LD_PRELOAD", string.Empty);

            using var agent = new MockDatadogAgent(_output);
            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*It's not safe to start the profiler. See previous log messages for more info.*");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void EnsureProfilerIsDeactivatedIfWrongPathToWrapperLibrary(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output);

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable("LD_PRELOAD", "/mnt/does_not_exist/Datadog.Linux.Wrapper.x64.so");

            using var agent = new MockDatadogAgent(_output);
            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*It's not safe to start the profiler. See previous log messages for more info.*");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void EnsureAppDoesNotCrashIfProfilerDeactivateAndTracerActivated(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true);

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable("LD_PRELOAD", string.Empty);

            using var agent = new MockDatadogAgent(_output);
            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);
            lines.Should().ContainMatch("*It's not safe to start the profiler. See previous log messages for more info.*");
        }
    }
}
