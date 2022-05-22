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
using Datadog.Trace.TestHelpers;
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
    private const string LogFileNamePrefix = "dotnet-tracer-managed-";
    private const string ProbesInstrumentedLogEntry = "Live Debugger.InstrumentProbes: Request to instrument probes definitions completed.";
    private const string ProbesDefinitionFileName = "probes_definition.json";
    private readonly string[] _typesToScrub = { nameof(IntPtr), nameof(Guid) };
    private readonly string[] _knownPropertiesToReplace = { "duration", "timestamp", "dd.span_id", "dd.trace_id", "id", "lineNumber", "thread_name", "thread_id" };

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
    public async Task LineProbeEmit100SnapshotsTest()
    {
        var testType = typeof(Emit100LineProbeSnapshotsTest);
        const int expectedNumberOfSnapshots = 100;

        var probes = GetProbeConfiguration(testType, true);
        var agent = GetMockAgent();

        SetDebuggerEnvironment();
        using var sample = DebuggerTestHelper.StartSample(this, agent, testType.FullName);
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{sample.Process.ProcessName}*");

        SetProbeConfiguration(DebuggerTestHelper.CreateProbeDefinition(probes.Select(p => p.Probe).ToArray()));
        await logEntryWatcher.WaitForLogEntry(ProbesInstrumentedLogEntry);

        await sample.RunCodeSample();
        try
        {
            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            AssertSnapshots(snapshots, expectedNumberOfSnapshots);
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
    public async Task MethodProbeTest(Type testType)
    {
        var probes = GetProbeConfiguration(testType, false);
        var agent = GetMockAgent();

        SetDebuggerEnvironment();
        using var sample = DebuggerTestHelper.StartSample(this, agent, testType.FullName);
        using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{sample.Process.ProcessName}*");

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
        }
        finally
        {
            await sample.StopSample();
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

        async Task RunPhase(SnapshotProbe[] snapshotProbes, ProbeAttributeBase[] probeData, bool isMultiPhase = false, int phaseNumber = 1)
        {
            var definition = DebuggerTestHelper.CreateProbeDefinition(snapshotProbes);
            SetProbeConfiguration(definition);
            await logEntryWatcher.WaitForLogEntry(ProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var expectedNumberOfSnapshots = DebuggerTestHelper.CalculateExpectedNumberOfSnapshots(probeData);
            string[] snapshots;
            if (expectedNumberOfSnapshots == 0)
            {
                Assert.True(await agent.WaitForNoSnapshots(), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");
                snapshots = Array.Empty<string>();
            }
            else
            {
                snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            }

            AssertSnapshots(snapshots, expectedNumberOfSnapshots);
            await ApproveSnapshots();
            agent.ClearSnapshots();

            async Task ApproveSnapshots()
            {
                if (snapshots.Length > 1)
                {
                    // Order the snapshots alphabetically so we'll be able to create deterministic approvals
                    snapshots = snapshots.OrderBy(snapshot => snapshot).ToArray();
                }

                for (var snapshotIndex = 0; snapshotIndex < snapshots.Length; snapshotIndex++)
                {
                    var settings = new VerifySettings();

                    var phaseText = isMultiPhase ? $"{phaseNumber}." : string.Empty;
                    var trailingSnapshotText = snapshots.Length > 1 || isMultiPhase ? $"#{phaseText}{snapshotIndex + 1}" : string.Empty;
                    settings.UseParameters(testType + trailingSnapshotText);

                    settings.ScrubEmptyLines();
                    settings.AddScrubber(ScrubSnapshotJson);

                    VerifierSettings.DerivePathInfo(
                        (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "snapshots")));

                    var toVerify = string.Join(Environment.NewLine, JsonUtility.NormalizeJsonString(snapshots[snapshotIndex]));
                    await Verifier.Verify(NormalizeLineEndings(toVerify), settings);
                }

                void ScrubSnapshotJson(StringBuilder input)
                {
                    var json = JObject.Parse(input.ToString());

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
                                        if (item.Key == "function" && value.Contains(".MoveNext"))
                                        {
                                            item.Value.Replace(string.Empty);
                                        }

                                        break;
                                    case "fileName":
                                    case "file":
                                        // Remove the full path of file names
                                        item.Value.Replace(Path.GetFileName(value));

                                        break;
                                }
                            }
                            catch (Exception)
                            {
                                Output.WriteLine($"Failed to sanitize snapshot. The part we are trying to sanitize: {item}");
                                Output.WriteLine($"Complete snapshot: {json}");

                                throw;
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
        }
    }

    private (ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)[] GetProbeConfiguration(Type testType, bool unlisted)
    {
        var probes = DebuggerTestHelper.GetAllProbes(testType, EnvironmentHelper.GetTargetFramework(), unlisted);
        if (!probes.Any())
        {
            throw new SkipException($"No probes for {testType.Name}, skipping.");
        }

        return probes;
    }

    private void SetDebuggerEnvironment()
    {
        var probeConfiguration = DebuggerTestHelper.CreateProbeDefinition(Array.Empty<SnapshotProbe>());
        var path = SetProbeConfiguration(probeConfiguration);

        SetEnvironmentVariable(ConfigurationKeys.Debugger.ProbeFile, path);
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DebuggerEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
    }

    private string SetProbeConfiguration(ProbeConfiguration probeConfiguration)
    {
        var json = JsonConvert.SerializeObject(probeConfiguration, Formatting.Indented);

        // We are not using a temp file here, but rather writing it directly to the debugger sample project,
        // so that if a test fails, we will be able to simply hit F5 to debug the same probe
        // configuration (launchsettings.json references the same file).
        var path = Path.Combine(EnvironmentHelper.GetSampleProjectDirectory(), ProbesDefinitionFileName);
        File.WriteAllText(path, json);
        return path;
    }

    private MockTracerAgent GetMockAgent()
    {
        var agent = EnvironmentHelper.GetMockAgent();
        SetEnvironmentVariable(ConfigurationKeys.AgentPort, agent.Port.ToString());
        agent.ShouldDeserializeTraces = false;
        return agent;
    }

    private void AssertSnapshots(string[] snapshots, int expectedNumberOfSnapshots)
    {
        Assert.NotNull(snapshots);
        Assert.Equal(expectedNumberOfSnapshots, snapshots.Length);
    }
}
