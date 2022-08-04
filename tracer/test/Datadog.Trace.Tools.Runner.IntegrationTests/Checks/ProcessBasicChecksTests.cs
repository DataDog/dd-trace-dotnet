// <copyright file="ProcessBasicChecksTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
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
        internal const string ClsidKey = @"SOFTWARE\Classes\CLSID\{846F5F1C-F9AE-4B07-969E-05C26BC060D8}\InprocServer32";
        internal const string Clsid32Key = @"SOFTWARE\Classes\Wow6432Node\CLSID\{846F5F1C-F9AE-4B07-969E-05C26BC060D8}\InprocServer32";
#if NET_FRAMEWORK
        private const string CorProfilerKey = "COR_PROFILER";
        private const string CorProfilerPathKey = "COR_PROFILER_PATH";
        private const string CorProfilerPath32Key = "COR_PROFILER_PATH_32";
        private const string CorProfilerPath64Key = "COR_PROFILER_PATH_64";
        private const string CorEnableKey = "COR_ENABLE_PROFILING";
#else
        private const string CorProfilerKey = "CORECLR_PROFILER";
        private const string CorProfilerPathKey = "CORECLR_PROFILER_PATH";
        private const string CorProfilerPath32Key = "CORECLR_PROFILER_PATH_32";
        private const string CorProfilerPath64Key = "CORECLR_PROFILER_PATH_64";
        private const string CorEnableKey = "CORECLR_ENABLE_PROFILING";
#endif

        private static readonly string ProfilerPath = EnvironmentHelper.GetNativeLoaderPath();

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

            ProcessBasicCheck.Run(processInfo, MockRegistryService(Array.Empty<string>(), ProfilerPath));

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

            var result = ProcessBasicCheck.Run(processInfo, MockRegistryService(Array.Empty<string>(), ProfilerPath));

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

            var result = ProcessBasicCheck.Run(processInfo, MockRegistryService(Array.Empty<string>(), ProfilerPath));

            result.Should().BeFalse();

            console.Output.Should().ContainAll(
                ProfilerNotLoaded,
                TracerNotLoaded,
                EnvironmentVariableNotSet("DD_DOTNET_TRACER_HOME"),
                WrongEnvironmentVariableFormat(CorProfilerKey, Utils.Profilerid, null),
                WrongEnvironmentVariableFormat(CorEnableKey, "1", null));

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // The variable is not required on Windows because the path is set through the registry
                console.Output.Should().NotContain(EnvironmentVariableNotSet(CorProfilerPathKey));
            }
            else
            {
                console.Output.Should().Contain(EnvironmentVariableNotSet(CorProfilerPathKey));
            }
        }

        [SkippableFact]
        public async Task WrongEnvironmentVariables()
        {
            using var helper = await StartConsole(
                enableProfiler: false,
                ("DD_DOTNET_TRACER_HOME", "TheDirectoryDoesNotExist"),
                (CorProfilerKey, Guid.Empty.ToString("B")),
                (CorEnableKey, "0"),
                (CorProfilerPathKey, "dummyPath"),
                (CorProfilerPath32Key, "dummyPath"),
                (CorProfilerPath64Key, "dummyPath"));
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo, MockRegistryService(Array.Empty<string>(), ProfilerPath));

            result.Should().BeFalse();

            console.Output.Should().ContainAll(
                ProfilerNotLoaded,
                TracerNotLoaded,
                TracerHomeNotFoundFormat("TheDirectoryDoesNotExist"),
                WrongEnvironmentVariableFormat(CorProfilerKey, Utils.Profilerid, Guid.Empty.ToString("B")),
                WrongEnvironmentVariableFormat(CorEnableKey, "1", "0"),
                MissingProfilerEnvironment(CorProfilerPathKey, "dummyPath"),
                WrongProfilerEnvironment(CorProfilerPathKey, "dummyPath"),
                MissingProfilerEnvironment(CorProfilerPath32Key, "dummyPath"),
                WrongProfilerEnvironment(CorProfilerPath32Key, "dummyPath"),
                MissingProfilerEnvironment(CorProfilerPath64Key, "dummyPath"),
                WrongProfilerEnvironment(CorProfilerPath64Key, "dummyPath"));
        }

        [SkippableFact]
        public async Task Working()
        {
            using var helper = await StartConsole(enableProfiler: true);
            var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

            processInfo.Should().NotBeNull();

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.Run(processInfo, MockRegistryService(Array.Empty<string>(), ProfilerPath));

            using var scope = new AssertionScope();
            scope.AddReportable("Output", console.Output);

            result.Should().BeTrue();

            console.Output.Should().Contain(TracerVersion(TracerConstants.AssemblyVersion));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                console.Output.Should().Contain(ProfilerVersion(TracerConstants.AssemblyVersion));
            }

            console.Output.Should().NotContainAny(
                ProfilerNotLoaded,
                TracerNotLoaded,
                "DD_DOTNET_TRACER_HOME",
                CorProfilerKey,
                CorEnableKey,
                CorProfilerPathKey,
                CorProfilerPath32Key,
                CorProfilerPath64Key);
        }

        [SkippableFact]
        public void GoodRegistry()
        {
            var registryService = MockRegistryService(Array.Empty<string>(), ProfilerPath);

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService);

            result.Should().BeTrue();

            console.Output.Should().NotContainAny(ErrorCheckingRegistry(string.Empty), "is defined and could prevent the tracer from working properly");
            console.Output.Should().NotContain(MissingRegistryKey(ClsidKey));
            console.Output.Should().NotContain(MissingProfilerRegistry(ClsidKey, ProfilerPath));
        }

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void BadRegistryKey(bool wow64)
        {
            var registryService = MockRegistryService(new[] { "cor_profiler" }, ProfilerPath, wow64);

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService);

            result.Should().BeFalse();

            var netFrameworkKey = wow64 ? @"SOFTWARE\WOW6432Node\Microsoft\.NETFramework" : @"SOFTWARE\Microsoft\.NETFramework";

            console.Output.Should().Contain(SuspiciousRegistryKey(netFrameworkKey, "cor_profiler"));
        }

        [SkippableFact]
        public void ProfilerNotRegistered()
        {
            var registryService = MockRegistryService(Array.Empty<string>(), null);

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService);

            result.Should().BeFalse();

            console.Output.Should().Contain(MissingRegistryKey(ClsidKey));
        }

        [SkippableFact]
        public void ProfilerNotFoundRegistry()
        {
            var registryService = MockRegistryService(Array.Empty<string>(), "dummyPath/" + Path.GetFileName(ProfilerPath));

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService);

            result.Should().BeFalse();

            console.Output.Should().NotContain(MissingRegistryKey(ClsidKey));
            console.Output.Should().Contain(MissingProfilerRegistry(ClsidKey, "dummyPath/" + Path.GetFileName(ProfilerPath)));
        }

        [SkippableFact]
        public void WrongProfilerRegistry()
        {
            var registryService = MockRegistryService(Array.Empty<string>(), "wrongProfiler.dll");

            using var console = ConsoleHelper.Redirect();

            var result = ProcessBasicCheck.CheckRegistry(registryService);

            result.Should().BeFalse();

            console.Output.Should().NotContain(MissingRegistryKey(ClsidKey));
            console.Output.Should().Contain(Resources.WrongProfilerRegistry(ClsidKey, "wrongProfiler.dll"));
        }

        private static IRegistryService MockRegistryService(string[] frameworkKeyValues, string profilerKeyValue, bool wow64 = false)
        {
            var registryService = new Mock<IRegistryService>();

            var netFrameworkKey = wow64 ? @"SOFTWARE\WOW6432Node\Microsoft\.NETFramework" : @"SOFTWARE\Microsoft\.NETFramework";

            registryService.Setup(r => r.GetLocalMachineValueNames(It.Is(netFrameworkKey, StringComparer.Ordinal)))
                .Returns(frameworkKeyValues);
            registryService.Setup(r => r.GetLocalMachineValue(It.Is<string>(s => s == ClsidKey || s == Clsid32Key)))
                .Returns(profilerKeyValue);

            return registryService.Object;
        }
    }
}
