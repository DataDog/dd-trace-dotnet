// <copyright file="MemoryFootprintTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using FluentAssertions;
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

        [TestAppFact(TestApplication.Computer01, TestFramework.Net11)]
        public void CheckMemoryFootprintMetricsWhenEnabled(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 10");
            // CPU and GC are disabled: the other default profilers stay on
            runner.Environment.EnableOnly(
                ProfilerFeature.WallTime,
                ProfilerFeature.Exceptions,
                ProfilerFeature.LockContention,
                ProfilerFeature.GcThreadsCpuTime,
                ProfilerFeature.Timeline,
                ProfilerFeature.MemoryFootprint);

            using var run = runner.Run();

            var footprintMetrics = GetMemoryFootprintMetrics(run.ProfileDirectory);
            footprintMetrics.Should().NotBeEmpty("at least one dotnet_memory_footprint_* metric should be present when enabled");

            foreach (var metric in footprintMetrics)
            {
                metric.Item2.Should().BeGreaterThanOrEqualTo(0, $"metric {metric.Item1} should have a non-negative value");
            }

            // Check that the shutdown log contains memory breakdown
            run.Logs.NativeProfiler.Files.Should().ContainSingle();
            run.Logs.NativeProfiler.Should().ContainLine("Profiler Memory Breakdown at Shutdown");
            run.Logs.NativeProfiler.Should().ContainLine("Total measured profiler memory");
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net11)]
        public void CheckMemoryFootprintMetricsAbsentWhenDisabled(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 10");
            // CPU and GC are disabled: the other default profilers stay on.
            // Do NOT enable MemoryFootprint - should be disabled by default
            runner.Environment.EnableOnly(
                ProfilerFeature.WallTime,
                ProfilerFeature.Exceptions,
                ProfilerFeature.LockContention,
                ProfilerFeature.GcThreadsCpuTime,
                ProfilerFeature.Timeline);

            using var run = runner.Run();

            var footprintMetrics = GetMemoryFootprintMetrics(run.ProfileDirectory);
            footprintMetrics.Should().BeEmpty("no memory footprint metrics should be present when feature is disabled");

            // Check that the shutdown log does NOT contain memory breakdown
            run.Logs.NativeProfiler.Files.Should().ContainSingle();
            run.Logs.NativeProfiler.Should().NotContainLine("Profiler Memory Breakdown at Shutdown");
            run.Logs.NativeProfiler.Should().NotContainLine("Total measured profiler memory");
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
    }
}
