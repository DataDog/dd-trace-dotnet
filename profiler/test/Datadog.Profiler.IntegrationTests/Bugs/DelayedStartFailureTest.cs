// <copyright file="DelayedStartFailureTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Xunit;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    // Regression tests for SCP-1413 / APMS-20766.
    [EnvironmentRestorerAttribute(EnvironmentVariables.SsiDeployed)]
    public class DelayedStartFailureTest
    {
        private const string DelayedStartFailureLog = "One or multiple services failed to start after a delay";
        private const string PendingSamplesMetric = "dotnet_pending_raw_samples";

        private readonly ITestOutputHelper _output;

        public DelayedStartFailureTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void ControlRun_DelayedStartSucceeds(string appName, string framework, string appAssembly)
        {
            var runner = CreateDelayedStartRunner(appName, framework, appAssembly, _output);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            AssertLogDoesNotContain(runner.Environment.LogDir, DelayedStartFailureLog);
            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0, "the delayed start must succeed when the trigger is not applied");

            // Do not assert on PendingSamplesMetric here
            // In success run, the metrics are dumped in the log *before* flushing the SamplesCollector, which means that
            // the count could be > 0.
        }

        [TestAppFact("Samples.BuggyBits")]
        public void NoPendingSamplesWhenDelayedStartFails(string appName, string framework, string appAssembly)
        {
            var runner = CreateDelayedStartRunner(appName, framework, appAssembly, _output);
            ForceDelayedStartFailure(runner);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var logDir = runner.Environment.LogDir;

            // 1. the failure path really ran (otherwise the assertions below are vacuous)
            AssertLogContains(logDir, DelayedStartFailureLog);
            AssertLogContains(logDir, "[TEST ONLY] StackSamplerLoopManager::RunWatcher - the watcher start timeout is 0: not waiting for the watcher thread and failing the start.");

            // 2. the profiler stayed stopped
            agent.NbCallsOnProfilingEndpoint.Should().Be(0, "no profile can be exported once the delayed start failed");

            // 3. the regression itself: nothing accumulated in the providers
            var pendingSamples = GetShutdownMetric(logDir, PendingSamplesMetric);
            pendingSamples.Should().NotBeNull($"'{PendingSamplesMetric}' must be dumped at shutdown");
            pendingSamples.Should().Be(0, "no sample may be created once the SamplesCollector is stopped for good");
        }

        // Reaches the SSI delayed start: deployed + enabled by SSI, long lived, and BuggyBits creates spans.
        // Same recipe as SingleStepInstrumentationTest.SsiAndProfilingSsiEnabled_AllTriggered.
        private static TestApplicationRunner CreateDelayedStartRunner(
            string appName,
            string framework,
            string appAssembly,
            ITestOutputHelper output,
            string commandLine = "--scenario 1")
        {
            var runner = new TestApplicationRunner(
                appName,
                framework,
                appAssembly,
                output,
                commandLine: commandLine,
                enableTracer: true,
                enableProfiler: false);

            runner.Environment.SetVariable(EnvironmentVariables.SsiDeployed, "tracer");
            runner.Environment.SetVariable(EnvironmentVariables.ProfilerEnabled, "auto");
            EnvironmentHelper.ForceInjectionIfRequired(runner, framework);

            // simulate long lived so the heuristics trigger quickly
            runner.Environment.SetVariable(EnvironmentVariables.SsiShortLivedThreshold, "1");

            // the metrics registry and the memory breakdown are only dumped at shutdown when this is on,
            // which is how the assertions observe the profiler without any profile being exported
            runner.Environment.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            runner.Environment.SetVariable("DD_TRACE_DEBUG", "1");

            return runner;
        }

        private static void ForceDelayedStartFailure(TestApplicationRunner runner)
        {
            runner.Environment.SetVariable(EnvironmentVariables.WatcherStartTimeout, "0");
        }

        private static string GetNativeLogFile(string logDir)
        {
            return Directory.GetFiles(logDir)
                            .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));
        }

        // Reads a value out of the "=== Profiler Metrics at Shutdown ===" block, which CorProfilerCallback
        // dumps from the metrics registry so the counters are observable even when nothing is exported.
        private static double? GetShutdownMetric(string logDir, string metricName)
        {
            var pattern = new Regex(@"\s" + Regex.Escape(metricName) + @":\s*(?<value>-?[0-9]+(\.[0-9]+)?)");

            double? found = null;
            foreach (var line in File.ReadLines(GetNativeLogFile(logDir)))
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    // keep the last occurrence: that is the value at shutdown
                    found = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
                }
            }

            return found;
        }

        private static void AssertLogContains(string logDir, string expectedContent)
        {
            var found = File.ReadLines(GetNativeLogFile(logDir)).Any(line => line.Contains(expectedContent));
            Assert.True(found, $"No log line contains: {expectedContent}");
        }

        private static void AssertLogDoesNotContain(string logDir, string unexpectedContent)
        {
            var found = File.ReadLines(GetNativeLogFile(logDir)).Any(line => line.Contains(unexpectedContent));
            Assert.False(found, $"Log should not contain: {unexpectedContent}");
        }
    }
}
