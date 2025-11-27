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
        public void CheckHybridUnwindingCapturesDeepManagedStacks(string appName, string framework, string appAssembly)
        {
            // This test validates that hybrid unwinding correctly captures deep managed call chains
            // for the ManagedStackExercise scenario which has Level1->Level2->Level3->Level4->Level5
            
            // Scenario 23 = ManagedStackExercise (deep recursive call chain)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 10");
            
            // Enable hybrid unwinding (experimental feature)
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");
            
            // Disable all profilers except wall-time to simplify analysis
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // Verify we captured samples
            var samplesCount = SamplesHelper.GetSamplesCount(runner.Environment.PprofDir);
            samplesCount.Should().BeGreaterThan(0, "should have captured wall-time samples");

            // Check that we see the expected managed call chain
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "wall-time").ToList();
            samples.Should().NotBeEmpty("should have wall-time samples");

            // Look for samples that contain our test methods
            var samplesWithManagedFrames = samples.Where(s =>
            {
                var frameNames = Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .ToList();
                return frameNames.Any(name => name.Contains("ManagedStackExercise") || name.Contains("Level"));
            }).ToList();

            samplesWithManagedFrames.Should().NotBeEmpty("should find samples with ManagedStackExercise frames");

            // Verify stack depth - we should see at least 3-4 levels of our recursive calls
            var deepestStack = samplesWithManagedFrames
                .Select(s =>
                {
                    var frames = Enumerable.Range(0, s.StackTrace.FramesCount)
                        .Select(i => s.StackTrace[i].Function)
                        .ToList();
                    var managedFrames = frames.Where(name => name.Contains("Level")).ToList();
                    return managedFrames.Count;
                })
                .Max();

            deepestStack.Should().BeGreaterOrEqualTo(3, "should capture at least 3 levels of managed recursion");

            _output.WriteLine($"Captured {samplesWithManagedFrames.Count} samples with managed frames");
            _output.WriteLine($"Deepest managed stack: {deepestStack} levels");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckHybridUnwindingForCpuProfiler(string appName, string framework, string appAssembly)
        {
            // This test validates that hybrid unwinding works for CPU profiling too
            
            // Scenario 23 = ManagedStackExercise
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 10");
            
            // Enable hybrid unwinding
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");
            
            // Disable all profilers except CPU
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // Verify we captured samples
            var samplesCount = SamplesHelper.GetSamplesCount(runner.Environment.PprofDir);
            samplesCount.Should().BeGreaterThan(0, "should have captured CPU samples");

            // Check that we see managed frames in CPU profiles
            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "cpu-time").ToList();
            samples.Should().NotBeEmpty("should have CPU samples");

            var samplesWithManagedFrames = samples.Where(s =>
            {
                var frameNames = Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .ToList();
                return frameNames.Any(name => name.Contains("ManagedStackExercise") || name.Contains("Level") || name.Contains("OnProcess"));
            }).ToList();

            samplesWithManagedFrames.Should().NotBeEmpty("CPU profiler should capture managed frames with hybrid unwinding");

            _output.WriteLine($"✓ CPU profiler captured {samplesWithManagedFrames.Count} samples with managed frames");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckHybridUnwindingCapturesParentFrames(string appName, string framework, string appAssembly)
        {
            // This test validates that we capture the full call chain, including parent frames above the hot loop
            
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario PiComputation --timeout 10");
            
            // Enable hybrid unwinding
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");
            
            // Disable all profilers except wall-time
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "wall-time").ToList();
            samples.Should().NotBeEmpty("should have wall-time samples");

            // Look for samples that show the full hierarchy: DoPiComputation -> OnProcess
            var samplesWithFullChain = samples.Where(s =>
            {
                var frameNames = Enumerable.Range(0, s.StackTrace.FramesCount)
                    .Select(i => s.StackTrace[i].Function)
                    .ToList();
                var hasDoPiComputation = frameNames.Any(name => name.Contains("DoPiComputation"));
                var hasOnProcess = frameNames.Any(name => name.Contains("OnProcess"));
                return hasDoPiComputation && hasOnProcess;
            }).ToList();

            samplesWithFullChain.Should().NotBeEmpty(
                "should capture full call chain from DoPiComputation up to OnProcess");

            _output.WriteLine($"Captured {samplesWithFullChain.Count} samples with complete call chain");

            // Print a sample stack for debugging
            if (samplesWithFullChain.Any())
            {
                var sampleStack = samplesWithFullChain.First();
                var frames = Enumerable.Range(0, sampleStack.StackTrace.FramesCount)
                    .Select(i => sampleStack.StackTrace[i].Function)
                    .ToList();
                _output.WriteLine("Sample stack trace:");
                foreach (var frame in frames)
                {
                    _output.WriteLine($"  - {frame}");
                }
            }
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckHybridUnwindingVsStandardUnwinding(string appName, string framework, string appAssembly)
        {
            // This test compares hybrid unwinding against standard libunwind-only unwinding
            // to ensure we're not losing frames or introducing regressions
            
            // First run: Standard unwinding (hybrid disabled)
            // Scenario 23 = ManagedStackExercise
            var runnerStandard = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 8");
            EnvironmentHelper.DisableDefaultProfilers(runnerStandard);
            runnerStandard.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            // Explicitly disable hybrid unwinding
            runnerStandard.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "0");

            using (var agent = MockDatadogAgent.CreateHttpAgent(runnerStandard.XUnitLogger))
            {
                runnerStandard.Run(agent);
            }

            var standardSamplesCount = SamplesHelper.GetSamplesCount(runnerStandard.Environment.PprofDir);
            var standardSamples = SamplesHelper.GetSamples(runnerStandard.Environment.PprofDir, sampleTypeFilter: "wall-time").ToList();
            var standardAvgDepth = standardSamples.Any()
                ? standardSamples.Average(s => s.StackTrace.FramesCount)
                : 0;

            _output.WriteLine($"Standard unwinding: {standardSamplesCount} samples, avg depth: {standardAvgDepth:F1}");

            // Second run: Hybrid unwinding
            // Scenario 23 = ManagedStackExercise
            var runnerHybrid = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 23 --timeout 8");
            EnvironmentHelper.DisableDefaultProfilers(runnerHybrid);
            runnerHybrid.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runnerHybrid.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");

            using (var agent = MockDatadogAgent.CreateHttpAgent(runnerHybrid.XUnitLogger))
            {
                runnerHybrid.Run(agent);
            }

            var hybridSamplesCount = SamplesHelper.GetSamplesCount(runnerHybrid.Environment.PprofDir);
            var hybridSamples = SamplesHelper.GetSamples(runnerHybrid.Environment.PprofDir, sampleTypeFilter: "wall-time").ToList();
            var hybridAvgDepth = hybridSamples.Any()
                ? hybridSamples.Average(s => s.StackTrace.FramesCount)
                : 0;

            _output.WriteLine($"Hybrid unwinding: {hybridSamplesCount} samples, avg depth: {hybridAvgDepth:F1}");

            // Verify hybrid unwinding produces at least as many samples
            hybridSamplesCount.Should().BeGreaterOrEqualTo(
                (int)(standardSamplesCount * 0.8),
                "hybrid unwinding should not lose more than 20% of samples");

            // Verify hybrid unwinding captures deeper or similar stacks
            hybridAvgDepth.Should().BeGreaterOrEqualTo(
                standardAvgDepth * 0.9,
                "hybrid unwinding should maintain or improve stack depth");

            _output.WriteLine($"Hybrid unwinding quality check passed");
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckNoRegressionWithHybridUnwinding(string appName, string framework, string appAssembly)
        {
            // This test ensures hybrid unwinding doesn't introduce crashes or hangs
            
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario PiComputation --timeout 15");
            
            // Enable hybrid unwinding
            runner.Environment.SetVariable(EnvironmentVariables.InternalUseHybridUnwinding, "1");
            
            // Enable multiple profilers to stress test
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            // This will throw if the app crashes or hangs
            runner.Run(agent);

            // Verify we got samples from both profilers
            var cpuSamplesCount = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "cpu-time").Count();
            var wallTimeSamplesCount = SamplesHelper.GetSamples(runner.Environment.PprofDir, sampleTypeFilter: "wall-time").Count();

            cpuSamplesCount.Should().BeGreaterThan(0, "CPU profiler should work with hybrid unwinding");
            wallTimeSamplesCount.Should().BeGreaterThan(0, "Wall-time profiler should work with hybrid unwinding");

            _output.WriteLine($"  No crashes or hangs with hybrid unwinding enabled");
            _output.WriteLine($"  CPU samples: {cpuSamplesCount}, Wall-time samples: {wallTimeSamplesCount}");
        }
    }
}

