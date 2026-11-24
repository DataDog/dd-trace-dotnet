// <copyright file="WrapperLibraryTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.LinuxOnly
{
    [Trait("Category", "LinuxOnly")]
    public class WrapperLibraryTest
    {
        private const string ExpectedErrorMessage = "It is not safe to start the profiler. See previous log messages for more info.";
        private readonly ITestOutputHelper _output;

        public WrapperLibraryTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // The profiler does not start in the first three tests, so there is no profile directory:
        // they keep a custom agent and read only the logs.
        [TestAppFact(TestApplication.BuggyBits)]
        public void EnsureProfilerIsDeactivatedIfNoWrapperLibrary(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1");

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable("LD_PRELOAD", string.Empty);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            ProfilerLogSet.Read(runner.Environment.LogDir).NativeProfiler.Should().ContainLine(ExpectedErrorMessage);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void EnsureProfilerIsDeactivatedIfWrongPathToWrapperLibrary(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1");

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable("LD_PRELOAD", "/mnt/does_not_exist/Datadog.Linux.Wrapper.x64.so");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            ProfilerLogSet.Read(runner.Environment.LogDir).NativeProfiler.Should().ContainLine(ExpectedErrorMessage);
        }

        [TestAppFact(TestApplication.BuggyBits)]
        public void EnsureAppDoesNotCrashIfProfilerDeactivateAndTracerActivated(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 1", enableTracer: true);

            // Overwrite the one set in EnvironmentHelper
            runner.Environment.SetVariable("LD_PRELOAD", string.Empty);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            ProfilerLogSet.Read(runner.Environment.LogDir).NativeProfiler.Should().ContainLine(ExpectedErrorMessage);
        }

        [TestAppFact(TestApplication.ExceptionGenerator)]
        public void GenerateDumpIfDbgRequested(TestAppContext app)
        {
            // The continuous profiler is disabled for this test because it's not needed and it *might* be causing some failures
            var runner = app.CreateRunner(_output, commandLine: "--scenario 7", enableTracer: true, enableProfiler: false);

            runner.Environment.SetVariable("COMPlus_DbgEnableMiniDump", "1");
            runner.Environment.SetVariable("COMPlus_DbgMiniDumpName", "/dev/null");
            runner.Environment.SetVariable("COMPlus_DbgMiniDumpType", string.Empty);
            runner.Environment.SetVariable("COMPlus_CreateDumpDiagnostics", "1");

            using var processHelper = runner.LaunchProcess();

            var success = processHelper.Process.WaitForExit(30_000);
            if (!success)
            {
                var logger = runner.XUnitLogger;

                // Note: we don't drain because the process hasn't exited, but it means the output may be incomplete
                logger.WriteLine("Standard output:");
                logger.WriteLine(processHelper.StandardOutput);

                logger.WriteLine("Error output:");
                logger.WriteLine(processHelper.ErrorOutput);

                var pid = processHelper.Process.Id;

                var status = File.ReadAllText($"/proc/{pid}/status");

                logger.WriteLine("Process status:");
                logger.WriteLine(status);

                logger.WriteLine("************************");

                // Enumerating status for each thread
                var threads = Directory.GetDirectories($"/proc/{pid}/task");

                foreach (var thread in threads)
                {
                    logger.WriteLine($"**** {thread}:");
                    try
                    {
                        var threadStatus = File.ReadAllText($"{thread}/status");
                        logger.WriteLine(threadStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.WriteLine(ex.Message);
                    }
                }

                logger.WriteLine("************************");

                var processes = Process.GetProcesses();

                logger.WriteLine("Processes:");

                foreach (var process in processes)
                {
                    logger.WriteLine($"Process: {process.ProcessName} ({process.Id})");
                }
            }

            success.Should().BeTrue();
            processHelper.Drain();
            processHelper.ErrorOutput.Should().Contain("Unhandled exception. System.InvalidOperationException: Task failed successfully");
            processHelper.StandardOutput.Should().NotMatchRegex(@"createdump [\w\.\/]+createdump \d+")
                .And.Contain("Writing minidump");
        }
    }
}
