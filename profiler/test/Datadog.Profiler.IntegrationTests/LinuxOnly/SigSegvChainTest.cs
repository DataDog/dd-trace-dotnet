// <copyright file="SigSegvChainTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class SigSegvChainTest
    {
        // ForceSigSegvHandler: null dereferences in a loop, on the same threads
        private const string Scenario = "--scenario 23";
        private readonly ITestOutputHelper _output;

        public SigSegvChainTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // Once the heap snapshot has installed the profiler SIGSEGV handler, the 2nd NullReferenceException
        // of a thread must still reach the CLR handler: the thread must not loop forever on the faulting instruction.
        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0", "net11.0" })]
        public void CheckThatNullReferenceExceptionsAreHandledAfterHeapSnapshot(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario)
            {
                // the application never stops if a thread is stuck: do not wait for the default (10 minutes)
                MaxTestRunDuration = TimeSpan.FromSeconds(120)
            };
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            // throws if the process does not stop
            runner.Run(agent);

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            File.ReadLines(logFile)
                .Should().Contain(l => l.Contains("Successfully setup signal handler for Segmentation fault"));

            agent.NbCallsOnProfilingEndpoint.Should().BeGreaterThan(0);
        }
    }
}
