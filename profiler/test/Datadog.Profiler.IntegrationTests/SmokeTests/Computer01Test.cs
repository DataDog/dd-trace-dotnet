// <copyright file="Computer01Test.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public class Computer01Test
    {
        private readonly ITestOutputHelper _output;

        public Computer01Test(ITestOutputHelper output)
        {
            _output = output;
        }

        // NOTE: now that .NET Framework is supported by default, the profiler tries to connect
        //       to connect to the Agent using namedpipe. Since the Agent does not exist in CI,
        //       the ETW support is disabled in the tests for .NET Framework.

        // scenarios implemented in Computer01:
        // -----------------------------------------------------------------------------------------
        //  1: start threads with specific callstacks in another appdomain
        //  2: start threads with generic type and method having long parameters list in callstack
        //  3: start threads that sleep/task.delay for 10s, 20s, 30s, 40s every minute
        //  4: start a thread to compute pi at a certain precision(high CPU usage)
        //  5: start a to compute fibonacci (high CPU usage + deep stacks)
        // -----------------------------------------------------------------------------------------
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckAppDomain(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 1", output: _output);
            if (app.IsNetFramework)
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");

            runner.RunAndCheck();
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckGenerics(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 2", output: _output);
            if (app.IsNetFramework)
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");

            runner.RunAndCheck();
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckPi(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 4", output: _output);
            if (app.IsNetFramework)
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");

            runner.RunAndCheck();
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckFibonacci(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 5", output: _output);
            if (app.IsNetFramework)
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");

            runner.RunAndCheck();
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckAppDomainForNewCpuProfiler(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 1", output: _output);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            runner.RunAndCheck();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(runner.EnvironmentHelper.LogDir);
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckGenericsForNewCpuProfiler(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 2", output: _output);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            runner.RunAndCheck();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(runner.EnvironmentHelper.LogDir);
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckPiForNewCpuProfiler(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 4", output: _output);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            runner.RunAndCheck();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(runner.EnvironmentHelper.LogDir);
        }

        [Trait("Category", "LinuxOnly")]
        [TestAppFact(TestApplication.Computer01)]
        public void CheckFibonacciForNewCpuProfiler(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 5", output: _output);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            runner.RunAndCheck();

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(runner.EnvironmentHelper.LogDir);
        }

        [Flaky("Intermittently finds an error message in the profiler log file in CI")]
        [TestAppFact(TestApplication.Computer01, TestFramework.Net48, MinimumFramework = TestFramework.NetCoreApp31)]
        public void CheckCustomGetFunctionFromIP(TestAppContext app)
        {
            var runner = new SmokeTestRunner(app, commandLine: "--scenario 0", output: _output);
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.UseManagedCodeCache, "1");
            runner.EnvironmentHelper.SetVariable(EnvironmentVariables.MemoryFootprintEnabled, "1");
            if (app.IsNetFramework)
            {
                runner.EnvironmentHelper.SetVariable(EnvironmentVariables.EtwEnabled, "0");
            }

            // First, check for no error
            runner.RunAndCheck();

            // Then check for custom get function from IP enabled
            ProfilerLogSet.Read(runner.EnvironmentHelper.LogDir).NativeProfiler
                .Should().ContainLine("ManagedCodeCache initialized successfully");
        }
    }
}
