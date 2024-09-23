// <copyright file="ProcessBasicChecksTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.Shared;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet.IntegrationTests.Checks;

#pragma warning disable CS0436 // Type conflicts with imported type

// Some of these tests use SSI variables, so we have to explicitly reset them
[EnvironmentRestorer("DD_INJECTION_ENABLED")]
[Collection(nameof(ConsoleTestsCollection))]
public class ProcessBasicChecksTests : ConsoleTestHelper
{
    internal const string ClsidKey = @"SOFTWARE\Classes\CLSID\{846F5F1C-F9AE-4B07-969E-05C26BC060D8}\InprocServer32";
    internal const string Clsid32Key = @"SOFTWARE\Classes\Wow6432Node\CLSID\{846F5F1C-F9AE-4B07-969E-05C26BC060D8}\InprocServer32";
    private const string CorProfilerKey = "CORECLR_PROFILER";
    private const string CorProfilerPathKey = "CORECLR_PROFILER_PATH";
    private const string CorProfilerPath32Key = "CORECLR_PROFILER_PATH_32";
    private const string CorProfilerPath64Key = "CORECLR_PROFILER_PATH_64";
    private const string CorEnableKey = "CORECLR_ENABLE_PROFILING";
    private const string LogDirectoryKey = "DD_TRACE_LOG_DIRECTORY";

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
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        const string expectedOutput = NetCoreRuntime;

        console.Output.Should().Contain(expectedOutput);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void NoRuntime()
    {
        using var console = ConsoleHelper.Redirect();

        var processInfo = new ProcessInfo("target", 100, new Dictionary<string, string>(), "mainModule", []);

        ProcessBasicCheck.Run(processInfo, MockRegistryService([], ProfilerPath));

        var expectedOutput = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? RuntimeDetectionFailedWindows : RuntimeDetectionFailedLinux;
        console.Output.Should().Contain(expectedOutput);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task VersionConflict1X()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        var environmentHelper = new EnvironmentHelper("VersionConflict.1x", typeof(TestHelper), Output);
        using var helper = await StartConsole(environmentHelper, enableProfiler: true);
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        result.Should().BeFalse();

        console.Output.Should().Contain(VersionConflict);

        console.Output.Should().Contain(MultipleTracers(new[] { "1.29.0.0", TracerConstants.AssemblyVersion }));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NoEnvironmentVariables()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        using var helper = await StartConsole(enableProfiler: false);
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        result.Should().BeFalse();

        console.Output.Should().ContainAll(
            LoaderNotLoaded,
            NativeTracerNotLoaded,
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
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        result.Should().BeFalse();

        console.Output.Should().ContainAll(
            LoaderNotLoaded,
            NativeTracerNotLoaded,
            TracerNotLoaded,
            TracerHomeNotFoundFormat("TheDirectoryDoesNotExist"),
            WrongEnvironmentVariableFormat(CorProfilerKey, Utils.Profilerid, Guid.Empty.ToString("B")),
            WrongEnvironmentVariableFormat(CorEnableKey, "1", "0"),
            MissingProfilerEnvironment(CorProfilerPathKey, "dummyPath"),
            WrongProfilerEnvironment(CorProfilerPathKey, "dummyPath"),
            MissingProfilerEnvironment(CorProfilerPath32Key, "dummyPath"),
            WrongProfilerEnvironment(CorProfilerPath32Key, "dummyPath"),
            MissingProfilerEnvironment(CorProfilerPath64Key, "dummyPath"),
            WrongProfilerEnvironment(CorProfilerPath64Key, "dummyPath"),
            WrongProfilerEnvironment(CorProfilerPath64Key, "dummyPath"));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#if NETFRAMEWORK
            console.Output.Should().Contain(TracingWithInstallerWindowsNetFramework);
#else
            console.Output.Should().Contain(TracingWithInstallerWindowsNetCore);
#endif
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            console.Output.Should().Contain(TracingWithInstallerLinux);
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task Working()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        using var helper = await StartConsole(enableProfiler: true);
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        using var scope = new AssertionScope();
        scope.AddReportable("Output", console.Output);

        result.Should().BeTrue();

        console.Output.Should().Contain(
            TracerVersion(TracerConstants.AssemblyVersion),
            ContinuousProfilerNotSet);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            console.Output.Should().Contain(ProfilerVersion(TracerConstants.AssemblyVersion));
        }

        console.Output.Should().NotContainAny(
            NativeTracerNotLoaded,
            TracerNotLoaded,
            TracerHomeNotFoundFormat("DD_DOTNET_TRACER_HOME"));

        console.Output.Should().Contain(
            CorrectlySetupEnvironment(CorProfilerKey, Utils.Profilerid),
            CorrectlySetupEnvironment(CorEnableKey, "1"),
            CorrectlySetupEnvironment(CorProfilerPathKey, ProfilerPath));
    }

    [SkippableFact]
    public async Task WorkingWithContinuousProfiler()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        string archFolder;

        if (FrameworkDescription.Instance.ProcessArchitecture == ProcessArchitecture.Arm64)
        {
            archFolder = Utils.IsAlpine() ? "linux-musl-arm64" : "linux-arm64";
        }
        else
        {
            archFolder = Utils.IsAlpine() ? "linux-musl-x64" : "linux-x64";
        }

        var apiWrapperPath = Path.Combine(EnvironmentHelper.MonitoringHome, archFolder, "Datadog.Linux.ApiWrapper.x64.so");

        using var helper = await StartConsole(
                               enableProfiler: true,
                               ("DD_PROFILING_ENABLED", "1"),
                               (LogDirectoryKey, this.LogDirectory),
                               ("LD_PRELOAD", apiWrapperPath));
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        using var scope = new AssertionScope();
        scope.AddReportable("Output", console.Output);

        result.Should().BeTrue();

        console.Output.Should().ContainAll(
            TracerVersion(TracerConstants.AssemblyVersion),
            ContinuousProfilerEnabled,
            CorrectlySetupEnvironment(CorProfilerKey, Utils.Profilerid),
            CorrectlySetupEnvironment(CorEnableKey, "1"),
            CorrectlySetupEnvironment(CorProfilerPathKey, ProfilerPath));

        console.Output.Should().NotContainAny(
            NativeTracerNotLoaded,
            TracerNotLoaded,
            ContinuousProfilerNotSet,
            ContinuousProfilerNotLoaded,
            "LD_PRELOAD",
            TracerHomeNotFoundFormat("DD_DOTNET_TRACER_HOME"));
    }

    [SkippableFact]
    public async Task WrongLdPreload()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.Platform(SkipOn.PlatformValue.Windows);

        using var helper = await StartConsole(
                               enableProfiler: true,
                               ("DD_PROFILING_ENABLED", "1"),
                               ("LD_PRELOAD", "/dummyPath"));

        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        using var scope = new AssertionScope();
        scope.AddReportable("Output", console.Output);

        result.Should().BeFalse();

        console.Output.Should().NotContain(ApiWrapperNotFound("/dummyPath"));
        console.Output.Should().Contain(Resources.WrongLdPreload("/dummyPath"));
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

        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        using var scope = new AssertionScope();
        scope.AddReportable("Output", console.Output);

        result.Should().BeFalse();

        console.Output.Should().Contain(ApiWrapperNotFound("/dummyPath/Datadog.Linux.ApiWrapper.x64.so"));
        console.Output.Should().NotContain(Resources.WrongLdPreload("/dummyPath/Datadog.Linux.ApiWrapper.x64.so"));
    }

    [SkippableTheory]
    [InlineData("auto", null)]
    [InlineData("1", null)]
    [InlineData("0", null)]
    [InlineData(null, null)]
    [InlineData("auto", false)]
    [InlineData("1", false)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    [InlineData("auto", true)]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData(null, true)]
    public async Task DetectContinuousProfilerState(string enabled, bool? ssiInjectionEnabled)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        var ssiInjection = ssiInjectionEnabled is null
                               ? null
                               : ssiInjectionEnabled == true ? "tracer,profiler" : "tracer";

        var expected = (enabled, ssiInjectionEnabled) switch
        {
            ("0", _) => ContinuousProfilerDisabled,
            ("1", _) => ContinuousProfilerEnabled,
            ("auto", _) => ContinuousProfilerEnabledWithHeuristics,
            (null, null) => ContinuousProfilerNotSet,
            (null, true) => ContinuousProfilerSsiEnabledWithHeuristics,
            (null, false) => ContinuousProfilerSsiMonitoring,
            _ => throw new InvalidOperationException("Unexpected test combination"),
        };

        (string, string)[] envVars = (enabled, ssiInjection) switch
        {
            (null, null) => [],
            ({ } prof, null) => [("DD_PROFILING_ENABLED", prof)],
            (null, { } ssi) => [("DD_INJECTION_ENABLED", ssi)],
            ({ } prof, { } ssi) => [("DD_PROFILING_ENABLED", prof), ("DD_INJECTION_ENABLED", ssi)],
        };

        using var helper = await StartConsole(enableProfiler: true, envVars);

        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        using var scope = new AssertionScope();
        scope.AddReportable("Output", console.Output);

        console.Output.Should().Contain(expected);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void GoodRegistry()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        var registryService = MockRegistryService(Array.Empty<string>(), ProfilerPath);

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.CheckRegistry("2.14", registryService);

        result.Should().BeTrue();

        console.Output.Should().NotContainAny(ErrorCheckingRegistry(string.Empty), "is defined and could prevent the tracer from working properly");
        console.Output.Should().NotContain(MissingRegistryKey(ClsidKey));
        console.Output.Should().NotContain(MissingProfilerRegistry(ClsidKey, ProfilerPath));
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData(true)]
    [InlineData(false)]
    public void BadRegistryKey(bool wow64)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        var registryService = MockRegistryService(new[] { "cor_profiler" }, ProfilerPath, wow64);

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.CheckRegistry("2.14", registryService);

        result.Should().BeFalse();

        var netFrameworkKey = wow64 ? @"SOFTWARE\WOW6432Node\Microsoft\.NETFramework" : @"SOFTWARE\Microsoft\.NETFramework";

        console.Output.Should().Contain(SuspiciousRegistryKey(netFrameworkKey, "cor_profiler"));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void ProfilerNotRegistered()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
        var registryService = MockRegistryService(Array.Empty<string>(), null);

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.CheckRegistry("2.14", registryService);

        result.Should().BeFalse();

        console.Output.Should().Contain(MissingRegistryKey(ClsidKey));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void ProfilerNotFoundRegistry()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
        var registryService = MockRegistryService(Array.Empty<string>(), "dummyPath/" + Path.GetFileName(ProfilerPath));

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.CheckRegistry("2.14", registryService);

        result.Should().BeFalse();

        console.Output.Should().NotContain(MissingRegistryKey(ClsidKey));
        console.Output.Should().Contain(MissingProfilerRegistry(ClsidKey, "dummyPath/" + Path.GetFileName(ProfilerPath)));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public void WrongProfilerRegistry()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
        var registryService = MockRegistryService(Array.Empty<string>(), "wrongProfiler.dll");

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.CheckRegistry("2.14", registryService);

        result.Should().BeFalse();

        console.Output.Should().NotContain(MissingRegistryKey(ClsidKey));
        console.Output.Should().Contain(Resources.WrongProfilerRegistry(ClsidKey, "wrongProfiler.dll"));
    }

    [SkippableFact]
    public void LinuxInstallationDirectory()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var (extension, archPath) = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), EnvironmentTools.GetTestTargetPlatform(), Utils.IsAlpine()) switch
            {
                ("win", _, "X64", _) => ("dll", "win-x64"),
                ("win", _, "X86", _) => ("dll", "win-x86"),
                ("linux", "Arm64", _, false) => ("so", "linux-arm64"),
                ("linux", "Arm64", _, true) => ("so", "linux-musl-arm64"),
                ("linux", "X64", _, false) => ("so", "linux-x64"),
                ("linux", "X64", _, true) => ("so", "linux-musl-x64"),
                ("osx", _, _, _) => ("dylib", "osx"),
                var unsupportedTarget => throw new PlatformNotSupportedException(unsupportedTarget.ToString())
            };

            var dir = Path.Join(tempDirectory, archPath);
            var path = Path.Join(dir, $"Datadog.Trace.ClrProfiler.Native.{extension}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, string.Empty);

            using var console = ConsoleHelper.Redirect();

            ProcessBasicCheck.CheckLinuxInstallation(tempDirectory);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                console.Output.Should().Contain(CorrectLinuxDirectoryFound(dir));
            }
        }
        finally
        {
            // cleanup
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData("DOTNET_EnableDiagnostics", "1")]
    [InlineData("DOTNET_EnableDiagnostics", "0")]
    [InlineData("COMPlus_EnableDiagnostics", "1")]
    [InlineData("COMPlus_EnableDiagnostics", "0")]
    [InlineData("DOTNET_EnableDiagnostics_Profiler", "0")]
    [InlineData("DOTNET_EnableDiagnostics_Profiler", "1")]
    [InlineData("COMPlus_EnableDiagnostics_Profiler", "0")]
    [InlineData("COMPlus_EnableDiagnostics_Profiler", "1")]
    public async Task EnableDiagnostics(string key, string value)
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        using var helper = await StartConsole(enableProfiler: true, (key, value));
        var processInfo = ProcessInfo.GetProcessInfo(helper.Process.Id);

        processInfo.Should().NotBeNull();

        using var console = ConsoleHelper.Redirect();

        var result = ProcessBasicCheck.Run(processInfo!, MockRegistryService(Array.Empty<string>(), ProfilerPath));

        using var scope = new AssertionScope();
        scope.AddReportable("Output", console.Output);

        if (value == "1")
        {
            result.Should().BeTrue();
            console.Output.Should().NotContain(EnableDiagnosticsSet(key));
        }
        else
        {
            result.Should().BeFalse();
            console.Output.Should().Contain(EnableDiagnosticsSet(key));
        }
    }

    private static IRegistryService MockRegistryService(string[] frameworkKeyValues, string? profilerKeyValue, bool wow64 = false)
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
