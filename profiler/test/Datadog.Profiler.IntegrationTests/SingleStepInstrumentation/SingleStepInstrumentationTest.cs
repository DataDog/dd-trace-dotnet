// <copyright file="SingleStepInstrumentationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.SingleStepInstrumentation
{
    public class SingleStepInstrumentationTest
    {
        private readonly ITestOutputHelper _output;

        public SingleStepInstrumentationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckManuallyDeployedAndProfilingEnvVarNotSet(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Manual*");
            lines.Should().NotContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler environment variable 'DD_PROFILING_ENABLED' was not set. The .NET profiler will be disabled.*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckManuallyDeployedAndProfilingEnvVarSetToTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Manual*");
            lines.Should().ContainMatch("*.NET Profiler is enabled.*");
            lines.Should().NotContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*ProcessStart(Manual)*");
            lines.Should().ContainMatch("*ProcessEnd(Manual*");
            lines.Should().NotContainMatch("*Process*(Single Step Instrumentation*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckManuallyDeployedAndProfilingEnvVarSetToFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Manual*");
            lines.Should().NotContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is disabled.*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
            lines.Should().NotContainMatch("*Process*(Single Step Instrumentation*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingenvVarSetToTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is enabled.*");
            lines.Should().NotContainMatch("*.NET Profiler is enabled using Single Step Instrumentation limited activation.*");
            lines.Should().ContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().ContainMatch("*ProcessEnd(Single Step Instrumentation*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingEnvVarSetToFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is disabled.*");
            lines.Should().ContainMatch("*DllGetClassObject(): Profiling is not enabled.*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
            lines.Should().NotContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().NotContainMatch("*ProcessEnd(Single Step Instrumentation*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingNotSsiEnabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is enabled using Single Step Instrumentation limited activation.*");
            // check it's telemetry only
            // no service started ?
            lines.Should().ContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().ContainMatch("*ProcessEnd(Single Step Instrumentation*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // No need to tweak the SsiShortLivedThreshold env variable to simulate a shortlived app.
            // This app runs for ~10s and the threshold is 30s.

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is enabled using Single Step Instrumentation limited activation.*");
            lines.Should().ContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().ContainMatch("*ProcessEnd(Single Step Instrumentation, ShortLived | NoSpan*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is enabled using Single Step Instrumentation limited activation.*");
            lines.Should().ContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().ContainMatch("*ProcessEnd(Single Step Instrumentation, NoSpan)*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // short lived with span

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is enabled using Single Step Instrumentation limited activation.*");
            lines.Should().ContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().ContainMatch("*ProcessEnd(Single Step Instrumentation, ShortLived)*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSsiDeployedAndProfilingSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false, enableTracer: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "profiler");
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);

            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            var lines = File.ReadAllLines(logFile);

            lines.Should().ContainMatch("*.NET Profiler deployment mode: Single Step Instrumentation*");
            lines.Should().ContainMatch("*.NET Profiler is enabled using Single Step Instrumentation limited activation.*");
            lines.Should().ContainMatch("*ProcessStart(Single Step Instrumentation)*");
            lines.Should().ContainMatch("*ProcessEnd(Single Step Instrumentation, AllTriggered)*");
            lines.Should().NotContainMatch("*ProcessStart(Manual)*");
            lines.Should().NotContainMatch("*ProcessEnd(Manual*");
        }
    }
}
