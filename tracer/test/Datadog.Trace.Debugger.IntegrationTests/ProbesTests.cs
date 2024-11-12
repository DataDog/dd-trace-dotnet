// <copyright file="ProbesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Samples.Probes.TestRuns;
using Samples.Probes.TestRuns.ExpressionTests;
using Samples.Probes.TestRuns.SmokeTests;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests;

[CollectionDefinition(nameof(ProbesTests), DisableParallelization = true)]
[Collection(nameof(ProbesTests))]
[UsesVerify]
public class ProbesTests : TestHelper
{
    private const string AddedProbesInstrumentedLogEntry = "Live Debugger.InstrumentProbes: Request to instrument added probes definitions completed.";
    private const string RemovedProbesInstrumentedLogEntry = "Live Debugger.InstrumentProbes: Request to de-instrument probes definitions completed.";

    private static readonly Type[] _unoptimizedNotSupportedTypes = new[]
    {
            typeof(AsyncCallChain),
            typeof(AsyncGenericClass),
            typeof(AsyncGenericMethodWithLineProbeTest),
            typeof(AsyncGenericStruct),
            typeof(AsyncInstanceMethod),
            typeof(AsyncInterfaceProperties),
            typeof(AsyncLineProbeWithFieldsArgsAndLocalsTest),
            typeof(AsyncMethodInsideTaskRun),
            typeof(AsyncRecursiveCall),
            typeof(AsyncStaticMethod),
            typeof(AsyncThrowException),
            typeof(AsyncTemplateLocalExitFullSnapshot),
            typeof(AsyncTaskReturnTest),
            typeof(AsyncTaskReturnWithExceptionTest),
            typeof(AsyncTemplateArgExitFullSnapshot),
            typeof(AsyncVoid),
            typeof(AsyncWithGenericArgumentAndLocal),
            typeof(HasLocalsAndReturnValue),
            typeof(MultipleLineProbes),
            typeof(MultiScopesWithSameLocalNameTest),
            typeof(NotSupportedFailureTest)
    };

    public ProbesTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> UdsMemberData =>
        new List<object[]>
        {
            new object[] { typeof(MetricCountInt) },
            new object[] { typeof(AsyncGenericMethod) }
        };

    public static IEnumerable<object[]> SpanDecorationMemberData =>
        new List<object[]>
        {
            new object[] { typeof(SpanDecorationArgsAndLocals) },
            new object[] { typeof(SpanDecorationTwoTags) },
            new object[] { typeof(SpanDecorationSameTags) },
            new object[] { typeof(SpanDecorationSameTagsFirstError) },
            new object[] { typeof(SpanDecorationSameTagsSecondError) },
            new object[] { typeof(SpanDecorationError) }
        };

    public static IEnumerable<object[]> ProbeTests()
    {
        return DebuggerTestHelper.AllTestDescriptions();
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task RedactionFromConfigurationTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(RedactionTest));
        const int expectedNumberOfSnapshots = 1;

        var guidGenerator = new DeterministicGuidGenerator();
        var probeId = guidGenerator.New().ToString();

        var probes = new[]
        {
            DebuggerTestHelper.CreateDefaultLogProbe("RedactionTest", "Run", guidGenerator: null, probeTestData: new LogMethodProbeTestDataAttribute(probeId: probeId, captureSnapshot: true))
        };

        SetEnvironmentVariable(ConfigurationKeys.Debugger.RedactedIdentifiers, "RedactMe,b");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.RedactedTypes, "Samples.Probes.TestRuns.SmokeTests.RedactMeType*,Samples.Probes.TestRuns.SmokeTests.AnotherRedactMeTypeB");

        await RunSingleTestWithApprovals(testDescription, expectedNumberOfSnapshots, probes);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task AsyncMethodInGenericClassTest()
    {
        Skip.If(true, "Not supported yet. Internal Jira Ticket: #DEBUG-1092.");

        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(AsyncMethodInGenericClassTest));
        const int expectedNumberOfSnapshots = 1;

        var guidGenerator = new DeterministicGuidGenerator();

        var probes = new[]
        {
            DebuggerTestHelper.CreateDefaultLogProbe("GenericClass`1", "Run", guidGenerator)
        };

        await RunSingleTestWithApprovals(testDescription, expectedNumberOfSnapshots, probes);
    }

    [SkippableFact(Skip = "Too flakey")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task TransparentCodeCtorInstrumentationTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(CtorTransparentCodeTest));
        const int expectedNumberOfSnapshots = 1;

        var guidGenerator = new DeterministicGuidGenerator();

        var probes = new[]
        {
            DebuggerTestHelper.CreateDefaultLogProbe("SecurityTransparentTest", ".ctor", guidGenerator),
            DebuggerTestHelper.CreateDefaultLogProbe("CtorTransparentCodeTest", "Run", guidGenerator)
        };

        await RunSingleTestWithApprovals(testDescription, expectedNumberOfSnapshots, probes);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task InstallAndUninstallMethodProbeWithOverloadsTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(OverloadAndSimpleNameTest));
        const int expectedNumberOfSnapshots = 9;

        var probes = GetProbeConfiguration(testDescription.TestType, true, new DeterministicGuidGenerator());

        if (probes.Length != 1)
        {
            throw new InvalidOperationException($"{nameof(InstallAndUninstallMethodProbeWithOverloadsTest)} expected one probe request to exist, but found {probes.Length} probes.");
        }

        await RunSingleTestWithApprovals(testDescription, expectedNumberOfSnapshots, probes.Select(p => p.Probe).ToArray());
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task LineProbeEmit100SnapshotsTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(Emit100LineProbeSnapshotsTest));
        const int expectedNumberOfSnapshots = 100;

        var probes = GetProbeConfiguration(testDescription.TestType, true, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, probes.Select(p => p.Probe).ToArray());
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var statuses = await agent.WaitForProbesStatuses(probes.Length);
            Assert.Equal(probes.Length, statuses?.Length);

            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            Assert.True(snapshots.All(IsSaneSnapshot), "Not all snapshots are sane.");
            Assert.True(snapshots.Distinct().Count() == snapshots.Length, "All snapshots should be unique.");
        }
        finally
        {
            await sample.StopSample();
        }

        bool IsSaneSnapshot(string snapshot)
        {
            var shouldBeInSnapshot = new[] { "message", "logger", "stack", "probe", "snapshot", "debugger" };

            return snapshot.Length > 250 &&
                   shouldBeInSnapshot.All(snapshot.Contains);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task MoveFromSimpleLogToSnapshotLogTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(SimpleMethodWithLocalsAndArgsTest));
        var probeId = new DeterministicGuidGenerator().New().ToString();
        var expectedNumberOfSnapshots = 1;

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            await RunSingle(phaseNumber: 1, captureSnapshot: false);
            await RunSingle(phaseNumber: 2, captureSnapshot: true);
            await RunSingle(phaseNumber: 3, captureSnapshot: false);
            await RunSingle(phaseNumber: 4, captureSnapshot: true);

            async Task RunSingle(int phaseNumber, bool captureSnapshot)
            {
                var probes = new[]
                {
                    DebuggerTestHelper.CreateDefaultLogProbe(nameof(SimpleMethodWithLocalsAndArgsTest), "Method", guidGenerator: null, probeTestData: new LogMethodProbeTestDataAttribute(probeId: probeId, captureSnapshot: captureSnapshot))
                };

                SetProbeConfiguration(agent, probes);
                await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

                await sample.RunCodeSample();

                var statuses = await agent.WaitForProbesStatuses(probes.Length);
                Assert.Equal(probes.Length, statuses?.Length);
                await ApproveStatuses(statuses, testDescription, isMultiPhase: true, phaseNumber: phaseNumber);
                agent.ClearProbeStatuses();

                var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
                Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
                await ApproveSnapshots(snapshots, testDescription, isMultiPhase: true, phaseNumber: phaseNumber);
                agent.ClearSnapshots();
            }
        }
        finally
        {
            await sample.StopSample();
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task LineProbeUnboundProbeBecomesBoundTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(UnboundProbeBecomesBoundTest));
        var guidGenerator = new DeterministicGuidGenerator();

        var probes = new[]
        {
            DebuggerTestHelper.CreateLogLineProbe(typeof(Samples.Probes.Unreferenced.External.ExternalTest), new LogLineProbeTestDataAttribute(lineNumber: 11), guidGenerator),
            DebuggerTestHelper.CreateLogLineProbe(typeof(Samples.Probes.Unreferenced.External.ExternalTest), new LogLineProbeTestDataAttribute(lineNumber: 12), guidGenerator),
        };

        var expectedNumberOfSnapshots = probes.Length;

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, probes);
            await logEntryWatcher.WaitForLogEntry($"ProbeID {probes.First().Id} is unbound.");
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            await logEntryWatcher.WaitForLogEntry($"LiveDebugger.CheckUnboundProbes: {expectedNumberOfSnapshots} unbound probes became bound.");

            Assert.True(await agent.WaitForNoSnapshots(), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");

            await sample.RunCodeSample();

            var statuses = await agent.WaitForProbesStatuses(probes.Length);
            Assert.Equal(probes.Length, statuses?.Length);

            await ApproveStatuses(statuses, testDescription, isMultiPhase: false, phaseNumber: 1);

            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            await ApproveSnapshots(snapshots, testDescription, isMultiPhase: false, phaseNumber: 1);
        }
        finally
        {
            await sample.StopSample();
        }
    }

#if NET462
    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ModuleUnloadInNetFramework462Test()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(ModuleUnloadTest));
        var guidGenerator = new DeterministicGuidGenerator();

        var probes = new[]
        {
            DebuggerTestHelper.CreateLogLineProbe(typeof(Samples.Probes.Unreferenced.External.ExternalTest), new LogLineProbeTestDataAttribute(lineNumber: 11), guidGenerator),
            DebuggerTestHelper.CreateLogLineProbe(typeof(Samples.Probes.Unreferenced.External.ExternalTest), new LogLineProbeTestDataAttribute(lineNumber: 12), guidGenerator),
        };

        var expectedNumberOfSnapshots = probes.Length;

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            await sample.RunCodeSample();

            Assert.True(await agent.WaitForNoSnapshots(), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");

            SetProbeConfiguration(agent, probes);

            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var statuses = await agent.WaitForProbesStatuses(probes.Length);
            Assert.Equal(probes.Length, statuses?.Length);

            await ApproveStatuses(statuses, testDescription, isMultiPhase: false, phaseNumber: 1);

            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            await ApproveSnapshots(snapshots, testDescription, isMultiPhase: false, phaseNumber: 1);
        }
        finally
        {
            await sample.StopSample();
        }
    }
#endif

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(ProbeTests))]
    public async Task MethodProbeTest(ProbeTestDescription testDescription)
    {
        SkipOverTestIfNeeded(testDescription);
        await RunMethodProbeTests(testDescription, true);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(SpanDecorationMemberData))]
    public async Task SpanDecorationTest(Type testType)
    {
        var method = testType.FullName + "[Annotate]";
        SetEnvironmentVariable("DD_TRACE_METHODS", method);
        var testDescription = DebuggerTestHelper.SpecificTestDescription(testType);
        await RunMethodProbeTests(testDescription, false);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    [Trait("Category", "LinuxUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task MethodProbeTest_NamedPipes()
    {
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
        }

        var testType = DebuggerTestHelper.FirstSupportedProbeTestType(EnvironmentHelper.GetTargetFramework());
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(AsyncGenericMethod));
        EnvironmentHelper.EnableWindowsNamedPipes();

        await RunMethodProbeTests(testDescription, false);
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "False")]
    [MemberData(nameof(UdsMemberData))]
    public async Task MethodProbeTest_UDS(Type type)
    {
        if (EnvironmentTools.IsWindows())
        {
            throw new SkipException("Can't use UDS on Windows");
        }

        var testType = DebuggerTestHelper.SpecificTestDescription(type);
        EnvironmentHelper.EnableUnixDomainSockets();
        await RunMethodProbeTests(testType, true);
    }

#endif

    private LogEntryWatcher CreateLogEntryWatcher()
    {
        string processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Probes";
        return new LogEntryWatcher($"dotnet-tracer-managed-{processName}*", LogDirectory);
    }

    private async Task RunMethodProbeTests(ProbeTestDescription testDescription, bool useStatsD)
    {
        var probes = GetProbeConfiguration(testDescription.TestType, false, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent(useStatsD: useStatsD);
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
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
        settings.UseFileName($"{nameof(ProbeTests)}.{testName}.{testNameSuffix}");

        var spans = agent.WaitForSpans(expectedSpanCount);

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
            var toSanitize = span.Tags.Where(tag => tag.Key.StartsWith(errorTagStartWith) && tag.Key.EndsWith(errorTagEndWith)).ToList();
            foreach (var keyValuePair in toSanitize)
            {
                span.Tags[keyValuePair.Key] = keyValuePair.Value.Substring(0, keyValuePair.Value.IndexOf(',')) + " }";
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
            settings.UseFileName($"{nameof(ProbeTests)}.{testName}.Spans");

            var spans = agent.WaitForSpans(spanProbes.Length, operationName: spanProbeOperationName);
            // Assert.Equal(spanProbes.Length, spans.Count);
            foreach (var span in spans)
            {
                var result = Result.FromSpan(span)
                                   .Properties(
                                        s => s
                                           .Matches(_ => (nameof(span.Name), span.Name), spanProbeOperationName))
                                   .Tags(
                                        s => s
                                            .Matches("component", "trace")
                                            .MatchesOneOf("debugger.probeid", Enumerable.Select<ProbeDefinition, string>(snapshotProbes, p => p.Id).ToArray()));
                // Assert.True(result.Success, result.ToString());
            }

            VerifierSettings.DerivePathInfo(
                (_, projectDirectory, _, _) => new(directory: Path.Combine(projectDirectory, "Approvals", "snapshots")));

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
            }
        }
    }

    /// <summary>
    /// Internal Jira Ticket: DEBUG-1092.
    /// </summary>
    private void SkipOverTestIfNeeded(ProbeTestDescription testDescription)
    {
        if (testDescription.TestType == typeof(LargeSnapshotTest) && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Should run only on Windows. Different approvals between Windows/Linux.");
        }

        if (testDescription.TestType == typeof(AsyncInstanceMethod) && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Should run only on Windows. Different approvals between Windows/Linux.");
        }

        if (testDescription.TestType == typeof(AsyncVoid) && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Should run only on Windows. Different approvals between Windows/Linux.");
        }

        if (!testDescription.IsOptimized && _unoptimizedNotSupportedTypes.Contains(testDescription.TestType))
        {
            throw new SkipException("Current test is not supported with unoptimized code.");
        }
    }

    private async Task RunSingleTestWithApprovals(ProbeTestDescription testDescription, int expectedNumberOfSnapshots, params ProbeDefinition[] probes)
    {
        using var agent = EnvironmentHelper.GetMockAgent();

        SetDebuggerEnvironment(agent);

        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, probes);

            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            await ApproveSnapshots(snapshots, testDescription, isMultiPhase: true, phaseNumber: 1);
            agent.ClearSnapshots();

            var statuses = await agent.WaitForProbesStatuses(probes.Length);
            Assert.Equal(probes.Length, statuses?.Length);
            await ApproveStatuses(statuses, testDescription, isMultiPhase: true, phaseNumber: 1);
            agent.ClearProbeStatuses();

            SetProbeConfiguration(agent, Array.Empty<LogProbe>());

            await logEntryWatcher.WaitForLogEntry(RemovedProbesInstrumentedLogEntry);
            Assert.True(await agent.WaitForNoSnapshots(6000), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");
        }
        finally
        {
            await sample.StopSample();
        }
    }

    private Task ApproveSnapshots(string[] snapshots, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        return Approver.ApproveSnapshots(snapshots, GetTestName(testDescription, isMultiPhase, phaseNumber), Output);
    }

    private Task ApproveStatuses(string[] statuses, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        return Approver.ApproveStatuses(statuses, GetTestName(testDescription, isMultiPhase, phaseNumber), Output);
    }

    private string GetTestName(ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        var testName = isMultiPhase ? $"{testDescription.TestType.Name}_#{phaseNumber}." : testDescription.TestType.Name;
        return $"{nameof(ProbeTests)}.{testName}";
    }

    private (ProbeAttributeBase ProbeTestData, ProbeDefinition Probe)[] GetProbeConfiguration(Type testType, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var probes = DebuggerTestHelper.GetAllProbes(testType, EnvironmentHelper.GetTargetFramework(), unlisted, guidGenerator);
        if (!probes.Any())
        {
            throw new SkipException($"No probes for {testType.Name}, skipping.");
        }

        return probes;
    }

    private void SetDebuggerEnvironment(MockTracerAgent agent)
    {
        SetEnvironmentVariable(ConfigurationKeys.ServiceName, EnvironmentHelper.SampleName);
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000");
        SetProbeConfiguration(agent, Array.Empty<LogProbe>());
    }

    private void SetProbeConfiguration(MockTracerAgent agent, ProbeDefinition[] snapshotProbes)
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
}
