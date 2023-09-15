// <copyright file="ProcessBasicChecksTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests.Checks
{
    public class ProcessBasicChecksTests : ConsoleTestHelper
    {
#if NETFRAMEWORK
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

        private const string Profilerid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        private static readonly string ProfilerPath = EnvironmentHelper.GetNativeLoaderPath();

        public ProcessBasicChecksTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task DetectRuntime()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            using var helper = await StartConsole(enableProfiler: false);

            var output = await RunTool($"check process {helper.Process.Id}");

#if NETFRAMEWORK
            const string expectedOutput = NetFrameworkRuntime;
#else
            const string expectedOutput = NetCoreRuntime;
#endif

            output.Should().Contain(expectedOutput);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task VersionConflict1X()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            var environmentHelper = new EnvironmentHelper("VersionConflict.1x", typeof(TestHelper), Output);
            using var helper = await StartConsole(environmentHelper, enableProfiler: true);

            var output = await RunTool($"check process {helper.Process.Id}");

            output.Should().Contain(VersionConflict);
            output.Should().Contain(MultipleTracers(new[] { "1.29.0.0", TracerConstants.AssemblyVersion }).Replace(Environment.NewLine, " "));
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task NoEnvironmentVariables()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            using var helper = await StartConsole(enableProfiler: false);

            var output = await RunTool($"check process {helper.Process.Id}");

            output.Should().ContainAll(
                LoaderNotLoaded,
                NativeTracerNotLoaded,
                TracerNotLoaded,
                EnvironmentVariableNotSet("DD_DOTNET_TRACER_HOME"),
                WrongEnvironmentVariableFormat(CorProfilerKey, Profilerid, null),
                WrongEnvironmentVariableFormat(CorEnableKey, "1", null));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // The variable is not required on Windows because the path is set through the registry
                output.Should().NotContain(EnvironmentVariableNotSet(CorProfilerPathKey));
            }
            else
            {
                output.Should().Contain(EnvironmentVariableNotSet(CorProfilerPathKey));
            }
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task WrongEnvironmentVariables()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            using var helper = await StartConsole(
                enableProfiler: false,
                ("DD_PROFILING_ENABLED", "1"),
                ("DD_DOTNET_TRACER_HOME", "TheDirectoryDoesNotExist"),
                (CorProfilerKey, Guid.Empty.ToString("B")),
                (CorEnableKey, "0"),
                (CorProfilerPathKey, "dummyPath"),
                (CorProfilerPath32Key, "dummyPath"),
                (CorProfilerPath64Key, "dummyPath"));

            var output = await RunTool($"check process {helper.Process.Id}");

            output.Should().ContainAll(
                LoaderNotLoaded,
                NativeTracerNotLoaded,
                TracerNotLoaded,
                ContinuousProfilerEnabled,
                ContinuousProfilerNotLoaded,
                TracerHomeNotFoundFormat("TheDirectoryDoesNotExist"),
                WrongEnvironmentVariableFormat(CorProfilerKey, Profilerid, Guid.Empty.ToString("B")),
                WrongEnvironmentVariableFormat(CorEnableKey, "1", "0"),
                MissingProfilerEnvironment(CorProfilerPathKey, "dummyPath"),
                WrongProfilerEnvironment(CorProfilerPathKey, "dummyPath"),
                MissingProfilerEnvironment(CorProfilerPath32Key, "dummyPath"),
                WrongProfilerEnvironment(CorProfilerPath32Key, "dummyPath"),
                MissingProfilerEnvironment(CorProfilerPath64Key, "dummyPath"),
                WrongProfilerEnvironment(CorProfilerPath64Key, "dummyPath"));
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task Working()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            using var helper = await StartConsole(enableProfiler: true);

            var output = await RunTool($"check process {helper.Process.Id}");

            using var scope = new AssertionScope();
            scope.AddReportable("Output", output);

            output.Should().Contain(
                TracerVersion(TracerConstants.AssemblyVersion),
                ContinuousProfilerNotSet);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                output.Should().Contain(ProfilerVersion(TracerConstants.AssemblyVersion));
            }

            output.Should().NotContainAny(
                NativeTracerNotLoaded,
                TracerNotLoaded,
                "DD_DOTNET_TRACER_HOME",
                CorProfilerKey,
                CorEnableKey,
                CorProfilerPathKey,
                CorProfilerPath32Key,
                CorProfilerPath64Key);
        }

        [SkippableFact]
        public async Task WorkingWithContinuousProfiler()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

#if NETFRAMEWORK
            string archFolder = string.Empty;
#else
            string archFolder;

            if (FrameworkDescription.Instance.ProcessArchitecture == ProcessArchitecture.Arm64)
            {
                archFolder = "linux-arm64";
            }
            else
            {
                archFolder = IsAlpine() ? "linux-musl-x64" : "linux-x64";
            }
#endif

            var apiWrapperPath = Path.Combine(EnvironmentHelper.MonitoringHome, archFolder, "Datadog.Linux.ApiWrapper.x64.so");

            using var helper = await StartConsole(
                                   enableProfiler: true,
                                   ("DD_PROFILING_ENABLED", "1"),
                                   ("LD_PRELOAD", apiWrapperPath));

            var output = await RunTool($"check process {helper.Process.Id}");

            using var scope = new AssertionScope();
            scope.AddReportable("Output", output);

            output.Should().ContainAll(
                TracerVersion(TracerConstants.AssemblyVersion),
                ContinuousProfilerEnabled);

            output.Should().NotContainAny(
                NativeTracerNotLoaded,
                TracerNotLoaded,
                ContinuousProfilerNotSet,
                ContinuousProfilerNotLoaded,
                "LD_PRELOAD",
                "DD_DOTNET_TRACER_HOME",
                CorProfilerKey,
                CorEnableKey,
                CorProfilerPathKey,
                CorProfilerPath32Key,
                CorProfilerPath64Key);
        }

#if !NETFRAMEWORK
        [SkippableFact]
        public async Task WrongLdPreload()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            SkipOn.Platform(SkipOn.PlatformValue.Windows);
            using var helper = await StartConsole(
                                   enableProfiler: true,
                                   ("DD_PROFILING_ENABLED", "1"),
                                   ("LD_PRELOAD", "/dummyPath"));

            var output = await RunTool($"check process {helper.Process.Id}");

            using var scope = new AssertionScope();
            scope.AddReportable("Output", output);

            output.Should().NotContain(ApiWrapperNotFound("/dummyPath"));
            output.Should().Contain(Resources.WrongLdPreload("/dummyPath"));
        }

        [SkippableFact]
        public async Task LdPreloadNotFound()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            SkipOn.Platform(SkipOn.PlatformValue.Windows);
            using var helper = await StartConsole(
                                   enableProfiler: true,
                                   ("DD_PROFILING_ENABLED", "1"),
                                   ("LD_PRELOAD", "/dummyPath/Datadog.Linux.ApiWrapper.x64.so"));

            var output = await RunTool($"check process {helper.Process.Id}");

            using var scope = new AssertionScope();
            scope.AddReportable("Output", output);

            output.Should().Contain(ApiWrapperNotFound("/dummyPath/Datadog.Linux.ApiWrapper.x64.so"));
            output.Should().NotContain(Resources.WrongLdPreload("/dummyPath/Datadog.Linux.ApiWrapper.x64.so"));
        }
#endif

        [SkippableTheory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task DetectContinousProfilerState(bool? enabled)
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            var environmentVariables = enabled == null ? Array.Empty<(string, string)>()
                : new[] { ("DD_PROFILING_ENABLED", enabled == true ? "1" : "0") };

            using var helper = await StartConsole(enableProfiler: true, environmentVariables);

            var output = await RunTool($"check process {helper.Process.Id}");

            using var scope = new AssertionScope();
            scope.AddReportable("Output", output);

            if (enabled == null)
            {
                output.Should().Contain(ContinuousProfilerNotSet);
            }
            else if (enabled == true)
            {
                output.Should().Contain(ContinuousProfilerEnabled);
            }
            else
            {
                output.Should().Contain(ContinuousProfilerDisabled);
            }
        }

        private static bool IsAlpine()
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    var strArray = File.ReadAllLines("/etc/os-release");
                    foreach (var str in strArray)
                    {
                        if (str.StartsWith("ID=", StringComparison.Ordinal))
                        {
                            return str.Substring(3).Trim('"', '\'') == "alpine";
                        }
                    }
                }
            }
            catch
            {
                // ignore error checking if the file doesn't exist or we can't read it
            }

            return false;
        }
    }
}
