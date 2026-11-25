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
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Newtonsoft.Json.Linq;
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

        [Flaky("Samples.Computer01 intermittently crashes with SIGSEGV; under investigation")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void ManualAndProfilingEnvVarNotSet(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);
            agent.HandlerExceptions.Should().BeEmpty();

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [Flaky("Samples.Computer01 intermittently crashes with SIGSEGV; under investigation")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void ManualAndProfilingEnvVarTrue(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);
            agent.HandlerExceptions.Should().BeEmpty();

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void ManualAndProfilingEnvVarFalse(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);
            agent.HandlerExceptions.Should().BeEmpty();

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [Flaky("Samples.Computer01 intermittently crashes with SIGSEGV; under investigation")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void SsiAndProfilingEnvVarTrue(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            using var run = runner.Run();
            run.UploadCount.Should().BeGreaterThan(0);
        }

        [Flaky("Samples.Computer01 intermittently crashes with SIGSEGV; under investigation")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void NoMetricsWhenSsiAndProfilingEnvVarTrue(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: true);

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);
            runner.Environment.SetVariable(EnvironmentVariables.SsiTelemetryEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.TelemetryToDiskEnabled, "1");

            using var run = runner.Run();
            run.UploadCount.Should().BeGreaterThan(0);

            var parser = TelemetryMetricsFileParser.LoadFromDirectory(run.ProfileDirectory);
            parser.Should().BeNull("Metrics files should not be generated");
        }

        [Flaky("Samples.Computer01 intermittently crashes with SIGSEGV; under investigation")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void SsiAndProfilingEnvVarFalse(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);
            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckSsiAndProfilingEnvVarSetToFalse(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);
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
            agent.HandlerExceptions.Should().BeEmpty();

            series.Should().BeEmpty();

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void SsiAndProfilingNotSsiEnabled_ShortLivedAndNoSpan(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);
            // short lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void SsiAndProfilingNotSsiEnabled_NoSpan(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void SsiAndProfilingNotSsiEnabled_ShortLived(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);
            // short lived with span
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void SsiAndProfilingNotSsiEnabled_AllTriggered(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "false");

            // deployed with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);
            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void SsiAndProfilingSsiEnabled_ShortLivedAndNoSpan(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // short lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void SsiAndProfilingSsiEnabled_NoSpan(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void SsiAndProfilingSsiEnabled_ShortLived(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // short lived with span
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "600000");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void SsiAndProfilingSsiEnabled_AllTriggered(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            using var run = runner.Run();

            run.UploadCount.Should().NotBe(0);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void SsiAndProfilingSsiEnabled_Delayed(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // deployed and enabled with SSI
            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var run = runner.Run();

            run.UploadCount.Should().NotBe(0);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void StableConfigWhenProfilingDisabled(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // create a stable config json file to disable the profiler
            var stableConfigFilePath = GetStableConfigFilePath(runner, "false");
            runner.Environment.SetVariable("DD_TRACE_CONFIG_FILE", stableConfigFilePath);

            // don't set any env var to simulate the work done by the tracer
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            agent.NbCallsOnProfilingEndpoint.Should().Be(0);
            var logs = ProfilerLogSet.Read(runner.Environment.LogDir);
            logs.NativeProfiler.Files.Should().ContainSingle();
            logs.NativeProfiler.Should().ContainLine("Profiler is disabled via Stable Configuration");
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void StableConfigWhenProfilingEnabled(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // create a stable config json file to disable the profiler
            var stableConfigFilePath = GetStableConfigFilePath(runner, "true");
            runner.Environment.SetVariable("DD_TRACE_CONFIG_FILE", stableConfigFilePath);

            // don't set any env var to simulate the work done by the tracer
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var run = runner.Run();

            run.UploadCount.Should().NotBe(0);
            run.Logs.NativeProfiler.Files.Should().ContainSingle();
            run.Logs.NativeProfiler.Should().ContainLine("Profiler manually enabled");
            // "Profiling delayed" start should follow
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void StableConfigWhenProfilingAuto(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true, enableProfiler: false);

            // create a stable config json file to disable the profiler
            var stableConfigFilePath = GetStableConfigFilePath(runner, "auto");
            runner.Environment.SetVariable("DD_TRACE_CONFIG_FILE", stableConfigFilePath);

            // don't set any env var to simulate the work done by the tracer
            EnvironmentHelper.ForceInjectionIfRequired(runner, app.Framework);

            // simulate long lived
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, TimeSpan.FromSeconds(6).TotalMilliseconds.ToString());

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var logs = ProfilerLogSet.Read(runner.Environment.LogDir);
            logs.NativeProfiler.Files.Should().ContainSingle();
            logs.NativeProfiler.Should().ContainLine("Profiler enabled by SSI");
        }

        [Flaky("Samples.Computer01 intermittently crashes with SIGSEGV; under investigation")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void StableConfigWhenKillSwitchReadsEnvVars(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableProfiler: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            List<Serie> series = [];
            agent.TelemetryMetricsRequestReceived += (_, ctx) =>
            {
                var s = GetRequestText(ctx.Value.Request);
                series = GetSeries(s);
            };

            runner.Run(agent);
            agent.HandlerExceptions.Should().BeEmpty();
            series.Should().BeEmpty();
            agent.NbCallsOnProfilingEndpoint.Should().NotBe(0);

            var logs = ProfilerLogSet.Read(runner.Environment.LogDir);
            logs.NativeProfiler.Files.Should().ContainSingle();
            logs.NativeProfiler.Should().NotContainLine("SetConfiguration called by Managed code");
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
            return HttpRequestBodyReader.ReadBodyAsText(request);
        }

        private static List<Serie> GetSeries(string s)
        {
            var x = JObject.Parse(s);
            return x.SelectTokens("$.payload[*].payload.series[?(@.namespace=='profilers')]")?.Select(serie => serie.ToObject<Serie>())?.ToList() ?? [];
        }

        private class Serie
        {
            public string @Namespace { get; set; }
            public string Metric { get; set; }
            public string[] Tags { get; set; }
        }
    }
}
