// <copyright file="ProbesTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Samples.Probes.TestRuns;
using Samples.Probes.TestRuns.ExpressionTests;
using Samples.Probes.TestRuns.SmokeTests;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests;

public abstract class ProbesTestsBase : TestHelper
{
    protected const string AddedProbesInstrumentedLogEntry = "Dynamic Instrumentation.InstrumentProbes: Request to instrument added probes definitions completed.";
    protected const string RemovedProbesInstrumentedLogEntry = "Dynamic Instrumentation.InstrumentProbes: Request to de-instrument probes definitions completed.";
    private const string TestMaxEvaluationTimeInMilliseconds = "1000";

    private readonly string _snapshotPrefix;

    protected ProbesTestsBase(string sampleAppName, string snapshotPrefix, ITestOutputHelper output)
        : base(sampleAppName, Path.Combine("test", "test-applications", "debugger"), output)
    {
        _snapshotPrefix = snapshotPrefix;
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.EvaluationTimeoutMs, TestMaxEvaluationTimeInMilliseconds);
    }

    private protected LogEntryWatcher CreateLogEntryWatcher(string suffix = "", [CallerMemberName] string testName = null)
    {
        // Create a subdirectory for the logs based on the test name and suffix
        // And write logs there instead
        var logDir = Path.Combine(LogDirectory, $"{testName}{suffix}");
        Directory.CreateDirectory(logDir);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);

        string processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : EnvironmentHelper.FullSampleName;
        return new LogEntryWatcher($"dotnet-tracer-managed-{processName}*", logDir, Output);
    }

    private protected async Task RunMethodProbeTests(ProbeTestDescription testDescription, bool useStatsD)
    {
        var probes = GetProbeConfiguration(testDescription.TestType, false, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent(useStatsD: useStatsD);
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher($"optimized_{testDescription.IsOptimized}_type_{testDescription.TestType.Name}");
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            var isSinglePhase = probes.Select(p => p.ProbeTestData.Phase).Distinct().Count() == 1;
            if (isSinglePhase)
            {
                await PerformSinglePhaseProbeTest();
            }
            else
            {
                await PerformMultiPhasesProbeTest();
            }

            async Task PerformSinglePhaseProbeTest()
            {
                var snapshotProbes = probes.Select(p => p.Probe).ToArray();
                var probeData = probes.Select(p => p.ProbeTestData).ToArray();

                await RunPhase(snapshotProbes, probeData);
            }

            async Task PerformMultiPhasesProbeTest()
            {
                var phaseNumber = 0;
                var groupedPhases =
                    probes
                       .GroupBy(p => p.ProbeTestData.Phase)
                       .OrderBy(group => group.Key);

                foreach (var groupedPhase in groupedPhases)
                {
                    phaseNumber++;

                    var snapshotProbes = groupedPhase.Select(p => p.Probe).ToArray();
                    var probeData = groupedPhase.Select(p => p.ProbeTestData).ToArray();
                    await RunPhase(snapshotProbes, probeData, isMultiPhase: true, phaseNumber);
                }
            }

            async Task RunPhase(ProbeDefinition[] snapshotProbes, ProbeAttributeBase[] probeData, bool isMultiPhase = false, int phaseNumber = 1)
            {
                SetProbeConfiguration(agent, snapshotProbes);

                try
                {
                    if (phaseNumber == 1)
                    {
                        await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);
                    }
                    else
                    {
                        await logEntryWatcher.WaitForLogEntries(new[] { AddedProbesInstrumentedLogEntry, RemovedProbesInstrumentedLogEntry });
                    }
                }
                catch (InvalidOperationException e) when (e.Message.StartsWith("Log file was not found for path:"))
                {
                    if (testDescription.TestType == typeof(UndefinedValue) || testDescription.TestType == typeof(MultidimensionalArrayTest))
                    {
                        return;
                    }
                }

                await sample.RunCodeSample();

                await VerifyMetricProbeResults(testDescription, probeData, agent, isMultiPhase, phaseNumber);

                await VerifyLogProbeResults(testDescription, probeData, agent, isMultiPhase, phaseNumber);

                if (probeData.Count(DebuggerTestHelper.IsSpanDecorationProbe) > 0)
                {
                    await VerifySpanDecorationResults(sample, testDescription, probeData, agent, isMultiPhase, phaseNumber);
                }
                else
                {
                    await VerifySpanProbeResults(snapshotProbes, testDescription, probeData, agent, isMultiPhase, phaseNumber);
                }

                // The Datadog-Agent is continuously receiving probe statuses.
                // We may have outdated probe statuses that were sent before the instrumentation took place.
                // To ensure consistency, we are clearing the probe statuses and requesting a fresh batch.
                // This will ensure that the next set of probe statuses received will be up-to-date and accurate.
                agent.ClearProbeStatuses();

                // If there are log probes that expect 0 snapshots it means it's a test that checks failure installation.
                // For a reference, look at: ByRefLikeTest.
                var expectedFailedStatuses = probeData.Count(probeData => probeData.ExpectProbeStatusFailure);

                var statuses = await agent.WaitForProbesStatuses(probeData.Length, expectedFailedStatuses: expectedFailedStatuses);

                Assert.Equal(probeData.Length, statuses?.Length);
                await ApproveStatuses(statuses, testDescription, isMultiPhase, phaseNumber);
                agent.ClearProbeStatuses();
            }
        }
        finally
        {
            await sample.StopSample();
        }
    }

    private protected Task ApproveSnapshots(string[] snapshots, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        return Approver.ApproveSnapshots(snapshots, GetTestName(testDescription, isMultiPhase, phaseNumber), Output);
    }

    private protected Task ApproveStatuses(string[] statuses, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        return Approver.ApproveStatuses(statuses, GetTestName(testDescription, isMultiPhase, phaseNumber), Output);
    }

    private protected (ProbeAttributeBase ProbeTestData, ProbeDefinition Probe)[] GetProbeConfiguration(Type testType, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var probes = DebuggerTestHelper.GetAllProbes(testType, EnvironmentHelper.GetTargetFramework(), unlisted, guidGenerator);
        if (!probes.Any())
        {
            throw new SkipException($"No probes for {testType.Name}, skipping.");
        }

        return probes;
    }

    private protected void SetDebuggerEnvironment(MockTracerAgent agent)
    {
        SetEnvironmentVariable(ConfigurationKeys.ServiceName, EnvironmentHelper.SampleName);
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000");
        SetProbeConfiguration(agent, Array.Empty<LogProbe>());
    }

    private protected void SetProbeConfiguration(MockTracerAgent agent, ProbeDefinition[] snapshotProbes)
    {
        var configurations = snapshotProbes
            .Select(snapshotProbe =>
                             {
                                 var path = snapshotProbe switch
                                 {
                                     LogProbe log => DefinitionPaths.LogProbe,
                                     MetricProbe metric => DefinitionPaths.MetricProbe,
                                     SpanProbe span => DefinitionPaths.SpanProbe,
                                     SpanDecorationProbe span => DefinitionPaths.SpanDecorationProbe,
                                     _ => throw new ArgumentOutOfRangeException(snapshotProbe.GetType().FullName, "Add a new probe kind"),
                                 };
                                 return (snapshotProbe, RcmProducts.LiveDebugging, $"{path}{snapshotProbe.Id}");
                             })
            .Select(dummy => ((object Config, string ProductName, string Id))dummy);

        agent.SetupRcm(Output, configurations);
    }

    private async Task VerifySpanDecorationResults(DebuggerSampleProcessHelper sample, ProbeTestDescription testDescription, ProbeAttributeBase[] probeData, MockTracerAgent agent, bool isMultiPhase, int phaseNumber)
    {
        int expectedSpanCount;
        string testNameSuffix;
        if (sample.Process.StartInfo.EnvironmentVariables.ContainsKey("DD_TRACE_METHODS"))
        {
            testNameSuffix = "with.trace.annotation";
            expectedSpanCount = 2;
        }
        else
        {
            testNameSuffix = "with.dynamic.span";
            expectedSpanCount = 1;
        }

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber("out.host: localhost", "out.host: debugger");
        settings.AddSimpleScrubber("out.host: mysql_arm64", "out.host: debugger");
        var testName = isMultiPhase ? $"{testDescription.TestType.Name}_#{phaseNumber}." : testDescription.TestType.Name;
        settings.UseFileName($"{_snapshotPrefix}.{testName}.{testNameSuffix}");

        var spans = await agent.WaitForSpansAsync(expectedSpanCount);

        Assert.Equal(expectedSpanCount, spans.Count);

        VerifierSettings.DerivePathInfo(
            (_, projectDirectory, _, _) => new(directory: Path.Combine(projectDirectory, "Approvals", "snapshots")));

        SanitizeSpanTags(spans);

        await VerifyHelper.VerifySpans(spans, settings).DisableRequireUniquePrefix();
    }

    private void SanitizeSpanTags(IImmutableList<MockSpan> spans)
    {
        const string errorTagStartWith = "_dd.di.";
        const string errorTagEndWith = ".evaluation_error";

        foreach (var span in spans)
        {
            // Remove process tags that can be injected on the first span in a payload
            span.Tags.Remove(global::Datadog.Trace.Tags.ProcessTags);

            var toSanitize = span.Tags.Where(tag => tag.Key.StartsWith(errorTagStartWith) && tag.Key.EndsWith(errorTagEndWith)).ToList();
            foreach (var keyValuePair in toSanitize)
            {
                span.Tags[keyValuePair.Key] = keyValuePair.Value.Substring(0, keyValuePair.Value.IndexOf(',')) + " }";
            }
        }

        SanitizeCodeOriginExitFrames(spans);
    }

    private void SanitizeCodeOriginExitFrames(IImmutableList<MockSpan> spans)
    {
        const string codeOriginFrame = "_dd.code_origin.frames";
        foreach (var span in spans)
        {
            var toSanitize = span.Tags.Where(tag => tag.Key.StartsWith(codeOriginFrame)).ToList();
            foreach (var keyValuePair in toSanitize)
            {
                span.Tags.Remove(keyValuePair.Key);
            }
        }
    }

    private async Task VerifySpanProbeResults(ProbeDefinition[] snapshotProbes, ProbeTestDescription testDescription, ProbeAttributeBase[] probeData, MockTracerAgent agent, bool isMultiPhase, int phaseNumber)
    {
        var spanProbes = probeData.Where(DebuggerTestHelper.IsSpanProbe).ToArray();

        if (spanProbes.Any())
        {
            const string spanProbeOperationName = "dd.dynamic.span";

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddSimpleScrubber("out.host: localhost", "out.host: debugger");
            settings.AddSimpleScrubber("out.host: mysql_arm64", "out.host: debugger");
            var testName = isMultiPhase ? $"{testDescription.TestType.Name}_#{phaseNumber}." : testDescription.TestType.Name;
            settings.UseFileName($"{_snapshotPrefix}.{testName}.Spans");

            var spans = await agent.WaitForSpansAsync(spanProbes.Length, operationName: spanProbeOperationName);
            SanitizeCodeOriginExitFrames(spans);

            // Assert.Equal(spanProbes.Length, spans.Count);
            foreach (var span in spans)
            {
                var result = Result.FromSpan(span)
                                   .Properties(s => s
                                                  .Matches(_ => (nameof(span.Name), span.Name), spanProbeOperationName))
                                   .Tags(s => s
                                             .Matches("component", "trace")
                                             .MatchesOneOf("debugger.probeid", Enumerable.Select<ProbeDefinition, string>(snapshotProbes, p => p.Id).ToArray()));

                // Assert.True(result.Success, result.ToString());
            }

            VerifierSettings.DerivePathInfo((_, projectDirectory, _, _) => new(directory: Path.Combine(projectDirectory, "Approvals", "snapshots")));

            await VerifyHelper.VerifySpans(spans, settings).DisableRequireUniquePrefix();
        }
    }

    private async Task VerifyLogProbeResults(ProbeTestDescription testDescription, ProbeAttributeBase[] probeData, MockTracerAgent agent, bool isMultiPhase, int phaseNumber)
    {
        var logProbes = probeData.Where(DebuggerTestHelper.IsLogProbe).ToArray();

        if (!logProbes.Any())
        {
            return;
        }

        var expectedNumberOfSnapshots = DebuggerTestHelper.CalculateExpectedNumberOfSnapshots(logProbes);
        string[] snapshots;
        if (expectedNumberOfSnapshots == 0)
        {
            Assert.True(await agent.WaitForNoSnapshots(), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");
        }
        else
        {
            snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            await ApproveSnapshots(snapshots, testDescription, isMultiPhase, phaseNumber);
            agent.ClearSnapshots();
        }
    }

    private async Task VerifyMetricProbeResults(ProbeTestDescription testDescription, ProbeAttributeBase[] probeData, MockTracerAgent agent, bool isMultiPhase, int phaseNumber)
    {
        var metricProbes = probeData.Where(DebuggerTestHelper.IsMetricProbe).ToArray();

        if (!metricProbes.Any())
        {
            return;
        }

        var expectedNumberOfSnapshots = DebuggerTestHelper.CalculateExpectedNumberOfSnapshots(metricProbes);

        if (expectedNumberOfSnapshots > 0)
        {
            // meaning there is an error so we don't receive metrics but an evaluation error (as a snapshot)
            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            await ApproveSnapshots(snapshots, testDescription, isMultiPhase, phaseNumber);
            agent.ClearSnapshots();
        }
        else
        {
            var normalizedServiceName = EnvironmentHelper.SampleName.ToLowerInvariant();
            var requests = await agent.WaitForStatsdRequests(metricProbes.Length);
            requests.Should().OnlyContain(s => s.Contains($"service:{normalizedServiceName}"));

            var retried = false;

            foreach (var probeAttributeBase in metricProbes)
            {
                var metricName = (probeAttributeBase as MetricMethodProbeTestDataAttribute)?.MetricName ?? (probeAttributeBase as MetricLineProbeTestDataAttribute)?.MetricName;
                Assert.NotNull(metricName);
                var req = requests.SingleOrDefault(r => r.Contains(metricName));

                if (!retried && req == null)
                {
                    retried = true;
                    await Task.Delay(2000);
                    requests = await agent.WaitForStatsdRequests(metricProbes.Length);
                    req = requests.SingleOrDefault(r => r.Contains(metricName)); // retry
                }

                Assert.NotNull(req);
                req.Should().Contain($"service:{normalizedServiceName}");
                req.Should().Contain($"probe-id:{probeAttributeBase.ProbeId}");
                req.Should().Contain($"debugger.probeid:{probeAttributeBase.ProbeId}");
            }
        }
    }

    private string GetTestName(ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        var testName = isMultiPhase ? $"{testDescription.TestType.Name}_#{phaseNumber}." : testDescription.TestType.Name;
        return $"{_snapshotPrefix}.{testName}";
    }
}
