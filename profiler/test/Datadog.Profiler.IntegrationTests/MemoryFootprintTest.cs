// <copyright file="MemoryFootprintTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class MemoryFootprintTest
    {
        private const string MemoryFootprintMetricPrefix = "dotnet_memory_footprint_";

        private readonly ITestOutputHelper _output;

        public MemoryFootprintTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckMemoryFootprintMetricsWhenEnabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 10");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var footprintMetrics = GetMemoryFootprintMetrics(runner.Environment.PprofDir);
            footprintMetrics.Should().NotBeEmpty("at least one dotnet_memory_footprint_* metric should be present when enabled");

            foreach (var metric in footprintMetrics)
            {
                metric.Item2.Should().BeGreaterOrEqualTo(0, $"metric {metric.Item1} should have a non-negative value");
            }

            // Check that the shutdown log contains memory breakdown
            var lines = GetNativeProfilerLogLines(runner.Environment.LogDir);
            lines.Should().ContainMatch("*Profiler Memory Breakdown at Shutdown*");
            lines.Should().ContainMatch("*Total measured profiler memory*");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckMemoryFootprintMetricsAbsentWhenDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 10");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            // Do NOT set MemoryFootprintEnabled - should be disabled by default

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var footprintMetrics = GetMemoryFootprintMetrics(runner.Environment.PprofDir);
            footprintMetrics.Should().BeEmpty("no memory footprint metrics should be present when feature is disabled");

            // Check that the shutdown log does NOT contain memory breakdown
            var lines = GetNativeProfilerLogLines(runner.Environment.LogDir);
            lines.Should().NotContainMatch("*Profiler Memory Breakdown at Shutdown*");
            lines.Should().NotContainMatch("*Total measured profiler memory*");
        }

        private static List<Tuple<string, double>> GetMemoryFootprintMetrics(string pprofDir)
        {
            var footprintMetrics = new List<Tuple<string, double>>();

            var metricsFiles = Directory.GetFiles(pprofDir, "metrics_*.json");
            foreach (var metricsFile in metricsFiles)
            {
                var metrics = MetricHelper.GetMetrics(metricsFile);
                footprintMetrics.AddRange(
                    metrics.Where(m => m.Item1.StartsWith(MemoryFootprintMetricPrefix)));
            }

            return footprintMetrics;
        }

        private static string[] GetNativeProfilerLogLines(string logDir)
        {
            var logFile = Directory.GetFiles(logDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            return File.ReadAllLines(logFile);
        }
    }
}
