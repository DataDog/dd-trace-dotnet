// <copyright file="DebuggerManagerDynamicTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.IntegrationTests.Assertions;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.TestHelpers;
using Samples.Probes.TestRuns.SmokeTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Datadog.Trace.Debugger.IntegrationTests;

// These tests crashed on NETCOREAPP2_1 and often hang on .NET 8 or greater (mostly on x86 but sometimes also on x64).
#if !NETCOREAPP2_1 && !NET8_0_OR_GREATER
[CollectionDefinition(nameof(DebuggerManagerDynamicTests), DisableParallelization = true)]
[Collection(nameof(DebuggerManagerDynamicTests))]
[UsesVerify]
public class DebuggerManagerDynamicTests : TestHelper
{
    private const string LogFileNamePrefix = "dotnet-tracer-managed-";

    // Log messages to verify dynamic state changes
    private const string DynamicInstrumentationEnabledLogEntry = "Initializing Dynamic Instrumentation";
    private const string ExceptionReplayEnabledLogEntry = "Initializing Exception Replay";
    private const string CodeOriginForSpansEnabledLogEntry = "Initializing Code Origin for Spans";
    private const string ApplyingDynamicDebuggerConfigLogEntry = "Applying new dynamic debugger configuration";
    private const string DisabledByRemoteConfiguration = "is disabled by remote enablement.";
    private const string TracerInitialized = "The profiler has been initialized";
    private const string DebuggerConfigurationInitialized = "DATADOG DEBUGGER CONFIGURATION";

    public DebuggerManagerDynamicTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_DynamicInstrumentation_StartDisabled_EnabledViaRemoteConfig()
    {
        // Set it true so we won't go through path of no debugger products at all
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");

        await RunDynamicConfigurationTest(
            false,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, no DI objects should exist
                memoryAssertions.NoObjectsExist<DynamicInstrumentation>();
                memoryAssertions.NoObjectsExist<LineProbeResolver>();
                memoryAssertions.NoObjectsExist<Symbols.SymbolsUploader>();
            },
            remoteConfig: new { dynamic_instrumentation_enabled = true },
            DynamicInstrumentationEnabledLogEntry,
            finalMemoryAssertions: memoryAssertions =>
            {
                // After enabling DI via remote config, symbol uploader should be created
                memoryAssertions.ObjectsExist<Symbols.SymbolsUploader>();
            });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_ExceptionReplay_StartDisabled_EnabledViaRemoteConfig()
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");

        await RunDynamicConfigurationTest(
            false,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, no Exception Replay objects should exist
                memoryAssertions.NoObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
            },
            remoteConfig: new { exception_replay_enabled = true },
            ExceptionReplayEnabledLogEntry);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_CodeOrigin_StartDisabled_EnabledViaRemoteConfig()
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");

        await RunDynamicConfigurationTest(
            false,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, no Code Origin object should exist
                memoryAssertions.NoObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
            },
            remoteConfig: new { code_origin_enabled = true },
            CodeOriginForSpansEnabledLogEntry);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_MultipleProducts_StartDisabled_EnabledViaRemoteConfig()
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");

        await RunDynamicConfigurationTest(
            false,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, no debugger objects should exist
                memoryAssertions.NoObjectsExist<DynamicInstrumentation>();
                memoryAssertions.NoObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
                memoryAssertions.NoObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
                memoryAssertions.NoObjectsExist<SnapshotSink>();
                memoryAssertions.NoObjectsExist<LineProbeResolver>();
                memoryAssertions.NoObjectsExist<Symbols.SymbolsUploader>();
            },
            remoteConfig: new
            {
                dynamic_instrumentation_enabled = true,
                exception_replay_enabled = true,
                code_origin_enabled = true
            },
            ExceptionReplayEnabledLogEntry,
            finalMemoryAssertions: memoryAssertions =>
            {
                // After remote config, all objects should be created
                memoryAssertions.ObjectsExist<DynamicInstrumentation>();
                memoryAssertions.ObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
                memoryAssertions.ObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
                memoryAssertions.ObjectsExist<SnapshotSink>();
                memoryAssertions.ObjectsExist<LineProbeResolver>();
                memoryAssertions.ObjectsExist<Symbols.SymbolsUploader>();
            });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_DynamicInstrumentation_StartEnabled_DisabledViaRemoteConfig()
    {
        // Start with DI enabled via environment variable
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");

        await RunDynamicConfigurationTest(
            true,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, DI objects should exist
                memoryAssertions.ObjectsExist<DynamicInstrumentation>();
                memoryAssertions.ObjectsExist<SnapshotSink>();
                memoryAssertions.ObjectsExist<LineProbeResolver>();
                memoryAssertions.ObjectsExist<Symbols.SymbolsUploader>();
            },
            remoteConfig: new { dynamic_instrumentation_enabled = false },
            $"Dynamic Instrumentation {DisabledByRemoteConfiguration}",
            finalMemoryAssertions: memoryAssertions =>
            {
                // After disabling DI, symbol uploader should still exist (it's not disposed when DI is disabled)
                // This is because symbol uploader initialization is one-time only
                memoryAssertions.ObjectsExist<Symbols.SymbolsUploader>();
            });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_ExceptionReplay_StartEnabled_DisabledViaRemoteConfig()
    {
        // Start with Exception Replay enabled via environment variable
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");

        await RunDynamicConfigurationTest(
            true,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, Exception Replay objects should exist
                memoryAssertions.ObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
            },
            remoteConfig: new { exception_replay_enabled = false },
            $"Exception Replay {DisabledByRemoteConfiguration}");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_CodeOrigin_StartEnabled_DisabledViaRemoteConfig()
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");

        await RunDynamicConfigurationTest(
            true,
            initialMemoryAssertions: memoryAssertions =>
            {
                // Initially, Code Origin object should exist
                memoryAssertions.ObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
            },
            remoteConfig: new { code_origin_enabled = false },
            $"Code Origin for Spans {DisabledByRemoteConfiguration}");
    }

    private async Task RunDynamicConfigurationTest(
        bool startEnabled,
        Action<MemoryAssertions> initialMemoryAssertions,
        object remoteConfig,
        string logToWaitAfterRc,
        Action<MemoryAssertions>? finalMemoryAssertions = null,
        [CallerMemberName] string? testName = null)
    {
#if NET8_0_OR_GREATER
        // These tests often hang on x86 on .NET 8+. Needs investigation
        Skip.If(!EnvironmentTools.IsTestTarget64BitProcess());
#endif

        var logPath = Path.Combine(LogDirectory, $"{testName}");
        Directory.CreateDirectory(logPath);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logPath);

        var testType = DebuggerTestHelper.SpecificTestDescription(typeof(AsyncVoid));

        using var agent = EnvironmentHelper.GetMockAgent();
        var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Probes";
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", logPath, Output);
        using var sample = await StartSample(agent, $"--test-name {testType.TestType}", string.Empty, aspNetCorePort: 5000);

        // Wait for initial setup and verify initial state (products should be enabled)
        if (startEnabled)
        {
            await logEntryWatcher.WaitForLogEntry(DebuggerConfigurationInitialized);
        }
        else
        {
            await logEntryWatcher.WaitForLogEntry(TracerInitialized);
        }

        try
        {
            var memoryAssertionTimeout = TimeSpan.FromSeconds(10);
            var initialMemorySnapshot = await MemoryAssertions.TryCaptureSnapshotToAssertOn(
                                            sample,
                                            Output,
                                            memoryAssertionTimeout);

            if (initialMemorySnapshot == null)
            {
                var skipReason = $"Initial memory assertion timed out after {memoryAssertionTimeout.TotalSeconds}s in {testName}. This may be due to ClrMD/runtime issues mostly on .NET 8 or .NET Core 2.1.";
                throw new SkipException(skipReason);
            }

            initialMemoryAssertions(initialMemorySnapshot);

            // Apply remote configuration
            var fileId = Guid.NewGuid().ToString();
            var configurations = new[] { ((object)new { lib_config = remoteConfig }, "APM_TRACING", fileId) };

            Output.WriteLine($"Sending remote config: {System.Text.Json.JsonSerializer.Serialize(remoteConfig)}");
            await agent.SetupRcmAndWait(Output, configurations);

            // Wait for the configuration to be applied and log entry to appear
            await logEntryWatcher.WaitForLogEntry(ApplyingDynamicDebuggerConfigLogEntry);

            // Verify final state
            if (finalMemoryAssertions != null)
            {
                var finalMemorySnapshot = await MemoryAssertions.TryCaptureSnapshotToAssertOn(
                                              sample,
                                              Output,
                                              memoryAssertionTimeout);

                if (finalMemorySnapshot == null)
                {
                    var skipReason = $"Final memory assertion timed out after {memoryAssertionTimeout.TotalSeconds}s in {testName}. This may be due to ClrMD/runtime issues mostly on .NET 8 or .NET Core 2.1.";
                    throw new SkipException(skipReason);
                }

                finalMemoryAssertions(finalMemorySnapshot);
            }

            await logEntryWatcher.WaitForLogEntry(logToWaitAfterRc);
        }
        finally
        {
            if (!sample.HasExited)
            {
                sample.Kill();
            }
        }
    }
}
#endif
