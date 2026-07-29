// <copyright file="TracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
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
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", LogDirectory, Output);
        using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");

        // Throws if the log entry is not found
        _ = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);

        // Tracing is disabled, we shouldn't have spans, even though they wrote some
        agent.Spans.Should().BeEmpty();
        await agent.AssertConfigurationAsync("DD_TRACE_ENABLED", false);
    }

#if NET6_0_OR_GREATER
    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsOtlpExportStateInStartupLog(bool enabled)
    {
        if (enabled)
        {
            SetEnvironmentVariable("OTEL_TRACES_EXPORTER", "otlp");
            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", "true");
            SetEnvironmentVariable("DD_LOGS_OTEL_ENABLED", "true");
        }

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        var processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Console";
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{processName}*", LogDirectory, Output);
        using var processResult = await RunSampleAndWaitForExit(agent, "traces 1");

        var entry = await logEntryWatcher.WaitForLogEntry(DiagnosticLog);
        var match = Regex.Match(entry, @".+ (?<diagnosticLog>\{.+\})\s+\{.+\}");
        match.Success.Should().BeTrue();

        var json = JObject.Parse(match.Groups["diagnosticLog"].Value);
        foreach (var field in new[]
                 {
                     "otlp_traces_export_enabled",
                     "otlp_metrics_export_enabled",
                     "otlp_logs_export_enabled",
                 })
        {
            json[field]?.Type.Should().Be(JTokenType.Boolean);
            json[field]?.Value<bool>().Should().Be(enabled);
        }
    }
#endif
}
