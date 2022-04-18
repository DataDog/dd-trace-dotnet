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
    public LiveDebuggerTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task LiveDebuggerDisabled_DebuggerDisabledByDefault_NoDebuggerTypesCreated()
    {
        await RunTest();
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task LiveDebuggerDisabled_DebuggerExplicitlyDisabled_NoDebuggerTypesCreated()
    {
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DebuggerEnabled, "0");
        await RunTest();
    }

    private async Task RunTest()
    {
        var testType =
            typeof(IRun)
               .Assembly.GetTypes()
               .Where(t => t.GetInterface(nameof(IRun)) != null)
               .First(t => DebuggerTestHelper.CreateProbeDefinition(t, EnvironmentHelper.GetTargetFramework(), unlisted: false) != null);

        var httpPort = TcpPortProvider.GetOpenPort();
        Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

        using var agent = EnvironmentHelper.GetMockAgent();
        SetEnvironmentVariable(ConfigurationKeys.AgentPort, agent.Port.ToString());

        using var process = StartSample(agent, testType.FullName, string.Empty, 5000);
        await WaitTracerToInitialize();

        try
        {
            var memoryAssertions = MemoryAssertions.CaptureSnapshotToAssertOn(process);

            memoryAssertions.NoObjectsExist<ConfigurationPoller>();
            memoryAssertions.NoObjectsExist<DebuggerSink>();
            memoryAssertions.NoObjectsExist<LineProbeResolver>();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }

        Task WaitTracerToInitialize()
        {
            return Task.Delay(1000);
        }
    }
}
#endif
