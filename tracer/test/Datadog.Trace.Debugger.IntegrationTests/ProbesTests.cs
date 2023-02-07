// <copyright file="ProbesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
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
            typeof(AsyncLineProbeWithFieldsArgsAndLocalsTest),
            typeof(AsyncMethodInsideTaskRun),
            typeof(AsyncRecursiveCall),
            typeof(AsyncStaticMethod),
            typeof(AsyncThrowException),
            typeof(AsyncTemplateLocalExitFullSnapshot),
            typeof(AsyncVoid),
            typeof(AsyncWithGenericArgumentAndLocal),
            typeof(AsyncTemplateArgExitFullSnapshot),
            typeof(HasLocalsAndReturnValue),
            typeof(MultipleLineProbes),
            typeof(MultiScopesWithSameLocalNameTest),
            typeof(NotSupportedFailureTest),
            typeof(AsyncTaskReturnTest),
            typeof(AsyncTaskReturnWithExceptionTest)
    };

    private readonly string[] _typesToScrub = { nameof(IntPtr), nameof(Guid) };
    private readonly string[] _knownPropertiesToReplace = { "duration", "timestamp", "dd.span_id", "dd.trace_id", "id", "lineNumber", "thread_name", "thread_id", "<>t__builder", "s_taskIdCounter", "<>u__1", "stack" };

    public ProbesTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
        EnableDebugMode();
    }

    public static IEnumerable<object[]> ProbeTests()
    {
        return DebuggerTestHelper.AllTestDescriptions();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task AsyncMethodInGenericClassTest()
    {
        Skip.If(true, "Not supported yet. Internal Jira Ticket: #DEBUG-1092.");

        var testDescription = DebuggerTestHelper.SpecificTestDescription<AsyncMethodInGenericClassTest>();
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
        var testDescription = DebuggerTestHelper.SpecificTestDescription<CtorTransparentCodeTest>();
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
        var testDescription = DebuggerTestHelper.SpecificTestDescription<OverloadAndSimpleNameTest>();
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
        var testDescription = DebuggerTestHelper.SpecificTestDescription<Emit100LineProbeSnapshotsTest>();
        const int expectedNumberOfSnapshots = 100;

        var probes = GetProbeConfiguration(testDescription.TestType, true, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
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

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(ProbeTests))]
    public async Task MethodProbeTest(ProbeTestDescription testDescription)
    {
        SkipOverTestIfNeeded(testDescription);
        await RunMethodProbeTests(testDescription);
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
        var testDescription = DebuggerTestHelper.SpecificTestDescription<AsyncGenericMethod>();
        EnvironmentHelper.EnableWindowsNamedPipes();

        await RunMethodProbeTests(testDescription);
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task MethodProbeTest_UDS()
    {
        var testType = DebuggerTestHelper.SpecificTestDescription<AsyncGenericMethod>();
        EnvironmentHelper.EnableUnixDomainSockets();

        await RunMethodProbeTests(testType);
    }

#endif

    private static LogEntryWatcher CreateLogEntryWatcher()
    {
        string processName = EnvironmentHelper.IsCoreClr() ? "dotnet" : "Samples.Probes";
        return new LogEntryWatcher($"dotnet-tracer-managed-{processName}*");
    }

    private async Task RunMethodProbeTests(ProbeTestDescription testDescription)
    {
        var probes = GetProbeConfiguration(testDescription.TestType, false, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
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

            async Task RunPhase(LogProbe[] snapshotProbes, ProbeAttributeBase[] probeData, bool isMultiPhase = false, int phaseNumber = 1)
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

                var expectedNumberOfSnapshots = DebuggerTestHelper.CalculateExpectedNumberOfSnapshots(probeData);
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

                // The Datadog-Agent is continuously receiving probe statuses.
                // We may have outdated probe statuses that were sent before the instrumentation took place.
                // To ensure consistency, we are clearing the probe statuses and requesting a fresh batch.
                // This will ensure that the next set of probe statuses received will be up-to-date and accurate.
                agent.ClearProbeStatuses();
                var statuses = await agent.WaitForProbesStatuses(probeData.Length);

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

    /// <summary>
    /// Internal Jira Ticket: DEBUG-1092.
    /// </summary>
    private void SkipOverTestIfNeeded(ProbeTestDescription testDescription)
    {
        if (testDescription.TestType == typeof(AsyncInstanceMethod) && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
        }

        if (!testDescription.IsOptimized && _unoptimizedNotSupportedTypes.Contains(testDescription.TestType))
        {
            throw new SkipException("Current test is not supported with unoptimized code.");
        }
    }

    private async Task RunSingleTestWithApprovals(ProbeTestDescription testDescription, int expectedNumberOfSnapshots, params LogProbe[] probes)
    {
        using var agent = EnvironmentHelper.GetMockAgent();

        SetDebuggerEnvironment(agent);

        using var logEntryWatcher = CreateLogEntryWatcher();
        using var sample = DebuggerTestHelper.StartSample(this, agent, testDescription.TestType.FullName);
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

    private async Task ApproveSnapshots(string[] snapshots, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        await ApproveOnDisk(snapshots, testDescription, isMultiPhase, phaseNumber, "snapshots");
    }

    private async Task ApproveStatuses(string[] statuses, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber)
    {
        await ApproveOnDisk(statuses, testDescription, isMultiPhase, phaseNumber, "statuses");
    }

    private async Task ApproveOnDisk(string[] dataToApprove, ProbeTestDescription testDescription, bool isMultiPhase, int phaseNumber, string path)
    {
        if (dataToApprove.Length > 1)
        {
            // Order the snapshots alphabetically so we'll be able to create deterministic approvals
            dataToApprove = dataToApprove.OrderBy(snapshot => snapshot).ToArray();
        }

        var settings = new VerifySettings();

        var testName = isMultiPhase ? $"{testDescription.TestType.Name}_#{phaseNumber}." : testDescription.TestType.Name;
        settings.UseFileName($"{nameof(ProbeTests)}.{testName}");
        settings.DisableRequireUniquePrefix();
        settings.ScrubEmptyLines();
        settings.AddScrubber(ScrubSnapshotJson);

        VerifierSettings.DerivePathInfo(
            (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "Approvals", path)));

        var toVerify =
            "["
           +
            string.Join(
                ",",
                dataToApprove.Select(JsonUtility.NormalizeJsonString))
           +
            "]";

        await Verifier.Verify(NormalizeLineEndings(toVerify), settings);

        void ScrubSnapshotJson(StringBuilder input)
        {
            var json = JArray.Parse(input.ToString());

            var toRemove = new List<JToken>();
            foreach (var descendant in json.DescendantsAndSelf().OfType<JObject>())
            {
                foreach (var item in descendant)
                {
                    try
                    {
                        if (_knownPropertiesToReplace.Contains(item.Key) && item.Value != null)
                        {
                            item.Value.Replace(JToken.FromObject("ScrubbedValue"));
                            continue;
                        }

                        var value = item.Value.ToString();
                        switch (item.Key)
                        {
                            case "type":
                                // Sanitizes types whose values may vary from run to run and consequently produce a different approval file.
                                if (_typesToScrub.Contains(item.Value.ToString()))
                                {
                                    item.Value.Parent.Parent["value"].Replace("ScrubbedValue");
                                }

                                break;
                            case "function":

                                // Remove stackframes from "System" namespace, or where the frame was not resolved to a method
                                if (value.StartsWith("System") || value == string.Empty)
                                {
                                    toRemove.Add(item.Value.Parent.Parent);
                                    continue;
                                }

                                // Scrub MoveNext methods from `stack` in the snapshot as it varies between Windows/Linux.
                                if (value.Contains(".MoveNext"))
                                {
                                    item.Value.Replace(string.Empty);
                                }

                                // Scrub generated DisplayClass from stack in the snapshot as it varies between .net frameworks
                                if (value.Contains("<>c__DisplayClass"))
                                {
                                    item.Value.Replace(string.Empty);
                                }

                                break;
                            case "fileName":
                            case "file":
                                // Remove the full path of file names
                                item.Value.Replace(Path.GetFileName(value));

                                break;

                            case "message":
                                if (!value.Contains("Installed probe ") && !value.Contains("Error installing probe ") &&
                                    !IsParentName(item, parentName: "throwable") &&
                                    !IsParentName(item, parentName: "exception"))
                                {
                                    // remove snapshot message (not probe status)
                                    item.Value.Replace("ScrubbedValue");
                                }

                                break;

                            case "stacktrace":
                                if (IsParentName(item, parentName: "throwable"))
                                {
                                    // take only the first frame of the exception stacktrace
                                    var firstChild = item.Value.Children().FirstOrDefault();
                                    if (firstChild != null)
                                    {
                                        item.Value.Replace(new JArray(firstChild));
                                    }
                                }

                                break;
                        }
                    }
                    catch (Exception)
                    {
                        Output.WriteLine($"Failed to sanitize snapshot. The part we are trying to sanitize: {item}");
                        Output.WriteLine($"Complete snapshot: {json}");

                        throw;
                    }

                    static bool IsParentName(KeyValuePair<string, JToken> item, string parentName)
                    {
                        return item.Value.Path.Substring(0, item.Value.Path.Length - $".{item.Key}".Length).EndsWith(parentName);
                    }
                }
            }

            foreach (var itemToRemove in toRemove)
            {
                itemToRemove.Remove();
            }

            input.Clear().Append(json);
        }

        string NormalizeLineEndings(string text) =>
            text
               .Replace(@"\r\n", @"\n")
               .Replace(@"\n\r", @"\n")
               .Replace(@"\r", @"\n")
               .Replace(@"\n", @"\r\n");
    }

    private (ProbeAttributeBase ProbeTestData, LogProbe Probe)[] GetProbeConfiguration(Type testType, bool unlisted, DeterministicGuidGenerator guidGenerator)
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

    private void SetProbeConfiguration(MockTracerAgent agent, LogProbe[] snapshotProbes)
    {
        var configurations = snapshotProbes
            .Select(snapshotProbe => (snapshotProbe, $"{DefinitionPaths.LogProbe}{snapshotProbe.Id}"))
            .Select(dummy => ((object Config, string Id))dummy);

        agent.SetupRcm(Output, configurations, LiveDebuggerProduct.ProductName);
    }
}
