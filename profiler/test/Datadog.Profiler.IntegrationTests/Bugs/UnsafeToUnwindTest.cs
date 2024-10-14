// <copyright file="UnsafeToUnwindTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    public class UnsafeToUnwindTest
    {
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

            var toSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:UnSafeToUnwind |fg: |sg:()"));

            var exceptionToSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionUnsafeUnwind |fg: |sg:()"));

            var exceptionToKeepStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionSafeUnwind |fg: |sg:()"));

            // Event based profiler
            var contentionToSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:ContentionUnsafeToUnwind |fg: |sg:()"));

            var contentionToKeepStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:ContentionSafeToUnwind |fg: |sg:()"));

            // Safe to unwind frame
            var safeToUnwind = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:SafeToUnwind |fg: |sg:()"));

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);

            samples.Should().NotBeNullOrEmpty("Not samples found");

            samples.Where(s => s.StackTrace.EndWith(toSkipStack)).Should().BeNullOrEmpty();
            samples.Where(s => s.StackTrace.EndWith(exceptionToSkipStack)).Should().BeNullOrEmpty();
            samples.Where(s => s.StackTrace.EndWith(contentionToSkipStack)).Should().BeNullOrEmpty();

            samples.Any(s => s.StackTrace.EndWith(safeToUnwind)).Should().BeTrue();
            samples.Any(s => s.StackTrace.EndWith(exceptionToKeepStack)).Should().BeTrue();
            samples.Any(s => s.StackTrace.EndWith(contentionToKeepStack)).Should().BeTrue();
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckIfEnvVarDoesNotExist(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 28", enableTracer: true);

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            var toSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:UnSafeToUnwind |fg: |sg:()"));

            var exceptionToSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionUnsafeUnwind |fg: |sg:()"));

            var exceptionToKeepStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:RaiseExceptionSafeUnwind |fg: |sg:()"));

            // Event based profiler
            var contentionToSkipStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:ContentionUnsafeToUnwind |fg: |sg:()"));

            var contentionToKeepStack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:ContentionSafeToUnwind |fg: |sg:()"));

            // Safe to unwind frame
            var safeToUnwind = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:UnsafeToUnwind |cg: |fn:SafeToUnwind |fg: |sg:()"));

            var samples = SamplesHelper.GetSamples(runner.Environment.PprofDir);

            samples.Where(s => s.StackTrace.EndWith(toSkipStack)).Should().NotBeNullOrEmpty("DD_PROFILER_SKIPPED_METHODS is not set and no CPU/Walltime samples contain the problematic function (UnSafeToUnwind).");
            samples.Where(s => s.StackTrace.EndWith(exceptionToSkipStack)).Should().NotBeNullOrEmpty("DD_PROFILER_SKIPPED_METHODS is not set and no Exception samples contain the problematic function (RaiseExceptionUnsafeUnwind).");
            samples.Where(s => s.StackTrace.EndWith(contentionToSkipStack)).Should().NotBeNullOrEmpty("DD_PROFILER_SKIPPED_METHODS is not set and no Contention samples contain the problematic function (ContentionSafeToUnwind).");

            samples.Any(s => s.StackTrace.EndWith(safeToUnwind)).Should().BeTrue();
            samples.Any(s => s.StackTrace.EndWith(exceptionToKeepStack)).Should().BeTrue();
            samples.Any(s => s.StackTrace.EndWith(contentionToKeepStack)).Should().BeTrue();
        }
    }
}
