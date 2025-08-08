// <copyright file="ConfigurationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Configuration
{
    public class ConfigurationTest
    {
        private const string Scenario1 = "--scenario 18";

        private readonly ITestOutputHelper _output;

        public ConfigurationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // NOTE: we don't need to validate ALL runtimes but just one
        //

        [TestAppFact("Samples.Computer01", new[] { "net9.0" })]
        public void CheckEnvVarsInLogWithDefaultProfilers(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            bool cpuIsLogged = false;
            bool walltimeIsLogged = false;
            bool exceptionIsLogged = false;
            bool allocationIsLogged = false;
            bool lockIsLogged = false;
            bool gcIsLogged = false;
            bool heapIsLogged = false;
            bool serviceIsLogged = false;
            bool etwIsLogged = false;
            bool gcCpuIsLogged = false;
            bool threadLifetimeIsLogged = false;
            bool stableConfigIsLogged = false;

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                                   .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            foreach (var line in File.ReadLines(logFile))
            {
                if (line.Contains("DD_PROFILING_CPU_ENABLED"))
                {
                    cpuIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_WALLTIME_ENABLED"))
                {
                    walltimeIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_EXCEPTION_ENABLED"))
                {
                    exceptionIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_ALLOCATION_ENABLED"))
                {
                    allocationIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_CONTENTION_ENABLED"))
                {
                    lockIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_GC_ENABLED"))
                {
                    gcIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_HEAP_ENABLED"))
                {
                    heapIsLogged = true;
                }
                else
                if (line.Contains("DD_SERVICE"))
                {
                    serviceIsLogged = true;
                }
                else
                if (line.Contains("DD_INTERNAL_PROFILING_ETW_ENABLED"))
                {
                    etwIsLogged = true;
                }
                else if (line.Contains("DD_GC_THREADS_CPUTIME_ENABLED"))
                {
                    gcCpuIsLogged = true;
                }
                else if (line.Contains("DD_THREAD_LIFETIME_ENABLED"))
                {
                    threadLifetimeIsLogged = true;
                }
                else if (line.Contains("DD_PROFILING_MANAGED_ACTIVATION_ENABLED"))
                {
                    stableConfigIsLogged = true;
                }
                else if (line.Contains("DD_INJECTION_ENABLED"))
                {
                    // this could happen on SSI deployments such as developer's machine
                }
                else if (line.Contains("] Configuration: DD_"))
                {
                    // This is the default value
                    Assert.Fail($"unexpected configuration log - {line}");
                }
            }

            cpuIsLogged.Should().BeTrue();
            walltimeIsLogged.Should().BeTrue();
            exceptionIsLogged.Should().BeTrue();
            allocationIsLogged.Should().BeTrue();
            lockIsLogged.Should().BeTrue();
            gcIsLogged.Should().BeTrue();
            heapIsLogged.Should().BeTrue();
            serviceIsLogged.Should().BeTrue();
            etwIsLogged.Should().BeTrue();
            gcCpuIsLogged.Should().BeTrue();
            threadLifetimeIsLogged.Should().BeTrue();
            stableConfigIsLogged.Should().BeTrue();
        }

        [TestAppFact("Samples.Computer01", new[] { "net9.0" })]
        public void CheckEnvVarsInLogWithDisabledProfilers(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario1);
            EnvironmentHelper.DisableDefaultProfilers(runner);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            bool cpuIsLogged = false;
            bool walltimeIsLogged = false;
            bool exceptionIsLogged = false;
            bool allocationIsLogged = false;
            bool lockIsLogged = false;
            bool gcIsLogged = false;
            bool heapIsLogged = false;
            bool serviceIsLogged = false;
            bool etwIsLogged = false;
            bool gcCpuIsLogged = false;
            bool threadLifetimeIsLogged = false;
            bool stableConfigIsLogged = false;

            var logFile = Directory.GetFiles(runner.Environment.LogDir)
                                   .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));

            foreach (var line in File.ReadLines(logFile))
            {
                if (line.Contains("DD_PROFILING_CPU_ENABLED"))
                {
                    cpuIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_WALLTIME_ENABLED"))
                {
                    walltimeIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_EXCEPTION_ENABLED"))
                {
                    exceptionIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_ALLOCATION_ENABLED"))
                {
                    allocationIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_LOCK_ENABLED"))
                {
                    lockIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_GC_ENABLED"))
                {
                    gcIsLogged = true;
                }
                else
                if (line.Contains("DD_PROFILING_HEAP_ENABLED"))
                {
                    heapIsLogged = true;
                }
                else
                if (line.Contains("DD_SERVICE"))
                {
                    serviceIsLogged = true;
                }
                else
                if (line.Contains("DD_INTERNAL_PROFILING_ETW_ENABLED"))
                {
                    etwIsLogged = true;
                }
                else if (line.Contains("DD_GC_THREADS_CPUTIME_ENABLED"))
                {
                    gcCpuIsLogged = true;
                }
                else if (line.Contains("DD_THREAD_LIFETIME_ENABLED"))
                {
                    threadLifetimeIsLogged = true;
                }
                else if (line.Contains("DD_PROFILING_MANAGED_ACTIVATION_ENABLED"))
                {
                    stableConfigIsLogged = true;
                }
                else if (line.Contains("DD_INJECTION_ENABLED"))
                {
                    // this could happen on SSI deployments such as developer's machine
                }
                else if (line.Contains("] Configuration: DD_"))
                {
                    // This is the default value
                    Assert.Fail($"unexpected configuration log - {line}");
                }
            }

            cpuIsLogged.Should().BeTrue();
            walltimeIsLogged.Should().BeTrue();
            exceptionIsLogged.Should().BeTrue();
            allocationIsLogged.Should().BeTrue();
            lockIsLogged.Should().BeTrue();
            gcIsLogged.Should().BeTrue();
            heapIsLogged.Should().BeTrue();
            serviceIsLogged.Should().BeTrue();
            etwIsLogged.Should().BeTrue();
            gcCpuIsLogged.Should().BeTrue();
            threadLifetimeIsLogged.Should().BeTrue();
            stableConfigIsLogged.Should().BeTrue();
        }
    }
}
