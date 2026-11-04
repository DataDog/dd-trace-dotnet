// <copyright file="ProbesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using ProbeSampling = Datadog.Trace.Debugger.Configurations.Models.Sampling;

namespace Datadog.Trace.Debugger.IntegrationTests;

[CollectionDefinition(nameof(ProbesTests), DisableParallelization = true)]
[Collection(nameof(ProbesTests))]
[UsesVerify]
public class ProbesTests : ProbesTestsBase
{
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
            typeof(NotSupportedFailureTest),
            typeof(AsyncTryFinallyTest),
            typeof(ConditionAndTemplateChangeTest),
            typeof(AsyncNoHoistedLocal),
            typeof(AsyncMethodWithNotHoistedLocals),
            typeof(BaseLocalWithConcreteTypeInAsyncMethod),
            typeof(ManyLocals),
            typeof(AsyncTryCatchTest),
            typeof(UnboundProbeBecomesBoundTest),
#if NETFRAMEWORK
            typeof(ModuleUnloadTest)
#endif
    };

    public ProbesTests(ITestOutputHelper output)
        : base("Probes", nameof(ProbeTests), output)
    {
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

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task RedactionFromConfigurationTest()
    {
        var framework = EnvironmentHelper.GetTargetFramework();
        if (framework != "net9.0")
        {
            throw new SkipException("This test should run on net9.0 only.");
        }

        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(RedactionTest));
        const int expectedNumberOfSnapshots = 1;
        SkipOverTestIfNeeded(testDescription);
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

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ServiceNameChangedInCode_IsReflectedInSnapshots()
    {
        // We intentionally do NOT set DD_SERVICE via env var here
        // to validate that a runtime update via Tracer.Configure(...) is reflected in debugger snapshots.

        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(ChangeServiceNameInCodeTest));

        using var agent = EnvironmentHelper.GetMockAgent();

        // Minimal debugger environment setup without setting ConfigurationKeys.ServiceName
        SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "100");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DiagnosticsInterval, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxTimeToSerialize, "1000");
        SetProbeConfiguration(agent, Array.Empty<LogProbe>());

        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);

        try
        {
            var guidGenerator = new DeterministicGuidGenerator();
            var probes = new[]
            {
                DebuggerTestHelper.CreateDefaultLogProbe(nameof(ChangeServiceNameInCodeTest), nameof(ChangeServiceNameInCodeTest.BeforeServiceNameChange), guidGenerator: guidGenerator),
                DebuggerTestHelper.CreateDefaultLogProbe(nameof(ChangeServiceNameInCodeTest), nameof(ChangeServiceNameInCodeTest.AfterServiceNameChange), guidGenerator: guidGenerator),
            };
            var beforeProbeId = probes[0].Id;
            var afterProbeId = probes[1].Id;

            SetProbeConfiguration(agent, probes);
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            agent.ClearSnapshots();

            await sample.RunCodeSample();

            var snapshots = await agent.WaitForSnapshots(snapshotCount: 2, timeout: TimeSpan.FromSeconds(15));

            string beforeService = null;
            string afterService = null;

            foreach (var raw in snapshots)
            {
                var token = JToken.Parse(raw);
                var id = token.SelectToken("debugger.snapshot.probe.id")?.Value<string>();
                if (string.Equals(id, beforeProbeId, StringComparison.Ordinal))
                {
                    beforeService = token["service"]?.Value<string>();
                }
                else if (string.Equals(id, afterProbeId, StringComparison.Ordinal))
                {
                    afterService = token["service"]?.Value<string>();
                }
            }

            if (beforeService is null || afterService is null)
            {
                var details = snapshots.Select(s =>
                {
                    var t = JToken.Parse(s);
                    var pid = t.SelectToken("debugger.snapshot.probe.id")?.Value<string>();
                    var service = t["service"]?.Value<string>();
                    var loggerName = t.SelectToken("logger.name")?.Value<string>();
                    var loggerMethod = t.SelectToken("logger.method")?.Value<string>();
                    return $"probe.id={pid}, service={service}, logger.name={loggerName}, logger.method={loggerMethod}";
                });

                throw new InvalidOperationException(
                    $"Did not find expected probe ids in snapshots. Expected beforeProbeId={beforeProbeId}, afterProbeId={afterProbeId}. " +
                    $"Snapshots: {string.Join(" | ", details)}");
            }

            // baseline should be the app name (Samples.Probes)
            beforeService.Should().Be("samples.probes");
            afterService.Should().Be(ChangeServiceNameInCodeTest.UpdatedServiceName);
        }
        finally
        {
            await sample.StopSample();
        }
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

    [Fact(Skip = "FIXME: .NET 9 SDK causes different snapshots in .NET 7+")]
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

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task LineProbeEmit100SnapshotsTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(Emit100LineProbeSnapshotsTest));
        const int expectedNumberOfSnapshots = 100;
        SkipOverTestIfNeeded(testDescription);
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

    [SkippableFact(Skip = "Too flaky, 'Log file was not found for path' error")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task MoveFromSimpleLogToSnapshotLogTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(SimpleMethodWithLocalsAndArgsTest));
        SkipOverTestIfNeeded(testDescription);
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

    [SkippableFact(Skip = "Too flaky, 'Log file was not found for path' error")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task LineProbeUnboundProbeBecomesBoundTest()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(UnboundProbeBecomesBoundTest));
        SkipOverTestIfNeeded(testDescription);

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

            await logEntryWatcher.WaitForLogEntry($"Dynamic Instrumentation.CheckUnboundProbes: {expectedNumberOfSnapshots} unbound probes became bound.");

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

#if NETFRAMEWORK
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ModuleUnloadInNetFramework462Test()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(ModuleUnloadTest));
        SkipOverTestIfNeeded(testDescription);
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
    [Flaky("This test fails for various different reasons and needs investigating")]
    public async Task MethodProbeTest(ProbeTestDescription testDescription)
    {
        SkipOverTestIfNeeded(testDescription);
        await RunMethodProbeTests(testDescription, true);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData(typeof(CaptureExpressionsMethod))]
    [InlineData(typeof(CaptureExpressionsLine))]
    [InlineData(typeof(CaptureExpressionsComplex))]
    [InlineData(typeof(CaptureExpressionsMultipleProbes))]
    public async Task CaptureExpressionProbeTest(Type testType)
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(testType);
        await RunCaptureExpressionProbeTest(testDescription);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CoordinatedSnapshotProbesEmitCompleteChains()
    {
        const int ExpectedProbeCount = 3;
        const int TraceCount = 30;
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(CoordinatedSamplingTest));
        var probes = GetProbeConfiguration(testDescription.TestType, unlisted: true, new DeterministicGuidGenerator())
                    .Where(probe => probe.Probe.Where.MethodName is not null)
                    .Select(probe => (LogProbe)probe.Probe)
                    .ToArray();
        probes.Should().HaveCount(ExpectedProbeCount);

        foreach (var probe in probes)
        {
            probe.Sampling = new ProbeSampling { SnapshotsPerSecond = 1 };
        }

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, probes);
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);
            agent.ClearSnapshots();

            // Keep each trace alive long enough for the requests to span multiple one-second sampler
            // windows. A tight loop would pass before coordination because every sampler starts with
            // the same full budget.
            for (var i = 0; i < TraceCount; i++)
            {
                await sample.RunCodeSample();
            }

            await Task.Delay(TimeSpan.FromSeconds(3));

            var expectedProbeIds = probes.Select(probe => probe.Id).ToArray();
            var snapshotsByTrace = agent.Snapshots
                                        .Select(JToken.Parse)
                                        .GroupBy(snapshot => snapshot["dd.trace_id"]?.Value<string>())
                                        .ToArray();

            snapshotsByTrace.Should().NotBeEmpty();
            snapshotsByTrace.Length.Should().BeLessThan(TraceCount);
            foreach (var traceSnapshots in snapshotsByTrace)
            {
                traceSnapshots.Key.Should().NotBeNullOrEmpty();
                traceSnapshots.Select(snapshot => snapshot.SelectToken("debugger.snapshot.probe.id")?.Value<string>())
                              .Should()
                              .BeEquivalentTo(expectedProbeIds);
            }
        }
        finally
        {
            await sample.StopSample();
        }
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task CoordinatedLineProbesEmitOncePerTrace()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(CoordinatedSamplingTest));
        var probes = GetProbeConfiguration(testDescription.TestType, unlisted: true, new DeterministicGuidGenerator())
                    .Where(probe => probe.Probe.Where.MethodName is null)
                    .Select(probe => (LogProbe)probe.Probe)
                    .ToArray();
        probes.Should().HaveCount(2);

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, probes);
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);
            agent.ClearSnapshots();

            await sample.RunCodeSample();

            await agent.WaitForSnapshots(probes.Length, TimeSpan.FromSeconds(15));
            await Task.Delay(TimeSpan.FromSeconds(3));

            var snapshots = agent.Snapshots.ToArray();
            snapshots.Should().HaveCount(probes.Length);
            snapshots.Select(JToken.Parse)
                     .Select(snapshot => snapshot.SelectToken("debugger.snapshot.probe.id")?.Value<string>())
                     .Should()
                     .BeEquivalentTo(probes.Select(probe => probe.Id));
        }
        finally
        {
            await sample.StopSample();
        }
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task ConditionErrorFullSnapshotOmitsCaptureData()
    {
        var testDescription = DebuggerTestHelper.SpecificTestDescription(typeof(FullSnapshotWithConditionError));
        var probes = GetProbeConfiguration(testDescription.TestType, unlisted: true, new DeterministicGuidGenerator());
        var snapshotProbes = probes.Select(p => p.Probe).ToArray();

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher(nameof(ConditionErrorFullSnapshotOmitsCaptureData));
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, snapshotProbes);
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var snapshots = await agent.WaitForSnapshots(snapshotProbes.Length);
            Assert.Equal(snapshotProbes.Length, snapshots?.Length);

            var snapshot = JToken.Parse(snapshots[0]);
            var evaluationErrors = snapshot.SelectToken("debugger.snapshot.evaluationErrors") as JArray;
            evaluationErrors.Should().NotBeNull();
            evaluationErrors.Should().ContainSingle();
            evaluationErrors![0]["expr"]?.Value<string>().Should().Be("this.undefined");
            evaluationErrors[0]["message"]?.Value<string>().Should().NotBeNullOrEmpty();
            snapshot.SelectToken("debugger.snapshot.captures.entry").Should().BeNull();
            snapshot.SelectToken("debugger.snapshot.captures.return").Should().BeNull();
            snapshot.SelectToken("debugger.snapshot.captures.lines").Should().BeNull();

            await ApproveSnapshots(snapshots, testDescription, isMultiPhase: false, phaseNumber: 1);
        }
        finally
        {
            await sample.StopSample();
        }
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(SpanDecorationMemberData))]
    [Flaky("Identified as flaky in error tracking. Marked as flaky until solved.")]
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
    [Flaky("Named pipes is flaky", maxRetries: 3)]
    public async Task MethodProbeTest_NamedPipes()
    {
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("WindowsNamedPipe transport is only supported on Windows");
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

    /// <summary>
    /// Internal Jira Ticket: DEBUG-1092.
    /// </summary>
    private void SkipOverTestIfNeeded(ProbeTestDescription testDescription)
    {
        if (testDescription.TestType == typeof(LargeSnapshotTest)
          || testDescription.TestType == typeof(NotSupportedFailureTest)
          || testDescription.TestType == typeof(AsyncLineProbeWithFieldsArgsAndLocalsTest)
          || testDescription.TestType == typeof(AsyncCallChain))
        {
            throw new SkipException("Inconsistent approvals.");
        }

        if (testDescription.TestType == typeof(AsyncWithGenericArgumentAndLocal)
         || testDescription.TestType == typeof(EmptyCtorTest)
         || testDescription.TestType == typeof(AsyncGenericClass)
         || testDescription.TestType == typeof(AsyncGenericMethodWithLineProbeTest)
         || testDescription.TestType == typeof(AsyncGenericStruct)
         || testDescription.TestType == typeof(NonSupportedInstrumentationTest)
         || testDescription.TestType == typeof(Emit100LineProbeSnapshotsTest)
         || testDescription.TestType == typeof(NonEmptyCtorTest)
         || testDescription.TestType == typeof(RedactionTest))
        {
            throw new SkipException("Probe status not found or log entry not found.");
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

#if NETFRAMEWORK
        if (testDescription.TestType == typeof(ModuleUnloadTest) && testDescription.IsOptimized)
        {
            throw new SkipException("Current test is not supported with optimized code.");
        }
#else
        if (testDescription.TestType == typeof(NullByRefArgAndLocal))
        {
            throw new SkipException("DEBUG-4875, DEBUG-4876");
        }
#endif
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

    private async Task RunCaptureExpressionProbeTest(ProbeTestDescription testDescription)
    {
        var probes = GetProbeConfiguration(testDescription.TestType, false, new DeterministicGuidGenerator());
        var snapshotProbes = probes.Select(p => p.Probe).ToArray();

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher($"capture_expressions_{testDescription.TestType.Name}");
        using var sample = await DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
        try
        {
            SetProbeConfiguration(agent, snapshotProbes);
            await logEntryWatcher.WaitForLogEntry(AddedProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var snapshots = await agent.WaitForSnapshots(snapshotProbes.Length);
            Assert.Equal(snapshotProbes.Length, snapshots?.Length);
            var captureExpressionNamesBySnapshot = new List<string[]>();
            foreach (var snapshot in snapshots)
            {
                var token = JToken.Parse(snapshot);
                var captureExpressionNames = GetCaptureExpressionNames(token);
                captureExpressionNames.Should().NotBeEmpty();
                captureExpressionNamesBySnapshot.Add(captureExpressionNames);
                token.SelectToken("debugger.snapshot.captures.return.locals").Should().BeNull();
                token.SelectToken("debugger.snapshot.captures.return.arguments").Should().BeNull();
            }

            if (snapshotProbes.Length > 1)
            {
                captureExpressionNamesBySnapshot
                   .Select(names => string.Join(",", names))
                   .Should()
                   .OnlyHaveUniqueItems();
            }

            await ApproveSnapshots(snapshots, testDescription, isMultiPhase: false, phaseNumber: 1);
        }
        finally
        {
            await sample.StopSample();
        }

        static string[] GetCaptureExpressionNames(JToken token)
        {
            return token
                  .SelectTokens("$..captureExpressions")
                  .SelectMany(captureExpressions => captureExpressions.Children<JProperty>())
                  .Select(property => property.Name)
                  .OrderBy(name => name)
                  .ToArray();
        }
    }
}
