// <copyright file="SingleStepInstrumentationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.SingleStepInstrumentation
{
    [EnvironmentRestorerAttribute(EnvironmentVariables.SsiDeployed)]
    public class SingleStepInstrumentationTest
    {
        private readonly ITestOutputHelper _output;

        public SingleStepInstrumentationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void ManualAndProfilingEnvVarNotSet(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void ManualAndProfilingEnvVarTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void ManualAndProfilingEnvVarFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingEnvVarTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void NoMetricsWhenSsiAndProfilingEnvVarTrue(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(runner.Environment.PprofDir);
            Assert.True(parser == null, "Metrics files should not be generated");
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingEnvVarFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckSsiAndProfilingEnvVarSetToFalse(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series.AddRange(GetSeries(s));
            };

            runner.Run(agent);

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingNotSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);
            // short lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingNotSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingNotSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);
            // short lived with span
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingNotSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            ForceInjectionIfRequired(runner, framework);
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingSsiEnabled_ShortLivedAndNoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            ForceInjectionIfRequired(runner, framework);

            // short lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.Computer01")]
        public void SsiAndProfilingSsiEnabled_NoSpan(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingSsiEnabled_ShortLived(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            ForceInjectionIfRequired(runner, framework);

            // short lived with span
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingSsiEnabled_AllTriggered(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void SsiAndProfilingSsiEnabled_Delayed(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void StableConfigWhenProfilingDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // create a stable config json file to disable the profiler
            var stableConfigFilePath = GetStableConfigFilePath(runner, "false");
            runner.Environment.SetVariable("DD_TRACE_CONFIG_FILE", stableConfigFilePath);

            // don't set any env var to simulate the work done by the tracer
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
            AssertProfilingLogsContains(runner.Environment.LogDir, "Profiler is disabled via Stable Configuration");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void StableConfigWhenProfilingEnabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // create a stable config json file to disable the profiler
            var stableConfigFilePath = GetStableConfigFilePath(runner, "true");
            runner.Environment.SetVariable("DD_TRACE_CONFIG_FILE", stableConfigFilePath);

            // don't set any env var to simulate the work done by the tracer
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
            AssertProfilingLogsContains(runner.Environment.LogDir, "Profiler manually enabled");
            // "Profiling delayed" start should follow
        }

        [TestAppFact("Samples.BuggyBits")]
        public void StableConfigWhenProfilingAuto(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // create a stable config json file to disable the profiler
            var stableConfigFilePath = GetStableConfigFilePath(runner, "auto");
            runner.Environment.SetVariable("DD_TRACE_CONFIG_FILE", stableConfigFilePath);

            // don't set any env var to simulate the work done by the tracer
            ForceInjectionIfRequired(runner, framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            AssertProfilingLogsContains(runner.Environment.LogDir, "Profiler enabled by SSI");
        }

        [TestAppFact("Samples.Computer01")]
        public void StableConfigWhenKillSwitchReadsEnvVars(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 1", enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);
            series.Should().BeEmpty();
            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);

            AssertProfilingLogsDoNotContain(runner.Environment.LogDir, "SetConfiguration called by Managed code");
        }

        private void AssertProfilingLogsDoNotContain(string logDir, string unexpectedContent)
        {
            var logFile = Directory.GetFiles(logDir)
                                   .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));
            var found = false;
            foreach (var line in File.ReadLines(logFile))
            {
                if (line.Contains(unexpectedContent))
                {
                    found = true;
                }
            }

            Assert.False(found, $"Log should not contain: {unexpectedContent}");
        }

        private void AssertProfilingLogsContains(string logDir, string expectedContent)
        {
            var logFile = Directory.GetFiles(logDir)
                                   .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));
            var found = false;
            foreach (var line in File.ReadLines(logFile))
            {
                if (line.Contains(expectedContent))
                {
                    found = true;
                }
            }

            Assert.True(found, $"No log line contains: {expectedContent}");
        }

        private static string GetStableConfigFilePath(TestApplicationRunner runner, string profilingEnabledValue)
        {
            var filePath = Path.Combine(runner.TestOutputDir, "stableconfig.json");

            // write {"DD_TRACE_DEBUG": true, "DD_PROFILING_ENABLED": profilingEnabledValue} into the file
            string envVars;
            if (profilingEnabledValue == "auto")
            {
                envVars = "{" +
                    "\"DD_TRACE_DEBUG\": true, " +
                    $"\"DD_PROFILING_ENABLED\": \"{profilingEnabledValue}\"" +
                "}";
            }
            else
            {
                envVars = "{" +
                    "\"DD_TRACE_DEBUG\": true, " +
                    $"\"DD_PROFILING_ENABLED\": {profilingEnabledValue}" +
                "}";
            }

            File.WriteAllText(filePath, envVars);

            return filePath;
        }

        private static string GetRequestText(HttpListenerRequest request)
        {
            var text = string.Empty;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            return text;
        }

        private static List<Serie> GetSeries(string s)
        {
            var x = JObject.Parse(s);
            return x.SelectTokens("$.payload[*].payload.series[?(@.namespace=='profilers')]")?.Select(serie => serie.ToObject<Serie>())?.ToList() ?? [];
        }

        private static void ForceInjectionIfRequired(TestApplicationRunner runner, string framework)
        {
            // For preview and old runtimes we have to force injection
            // after .NET 10 preview, can remove this
            if (framework == "net10.0")
            {
                runner.Environment.SetVariable(EnvironmentVariables.SsiInjectionForced, "1");
            }

            if (EnvironmentHelper.IsRunningOnWindows())
            {
                // in SSI on Windows log buffering is enabled, which means we don't write
                // any logs unless we inject, which makes debugging issues harder
                runner.Environment.SetVariable(EnvironmentVariables.SsiLogBufferingEnabled, "0");
            }
        }

        private class Serie
        {
            public string @Namespace { get; set; }
            public string Metric { get; set; }
            public string[] Tags { get; set; }
        }
    }
}
