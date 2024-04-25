// <copyright file="TracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[Collection(nameof(DynamicConfigurationTests))]
public class TracerTests : TestHelper
{
    private const string LogFileNamePrefix = "dotnet-tracer-managed-";
    private const string DiagnosticLog = "DATADOG TRACER CONFIGURATION";

    public TracerTests(ITestOutputHelper output)
        : base("Console", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task InitializesTracerWhenTracingIsDisabled()
    {
        EnvironmentHelper.CustomEnvironmentVariables["DD_TRACE_ENABLED"] = "0";
        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", LogDirectory);
        using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");

        // Throws if the log entry is not found
        _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

        // Tracing is disabled, we shouldn't have spans, even though they wrote some
        agent.Spans.Should().BeEmpty();
        agent.AssertConfiguration("DD_TRACE_ENABLED", false);
    }
}
