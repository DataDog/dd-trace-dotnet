// <copyright file="ProcessBasicChecksTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Checks;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

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

        [SkippableFact]
        public async Task DetectRuntime()
        {
            using var helper = await StartConsole(enableProfiler: false);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            ProcessBasicCheck.Run(processInfo);

#if NET_FRAMEWORK
            const string expectedOutput = NetFrameworkRuntime;
#else
            const string expectedOutput = NetCoreRuntime;
#endif

            console.Output.Should().Contain(expectedOutput);
        }

        [SkippableFact]
        public async Task VersionConflict1X()
        {
            var environmentHelper = new EnvironmentHelper("VersionConflict.1x", typeof(TestHelper), Output);
            using var helper = await StartConsole(environmentHelper, enableProfiler: true);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo);

            result.Should().BeFalse();

            console.Output.Should().Contain(VersionConflict);

            console.Output.Should().Contain(MultipleTracers(new[] { "1.29.0.0", TracerConstants.AssemblyVersion }));
        }

        [SkippableFact]
        public async Task NoEnvironmentVariables()
        {
            using var helper = await StartConsole(enableProfiler: false);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo);

            result.Should().BeFalse();

            console.Output.Should().ContainAll(
                ProfilerNotLoaded,
                TracerNotLoaded,
                TracerHomeNotSet,
                WrongEnvironmentVariableFormat(CorProfilerKey, Utils.Profilerid, null),
                WrongEnvironmentVariableFormat(CorEnableKey, "1", null));
        }

        [SkippableFact]
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

            var result = ProcessBasicCheck.Run(processInfo);

            result.Should().BeFalse();

            console.Output.Should().ContainAll(
                ProfilerNotLoaded,
                TracerNotLoaded,
                TracerHomeNotFoundFormat("TheDirectoryDoesNotExist"),
                WrongEnvironmentVariableFormat(CorProfilerKey, Utils.Profilerid, Guid.Empty.ToString("B")),
                WrongEnvironmentVariableFormat(CorEnableKey, "1", "0"));
        }

        [SkippableFact]
        public async Task Working()
        {
            using var helper = await StartConsole(enableProfiler: true);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo);

            using var scope = new AssertionScope();
            scope.AddReportable("Output", console.Output);

            result.Should().BeTrue();

            console.Output.Should().NotContainAny(
                ProfilerNotLoaded,
                TracerNotLoaded,
                "DD_DOTNET_TRACER_HOME",
                CorProfilerKey,
                CorEnableKey);
        }

        [SkippableFact]
        public void NoRegistry()
        {
            var registryService = new Mock<IRegistryService>();
            registryService.Setup(r => r.GetLocalMachineValueNames(It.IsAny<string>())).Returns(Array.Empty<string>());

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService.Object);

            result.Should().BeTrue();

            console.Output.Should().NotContainAny(ErrorCheckingRegistry(string.Empty), "is defined and could prevent the tracer from working properly");
        }

        [SkippableFact]
        public void BadRegistryKey()
        {
            var registryService = new Mock<IRegistryService>();
            registryService.Setup(r => r.GetLocalMachineValueNames(It.IsAny<string>())).Returns(new[] { "cor_profiler" });

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService.Object);

            result.Should().BeFalse();

            console.Output.Should().Contain(SuspiciousRegistryKey("cor_profiler"));
        }
    }
}
