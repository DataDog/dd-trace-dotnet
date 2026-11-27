// <copyright file="SigSegvChainTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
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
        // On .NET 6 the process must also not crash on the 1st one: the CLR handler needs SA_ONSTACK.
        // https://github.com/DataDog/dd-trace-dotnet/issues/9563
        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net8, TestFramework.Net10, TestFramework.Net11)]
        public void CheckThatNullReferenceExceptionsAreHandledAfterHeapSnapshot(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: Scenario);
            // the application never stops if a thread is stuck: do not wait for the default (10 minutes)
            runner.MaxTestRunDuration = TimeSpan.FromSeconds(120);
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            // throws if the process does not stop
            using var run = runner.Run();

            run.Logs.NativeProfiler.Should().ContainLine("Successfully setup signal handler for Segmentation fault");

            run.UploadCount.Should().BePositive();
        }
    }
}
