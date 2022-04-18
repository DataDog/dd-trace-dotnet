// <copyright file="ProbesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.PDBs;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Samples.Probes;
using Samples.Probes.SmokeTests;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger;

[CollectionDefinition(nameof(ProbesTests), DisableParallelization = true)]
[Collection(nameof(ProbesTests))]
[UsesVerify]
public class ProbesTests : TestHelper
{
    private const string ProbesDefinitionFileName = "probes_definition.json";
    private readonly string[] _typesToScrub = { nameof(IntPtr), nameof(Guid) };
    private readonly string[] _knownPropertiesToReplace = { "duration", "timestamp", "dd.span_id", "dd.trace_id", "id", "lineNumber" };

    public ProbesTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> ProbeTests()
    {
        return typeof(IRun).Assembly.GetTypes()
                           .Where(t => t.GetInterface(nameof(IRun)) != null)
                           .Select(t => new object[] { t });
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void LineProbeEmit100SnapshotsTest()
    {
        var testType = typeof(Emit100LineProbeSnapshotsTest);
        const int expectedNumberOfSnapshots = 100;

        var probes = DebuggerTestHelper.GetAllProbes(testType, EnvironmentHelper.GetTargetFramework(), unlisted: true);

        if (!probes.Any())
        {
            throw new SkipException($"Definition for {testType.Name} is null, skipping.");
        }

        var definition = DebuggerTestHelper.CreateProbeDefinition(probes.Select(p => p.Probe).ToArray());

        if (definition == null)
        {
            return;
        }

        var snapshots = ExecuteTestWithBasicAssertions(testType, definition, numberOfSnapshots: expectedNumberOfSnapshots);
        Assert.True(snapshots.All(IsSaneSnapshot), "Not all snapshots are sane.");
        Assert.True(snapshots.Distinct().Count() == snapshots.Length, "All snapshots should be unique.");

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
    public async Task MethodProbeTest(Type testType)
    {
        var probes = DebuggerTestHelper.GetAllProbes(testType, EnvironmentHelper.GetTargetFramework(), unlisted: false);

        if (!probes.Any())
        {
            throw new SkipException($"Definition for {testType.Name} is null, skipping.");
        }

        var firstPhase = probes.First().ProbeTestData.Phase;
        if (probes.All(p => p.ProbeTestData.Phase == firstPhase))
        {
            await PerformSinglePhaseProbeTest(testType, probes);
        }
        else
        {
            await PerformMultiPhasesProbeTest(testType, probes);
        }
    }

    private async Task PerformSinglePhaseProbeTest(Type testType, (ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)[] probes)
    {
        var definition = DebuggerTestHelper.CreateProbeDefinition(probes.Select(p => p.Probe).ToArray());
        var expectedNumberOfSnapshots = DebuggerTestHelper.CalculateExpectedNumberOfSnapshots(probes.Select(p => p.ProbeTestData).ToArray());

        var snapshots = ExecuteTestWithBasicAssertions(testType, definition, numberOfSnapshots: expectedNumberOfSnapshots);
        await ApproveSnapshots(testType, snapshots, isMultiPhase: false);
    }

    private async Task PerformMultiPhasesProbeTest(Type testType, (ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)[] probes)
    {
        var phaseNumber = 1;
        var groupedPhases = probes
                           .GroupBy(p => p.ProbeTestData.Phase)
                           .Select(group => new { Group = group.Key, Probes = group })
                           .OrderBy(group => group.Group)
                           .ToArray();
        var firstGroup = groupedPhases.First();
        var definition = DebuggerTestHelper.CreateProbeDefinition(firstGroup.Probes.Select(p => p.Probe).ToArray());
        using var agent = PrepareDefinitionForTest(definition);

        // Spawn the test process
        var process = StartSample(agent, $"{testType.FullName} {groupedPhases.Length}", string.Empty, aspNetCorePort: 5000);
        using var helper = new ProcessHelper(process);

        var probesTestData = firstGroup.Probes.Select(p => p.ProbeTestData).ToArray();
        await RunPhase(agent, testType, phaseNumber, probesTestData);

        foreach (var groupedPhase in groupedPhases.Skip(1))
        {
            phaseNumber++;
            definition = DebuggerTestHelper.CreateProbeDefinition(groupedPhase.Probes.Select(p => p.Probe).ToArray());
            WriteProbeDefinitionToFile(definition);

            probesTestData = groupedPhase.Probes.Select(p => p.ProbeTestData).ToArray();
            await RunPhase(agent, testType, phaseNumber, probesTestData);
        }
    }

    private async Task RunPhase(MockTracerAgent agent, Type testType, int phaseNumber, ProbeAttributeBase[] probes)
    {
        string[] snapshots;
        var expectedNumberOfSnapshots = DebuggerTestHelper.CalculateExpectedNumberOfSnapshots(probes);
        if (expectedNumberOfSnapshots == 0)
        {
            Assert.True(agent.NoSnapshots(), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");
            snapshots = Array.Empty<string>();
        }
        else
        {
            snapshots = agent.WaitForSnapshots(expectedNumberOfSnapshots).ToArray();
        }

        AssertSnapshots(snapshots, expectedNumberOfSnapshots);
        await ApproveSnapshots(testType, snapshots, isMultiPhase: true, phaseNumber);
        agent.ClearSnapshots();
    }

    private MockTracerAgent PrepareDefinitionForTest(ProbeConfiguration definition)
    {
        var path = WriteProbeDefinitionToFile(definition);
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ProbeFile, path);
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DebuggerEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
        int httpPort = TcpPortProvider.GetOpenPort();
        Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

        var agent = EnvironmentHelper.GetMockAgent();
        SetEnvironmentVariable(ConfigurationKeys.AgentPort, agent.Port.ToString());
        agent.ShouldDeserializeTraces = false;
        return agent;
    }

    private string[] ExecuteTestWithBasicAssertions(Type testType, ProbeConfiguration definition, int numberOfSnapshots = 1)
    {
        using var agent = PrepareDefinitionForTest(definition);
        using var processResult = RunSampleAndWaitForExit(agent, arguments: testType.FullName);

        var snapshots = agent.WaitForSnapshots(numberOfSnapshots).ToArray();
        AssertSnapshots(snapshots, numberOfSnapshots);

        return snapshots;
    }

    private void AssertSnapshots(string[] snapshots, int expectedNumberOfSnapshots)
    {
        Assert.NotNull(snapshots);
        Assert.Equal(expectedNumberOfSnapshots, snapshots.Length);
    }

    private async Task ApproveSnapshots(Type testType, string[] snapshots, bool isMultiPhase, int phase = 1)
    {
        if (snapshots.Length > 1)
        {
            // Order the snapshots alphabetically so we'll be able to create deterministic approvals
            snapshots = snapshots.OrderBy(snapshot => snapshot).ToArray();
        }

        for (var snapshotIndex = 0; snapshotIndex < snapshots.Length; snapshotIndex++)
        {
            var settings = new VerifySettings();

            var phaseText = isMultiPhase ? $"{phase}." : string.Empty;
            var trailingSnapshotText = snapshots.Length > 1 || isMultiPhase ? $"#{phaseText}{snapshotIndex + 1}" : string.Empty;
            settings.UseParameters(testType + trailingSnapshotText);

            settings.ScrubEmptyLines();
            settings.AddScrubber(ScrubSnapshotJson);

            VerifierSettings.DerivePathInfo(
                (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "snapshots")));

            string toVerify = string.Join(Environment.NewLine, JsonUtility.NormalizeJsonString(snapshots[snapshotIndex]));
            await Verifier.Verify(NormalizeLineEndings(toVerify), settings);
        }
    }

    private string NormalizeLineEndings(string text) =>
        text
           .Replace(@"\r\n", @"\n")
           .Replace(@"\n\r", @"\n")
           .Replace(@"\r", @"\n")
           .Replace(@"\n", @"\r\n");

    private void ScrubSnapshotJson(StringBuilder input)
    {
        var json = JObject.Parse(input.ToString());

        var toRemove =  new List<JToken>();
        foreach (var descendant in json.DescendantsAndSelf().OfType<JObject>())
        {
            foreach (var item in descendant)
            {
                if (_knownPropertiesToReplace.Contains(item.Key) && item.Value != null)
                {
                    item.Value.Replace(JToken.FromObject("ScrubbedValue"));
                }

                // Sanitizes types whose values may vary from run to run and consequently produce a different approval file.
                if (item.Key == "type" && _typesToScrub.Contains(item.Value.ToString()))
                {
                    item.Value.Parent.Parent["value"].Replace("ScrubbedValue");
                }

                // Scrub MoveNext methods from `stack` in the snapshot as it varies between Windows/Linux.
                if (item.Key == "function" && item.Value.ToString().Contains("MoveNext"))
                {
                    item.Value.Replace(string.Empty);
                }

                // Remove the full path of file names
                if (item.Key == "fileName" || item.Key == "file")
                {
                    item.Value.Replace(Path.GetFileName(item.Value.ToString()));
                }

                // Remove stackframes from "System" namespace, or where the frame was not resolved to a method
                if (item.Key == "function" && (item.Value.ToString().StartsWith("System") || item.Value.ToString() == string.Empty))
                {
                    toRemove.Add(item.Value.Parent.Parent);
                }
            }
        }

        foreach (var itemToRemove in toRemove)
        {
            itemToRemove.Remove();
        }

        input.Clear().Append(json);
    }

    private string WriteProbeDefinitionToFile(ProbeConfiguration probeConfiguration)
    {
        var json = JsonConvert.SerializeObject(probeConfiguration, Formatting.Indented);
        // We are not using a temp file here, but rather writing it directly to the debugger sample project,
        // so that if a test fails, we will be able to simply hit F5 to debug the same probe
        // configuration (launchsettings.json references the same file).
        var path = Path.Combine(EnvironmentHelper.GetSampleProjectDirectory(), ProbesDefinitionFileName);
        File.WriteAllText(path, json);
        return path;
    }
}
