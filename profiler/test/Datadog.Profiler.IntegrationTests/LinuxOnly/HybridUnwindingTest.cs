// <copyright file="HybridUnwindingTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class HybridUnwindingTest
    {
        private readonly ITestOutputHelper _output;

        public HybridUnwindingTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckHybridUnwinding(string appName, string framework, string appAssembly)
        {
            // Validates hybrid unwinding captures deep managed call chains (Level1->Level2->Level3->Level4->Level5)
            // Scenario 23 = ManagedStackExercise
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 10");
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "wall").ToList();
            samples.Should().NotBeEmpty("should have wall samples");

            var samplesWithManagedFrames = samples.Where(s =>
                Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .Any(name => name.Contains("ManagedStackExercise") || name.Contains("Level"))
            ).ToList();

            samplesWithManagedFrames.Should().NotBeEmpty("should find samples with ManagedStackExercise frames");

            var deepestStack = samplesWithManagedFrames
                .Select(s => Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .Count(name => name.Contains("Level")))
                .Max();

            deepestStack.Should().BeGreaterOrEqualTo(3, "should capture at least 3 levels of managed recursion");

            _output.WriteLine($"Captured {samplesWithManagedFrames.Count} samples with managed frames");
            _output.WriteLine($"Deepest managed stack: {deepestStack} levels");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckHybridUnwindingForCpuProfiler(string appName, string framework, string appAssembly)
        {
            // Validates hybrid unwinding works for CPU profiling
            // Scenario 23 = ManagedStackExercise
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 10");
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "cpu").ToList();
            samples.Should().NotBeEmpty("should have CPU samples");

            var samplesWithManagedFrames = samples.Where(s =>
                Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .Any(name => name.Contains("ManagedStackExercise") || name.Contains("Level") || name.Contains("OnProcess"))
            ).ToList();

            samplesWithManagedFrames.Should().NotBeEmpty("CPU profiler should capture managed frames with hybrid unwinding");
            _output.WriteLine($"✓ CPU profiler captured {samplesWithManagedFrames.Count} samples with managed frames");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckHybridUnwindingVsStandardUnwinding(string appName, string framework, string appAssembly)
        {
            // Compares hybrid unwinding against standard libunwind-only unwinding
            // Scenario 23 = ManagedStackExercise
            var runnerStandard = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 8");
            EnvironmentHelper.DisableDefaultProfilers(runnerStandard);
            runnerStandard.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runnerStandard.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "0");

            using (var agent = MockDatadogAgent.CreateHttpAgent(runnerStandard.XUnitLogger))
            {
                runnerStandard.Run(agent);
            }

            var standardSamplesCount = SamplesHelper.GetSamplesCount(runnerStandard.Environment.PprofDir);
            var standardSamples = SamplesHelper.GetSamples(runnerStandard.Environment.PprofDir, sampleTypeFilter: "wall").ToList();
            var standardAvgDepth = standardSamples.Any() ? standardSamples.Average(s => s.StackTrace.FramesCount) : 0;

            _output.WriteLine($"Standard unwinding: {standardSamplesCount} samples, avg depth: {standardAvgDepth:F1}");

            var runnerHybrid = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 8");
            EnvironmentHelper.DisableDefaultProfilers(runnerHybrid);
            runnerHybrid.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runnerHybrid.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");

            using (var agent = MockDatadogAgent.CreateHttpAgent(runnerHybrid.XUnitLogger))
            {
                runnerHybrid.Run(agent);
            }

            var hybridSamplesCount = SamplesHelper.GetSamplesCount(runnerHybrid.Environment.PprofDir);
            var hybridSamples = SamplesHelper.GetSamples(runnerHybrid.Environment.PprofDir, sampleTypeFilter: "wall").ToList();
            var hybridAvgDepth = hybridSamples.Any() ? hybridSamples.Average(s => s.StackTrace.FramesCount) : 0;

            _output.WriteLine($"Hybrid unwinding: {hybridSamplesCount} samples, avg depth: {hybridAvgDepth:F1}");

            hybridSamplesCount.Should().BeGreaterOrEqualTo((int)(standardSamplesCount * 0.8),
                "hybrid unwinding should not lose more than 20% of samples");
            hybridAvgDepth.Should().BeGreaterOrEqualTo(standardAvgDepth * 0.9,
                "hybrid unwinding should maintain or improve stack depth");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckNoRegressionWithHybridUnwinding(string appName, string framework, string appAssembly)
        {
            // Ensures hybrid unwinding doesn't introduce crashes or hangs with multiple profilers
            // Scenario 23 = ManagedStackExercise
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 10");
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var cpuSamples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "cpu").ToList();
            var wallSamples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "wall").ToList();

            cpuSamples.Should().NotBeEmpty("CPU profiler should work with hybrid unwinding");
            wallSamples.Should().NotBeEmpty("Wall-time profiler should work with hybrid unwinding");

            // Verify we capture deep managed stacks with Level frames
            var samplesWithLevelFrames = wallSamples.Where(s =>
                Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .Any(name => name.Contains("Level"))
            ).ToList();

            samplesWithLevelFrames.Should().NotBeEmpty("should capture samples with Level frames");

            var maxLevelDepth = samplesWithLevelFrames
                .Select(s => Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .Count(name => name.Contains("Level")))
                .Max();

            maxLevelDepth.Should().BeGreaterOrEqualTo(3, "should capture at least 3 Level frames in call chain");

            _output.WriteLine($"  CPU samples: {cpuSamples.Count}, Wall-time samples: {wallSamples.Count}");
            _output.WriteLine($"  Max Level depth captured: {maxLevelDepth}");
        }
    }
}
