// <copyright file="DebuggerManagerTests.cs" company="Datadog">
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

// These tests crashed on NETCOREAPP2_1 and often hang on .NET 8 (mostly on x86 but sometimes also on x64).
#if !NETCOREAPP2_1 && !NET8_0
[CollectionDefinition(nameof(DebuggerManagerTests), DisableParallelization = true)]
[Collection(nameof(DebuggerManagerTests))]
[UsesVerify]
public class DebuggerManagerTests : TestHelper
{
    private const string LogFileNamePrefix = "dotnet-tracer-managed-";

    // Log messages to verify debugger states
    private const string RemoteConfigNotAvailableLogEntry = "Remote configuration is not available in this environment";
    private const string DebuggerConfigurationLogEntry = "DATADOG DEBUGGER CONFIGURATION";
    private const string TracerInitialized = "The profiler has been initialized";

    public DebuggerManagerTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_AllFeaturesByDefault_NoDebuggerObjectsCreated()
    {
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.NoObjectsExist<SnapshotSink>();
            memoryAssertions.NoObjectsExist<LineProbeResolver>();
            memoryAssertions.NoObjectsExist<DynamicInstrumentation>();
            memoryAssertions.NoObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
            memoryAssertions.NoObjectsExist<Symbols.SymbolsUploader>();
            memoryAssertions.ObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_DynamicInstrumentationExplicitlyDisabled_NoDebuggerObjectsCreated()
    {
        // at least one product should be enabled to initialize the debugger manager
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "false");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.NoObjectsExist<SnapshotSink>();
            memoryAssertions.NoObjectsExist<LineProbeResolver>();
            memoryAssertions.NoObjectsExist<DynamicInstrumentation>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_ExceptionReplayExplicitlyDisabled_NoExceptionReplayObjectsCreated()
    {
        // at least one product should be enabled to initialize the debugger manager
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.NoObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_CodeOriginExplicitlyDisabled_NoCodeOriginObjectsCreated()
    {
        // at least one product should be enabled to initialize the debugger manager
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "false");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.NoObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_SymbolDatabaseUploadDisabled_NoSymbolUploaderCreated()
    {
        // at least one product should be enabled to initialize the debugger manager
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "false");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.NoObjectsExist<Pdb.DatadogMetadataReader>();
            memoryAssertions.NoObjectsExist<Symbols.SymbolsUploader>();
            memoryAssertions.NoObjectsExist<Symbols.SymbolExtractor>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_DynamicInstrumentationEnabled_WithoutRemoteConfig_LogsWarning()
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true");
        await RunDebuggerManagerTestWithMemoryAssertions(null, RemoteConfigNotAvailableLogEntry);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_DynamicInstrumentationEnabled_WitRemoteConfig_CreateDynamicInstrumentationObjects()
    {
        SetEnvironmentVariable(ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.ObjectsExist<DynamicInstrumentation>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_ExceptionReplayEnabled_CreatesExceptionReplayObjects()
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.ObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_CodeOriginEnabled_CreatesCodeOriginObjects()
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.ObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_MultipleFeaturesCombined_CreatesAppropriateObjects()
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "false");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true");

        await RunDebuggerManagerTestWithMemoryAssertions(memoryAssertions =>
        {
            memoryAssertions.ObjectsExist<ExceptionAutoInstrumentation.ExceptionReplay>();
            memoryAssertions.NoObjectsExist<SpanCodeOrigin.SpanCodeOrigin>();
            memoryAssertions.ObjectsExist<DynamicInstrumentation>();
            memoryAssertions.ObjectsExist<Symbols.SymbolsUploader>();
        });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task DebuggerManager_StartupDiagnosticLogEnabled_WritesConfigurationLog()
    {
        SetEnvironmentVariable(ConfigurationKeys.StartupDiagnosticLogEnabled, "true");

        // at least one product should be enabled to initialize the debugger manager
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true");
        await RunDebuggerManagerTestWithMemoryAssertions(null, DebuggerConfigurationLogEntry);
    }

    private async Task RunDebuggerManagerTestWithMemoryAssertions(
        Action<MemoryAssertions>? assertionAction = null,
        string? expectedLogEntry = null,
        [CallerMemberName] string? testName = null)
    {
#if NET8_0_OR_GREATER
        // These tests often hang on x86 on .NET 8+. Needs investigation
        Skip.If(!EnvironmentTools.IsTestTarget64BitProcess());
#endif

        // Create a subdirectory for the logs based on the test name
        var logPath = Path.Combine(LogDirectory, $"{testName}");
        Directory.CreateDirectory(logPath);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logPath);

        var testType = DebuggerTestHelper.SpecificTestDescription(typeof(AsyncVoid));

        using var agent = EnvironmentHelper.GetMockAgent();
        var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Probes";
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", logPath, Output);
        using var sample = await StartSample(agent, $"--test-name {testType.TestType}", string.Empty, aspNetCorePort: 5000);

        // Wait for initial setup and verify initial state
        await logEntryWatcher.WaitForLogEntry(TracerInitialized);
        try
        {
            // Wait for expected log entry if specified
            if (!string.IsNullOrEmpty(expectedLogEntry))
            {
                await logEntryWatcher.WaitForLogEntry(expectedLogEntry);
            }

            if (assertionAction != null)
            {
                var memoryAssertionTimeout = TimeSpan.FromSeconds(10);
                var memoryAssertions = await MemoryAssertions.TryCaptureSnapshotToAssertOn(
                                           sample,
                                           Output,
                                           memoryAssertionTimeout);

                if (memoryAssertions == null)
                {
                    var skipReason = $"Memory assertion timed out after {memoryAssertionTimeout.TotalSeconds}s in {testName}. This may be due to ClrMD/runtime issues mostly on .NET 8 or .NET Core 2.1.";
                    throw new SkipException(skipReason);
                }

                assertionAction(memoryAssertions);
            }
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
