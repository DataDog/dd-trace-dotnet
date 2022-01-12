// <copyright file="ProcessBasicChecksTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Runner.Checks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.IntegrationTests.Checks
{
    [Collection(nameof(ConsoleTestsCollection))]
    public class ProcessBasicChecksTests : ConsoleTestHelper
    {
#if NET_FRAMEWORK
        private const string CorProfilerKey = "COR_PROFILER";
        private const string CorEnableKey = "COR_ENABLE_PROFILING";
#else
        private const string CorProfilerKey = "CORECLR_PROFILER";
        private const string CorEnableKey = "CORECLR_ENABLE_PROFILING";
#endif

        public ProcessBasicChecksTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task DetectRuntime()
        {
            using var helper = await StartConsole(enableProfiler: false);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            ProcessBasicCheck.Run(processInfo.Value);

#if NET_FRAMEWORK
            const string expectedOutput = "Target process is running with .NET Framework";
#else
            const string expectedOutput = "Target process is running with .NET Core";
#endif

            console.Output.Should().Contain(expectedOutput);
        }

        [Fact]
        public async Task NoEnvironmentVariables()
        {
            using var helper = await StartConsole(enableProfiler: false);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo.Value);

            result.Should().BeFalse();

            console.Output.Should().ContainAll(
                "Profiler is not loaded into the process",
                "Tracer is not loaded into the process",
                "The environment variable DD_DOTNET_TRACER_HOME is not set",
                $"The environment variable {CorProfilerKey} should be set to {Utils.PROFILERID} (current value: not set)",
                $"The environment variable {CorEnableKey} should be set to 1 (current value: not set)");
        }

        [Fact]
        public async Task WrongEnvironmentVariables()
        {
            using var helper = await StartConsole(
                enableProfiler: false,
                ("DD_DOTNET_TRACER_HOME", "TheDirectoryDoesNotExist"),
                (CorProfilerKey, Guid.Empty.ToString("B")),
                (CorEnableKey, "0"));
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo.Value);

            result.Should().BeFalse();

            console.Output.Should().ContainAll(
                "Profiler is not loaded into the process",
                "Tracer is not loaded into the process",
                $"DD_DOTNET_TRACER_HOME is set to TheDirectoryDoesNotExist but the directory does not exist",
                $"The environment variable {CorProfilerKey} should be set to {Utils.PROFILERID} (current value: {Guid.Empty:B})",
                $"The environment variable {CorEnableKey} should be set to 1 (current value: 0)");
        }

        [Fact]
        public async Task Working()
        {
            using var helper = await StartConsole(enableProfiler: true);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo.Value);

            using var scope = new AssertionScope();
            scope.AddReportable("Output", console.Output);

            result.Should().BeTrue();

            console.Output.Should().NotContainAny(
                "Profiler is not loaded into the process",
                "Tracer is not loaded into the process",
                $"DD_DOTNET_TRACER_HOME",
                $"{CorProfilerKey}",
                $"{CorEnableKey}");
        }
    }
}
