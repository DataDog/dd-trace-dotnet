// <copyright file="LiveDebuggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Debugger.Assertions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.TestHelpers;
using Samples.Probes;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger;

#if !NETCOREAPP2_1
[CollectionDefinition(nameof(LiveDebuggerTests), DisableParallelization = true)]
[Collection(nameof(LiveDebuggerTests))]
[UsesVerify]
public class LiveDebuggerTests : TestHelper
{
    private const string LogFileNamePrefix = "dotnet-tracer-managed-";
    private const string LiveDebuggerDisabledLogEntry = "Live Debugger is disabled. To enable it, please set DD_DEBUGGER_ENABLED environment variable to 'true'.";

    public LiveDebuggerTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    [Fact(Skip = "Live Debugger is temporarily disabled.")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task LiveDebuggerDisabled_DebuggerDisabledByDefault_NoDebuggerTypesCreated()
    {
        await RunTest();
    }

    [Fact(Skip = "Live Debugger is temporarily disabled.")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task LiveDebuggerDisabled_DebuggerExplicitlyDisabled_NoDebuggerTypesCreated()
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.Enabled, "0");
        await RunTest();
    }

    private async Task RunTest()
    {
        var guidGenerator = new DeterministicGuidGenerator();
        var testType =
            typeof(IRun)
               .Assembly.GetTypes()
               .Where(t => t.GetInterface(nameof(IRun)) != null)
               .First(t => DebuggerTestHelper.GetAllProbes(t, EnvironmentHelper.GetTargetFramework(), unlisted: false, guidGenerator).Any());

        using var agent = EnvironmentHelper.GetMockAgent();

        using var sample = StartSample(agent, $"--test-name {testType.FullName}", string.Empty, aspNetCorePort: 5000);
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{sample.ProcessName}*");
        await logEntryWatcher.WaitForLogEntry(LiveDebuggerDisabledLogEntry);

        try
        {
            var memoryAssertions = MemoryAssertions.CaptureSnapshotToAssertOn(sample);

            memoryAssertions.NoObjectsExist<ConfigurationPoller>();
            memoryAssertions.NoObjectsExist<DebuggerSink>();
            memoryAssertions.NoObjectsExist<LineProbeResolver>();
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
