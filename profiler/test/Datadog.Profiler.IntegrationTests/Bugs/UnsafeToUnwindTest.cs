// <copyright file="UnsafeToUnwindTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    public class UnsafeToUnwindTest
    {
        private static readonly StackFrame UnsafeFrame = new("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:UnSafeToUnwind |fg: |sg:()");
        private static readonly StackFrame UnsafeExceptionFrame = new("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionUnsafeUnwind |fg: |sg:()");
        private static readonly StackFrame UnsafeContentionFrame = new("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:ContentionUnsafeToUnwind |fg: |sg:(object lockObj)");

        private static readonly StackFrame SafeFrame = new("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:SafeToUnwind |fg: |sg:()");
        private static readonly StackFrame SafeExceptionFrame = new("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionSafeUnwind |fg: |sg:()");
        private static readonly StackFrame SafeContentionFrame = new("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:ContentionSafeToUnwind |fg: |sg:(object lockObj)");

        private readonly ITestOutputHelper _output;

        public UnsafeToUnwindTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void AvoidUnwindingUnsafeExecution(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 28", enableTracer: true);
            runner.Environment.SetVariable(EnvironmentVariables.SkippedMethods, "Samples.Computer01.UnsafeToUnwind[Wrap_UnSafeToUnwind];Samples.Computer01.UnsafeToUnwind[Wrap_RaiseExceptionUnsafeUnwind];Samples.Computer01.UnsafeToUnwind[Wrap_ContentionUnsafeToUnwind]");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);

            samples.Should().NotBeNullOrEmpty("Not samples found");

            var uniqueFrames = GetFrames(samples);

            AssertNoUnsafeFramesInSamples(uniqueFrames, framework);

            AssertSafeFramesInSamples(uniqueFrames, framework);
        }

        [TestAppFact("Samples.Computer01")]
        public void AvoidUnwindingUnsafeExecution2(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 28", enableTracer: true);
            runner.Environment.SetVariable(EnvironmentVariables.SkippedMethods, "Samples.Computer01.UnsafeToUnwind[Wrap_UnSafeToUnwind,Wrap_RaiseExceptionUnsafeUnwind,Wrap_ContentionUnsafeToUnwind]");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);

            samples.Should().NotBeNullOrEmpty("Not samples found");

            var uniqueFrames = GetFrames(samples);

            AssertNoUnsafeFramesInSamples(uniqueFrames, framework);

            AssertSafeFramesInSamples(uniqueFrames, framework);
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckIfEnvVarDoesNotExist(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 28", enableTracer: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);

            samples.Should().NotBeNullOrEmpty();

            var uniqueFrames = GetFrames(samples);

            // In CI, this assert can make the test flaky. Keep it commented for documentation
            // uniqueFrames.Should().Contain(UnsafeFrame);

            uniqueFrames.Should().Contain(UnsafeExceptionFrame);
            if (framework == "net8.0")
            {
                uniqueFrames.Should().Contain(UnsafeContentionFrame);
            }
        }

        private static void AssertSafeFramesInSamples(HashSet<StackFrame> frames, string framework)
        {
            // In CI, this assert can make the test flaky. Keep it commented for documentation
            // frames.Should().Contain(SafeFrame);

            frames.Should().Contain(SafeExceptionFrame);
            if (framework == "net8.0")
            {
                frames.Should().Contain(SafeContentionFrame);
            }
        }

        private static void AssertNoUnsafeFramesInSamples(HashSet<StackFrame> frames, string framework)
        {
            frames.Should().NotContain(UnsafeFrame);
            frames.Should().NotContain(UnsafeExceptionFrame);
            if (framework == "net8.0")
            {
                frames.Should().NotContain(UnsafeContentionFrame);
            }
        }

        private static HashSet<StackFrame> GetFrames(IEnumerable<Sample> samples)
        {
            HashSet<StackFrame> uniqueFrames = [];

            foreach (var sample in samples)
            {
                foreach (var frame in sample.StackTrace)
                {
                    uniqueFrames.Add(frame);
                }
            }

            return uniqueFrames;
        }
    }
}
