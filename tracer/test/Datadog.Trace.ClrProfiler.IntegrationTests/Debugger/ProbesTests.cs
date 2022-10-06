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
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Samples.Probes;
using Samples.Probes.SmokeTests;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using Target = Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf.Target;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger;

[CollectionDefinition(nameof(ProbesTests), DisableParallelization = true)]
[Collection(nameof(ProbesTests))]
[UsesVerify]
public class ProbesTests : TestHelper, IDisposable
{
    private const string LogFileNamePrefix = "dotnet-tracer-managed-";
    private const string ProbesInstrumentedLogEntry = "Live Debugger.InstrumentProbes: Request to instrument probes definitions completed.";

    private readonly string[] _typesToScrub = { nameof(IntPtr), nameof(Guid) };
    private readonly string[] _knownPropertiesToReplace = { "duration", "timestamp", "dd.span_id", "dd.trace_id", "id", "lineNumber", "thread_name", "thread_id", "<>t__builder", "s_taskIdCounter", "<>u__1" };

    public ProbesTests(ITestOutputHelper output)
        : base("Probes", Path.Combine("test", "test-applications", "debugger"), output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> ProbeTests()
    {
        return DebuggerTestHelper.AllProbeTestTypes();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task AsyncMethodInGenericClassTest()
    {
        Skip.If(true, "Not supported yet. Internal Jira Ticket: #DEBUG-1092.");

        var testType = typeof(AsyncMethodInGenericClassTest);
        const int expectedNumberOfSnapshots = 1;

        var guidGenerator = new DeterministicGuidGenerator();

        var probes = new[]
        {
            CreateProbe("GenericClass`1", "Run", guidGenerator)
        };

        await RunSingleTestWithApprovals(testType, isMultiPhase: false, expectedNumberOfSnapshots, probes);
    }

    [SkippableFact(Skip = "Too flakey")]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task TransparentCodeCtorInstrumentationTest()
    {
        var testType = typeof(CtorTransparentCodeTest);
        const int expectedNumberOfSnapshots = 1;

        var guidGenerator = new DeterministicGuidGenerator();

        var probes = new[]
        {
            CreateProbe("SecurityTransparentTest", ".ctor", guidGenerator),
            CreateProbe("CtorTransparentCodeTest", "Run", guidGenerator)
        };

        await RunSingleTestWithApprovals(testType, isMultiPhase: false, expectedNumberOfSnapshots, probes);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData(typeof(OverloadAndSimpleNameTest))]
    public async Task InstallAndUninstallMethodProbeWithOverloadsTest(Type testType)
    {
        const int expectedNumberOfSnapshots = 9;

        var probes = GetProbeConfiguration(testType, true, new DeterministicGuidGenerator());

        if (probes.Length != 1)
        {
            throw new InvalidOperationException($"{nameof(InstallAndUninstallMethodProbeWithOverloadsTest)} expected one probe request to exist, but found {probes.Length} probes.");
        }

        await RunSingleTestWithApprovals(testType, isMultiPhase: true, expectedNumberOfSnapshots, probes.Select(p => p.Probe).ToArray());
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task LineProbeEmit100SnapshotsTest()
    {
        var testType = typeof(Emit100LineProbeSnapshotsTest);
        const int expectedNumberOfSnapshots = 100;

        var probes = GetProbeConfiguration(testType, true, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var sample = DebuggerTestHelper.StartSample(this, agent, testType.FullName);
        try
        {
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{sample.Process.ProcessName}*");

            SetProbeConfiguration(agent, probes.Select(p => p.Probe).ToArray());
            await logEntryWatcher.WaitForLogEntry(ProbesInstrumentedLogEntry);

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
    public async Task MethodProbeTest(Type testType)
    {
        SkipOverTestIfNeeded(testType);
        await RunMethodProbeTests(testType);
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
        EnvironmentHelper.EnableWindowsNamedPipes();

        await RunMethodProbeTests(testType);
    }

#if NETCOREAPP3_1_OR_GREATER
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task MethodProbeTest_UDS()
    {
        var testType = DebuggerTestHelper.FirstSupportedProbeTestType(EnvironmentHelper.GetTargetFramework());
        EnvironmentHelper.EnableUnixDomainSockets();

        await RunMethodProbeTests(testType);
    }
#endif

    private static SnapshotProbe CreateProbe(string typeName, string methodName, DeterministicGuidGenerator guidGenerator)
    {
        return new SnapshotProbe
        {
            Id = guidGenerator.New().ToString(),
            Language = TracerConstants.Language,
            Active = true,
            Where = new Where
            {
                TypeName = typeName,
                MethodName = methodName
            },
            Sampling = new Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 1000000 }
        };
    }

    private async Task RunMethodProbeTests(Type testType)
    {
        var probes = GetProbeConfiguration(testType, false, new DeterministicGuidGenerator());

        using var agent = EnvironmentHelper.GetMockAgent();
        SetDebuggerEnvironment(agent);
        using var sample = DebuggerTestHelper.StartSample(this, agent, testType.FullName);
        try
        {
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{sample.Process.ProcessName}*");

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

            async Task RunPhase(SnapshotProbe[] snapshotProbes, ProbeAttributeBase[] probeData, bool isMultiPhase = false, int phaseNumber = 1)
            {
                SetProbeConfiguration(agent, snapshotProbes);
                await logEntryWatcher.WaitForLogEntry(ProbesInstrumentedLogEntry);

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
                    await ApproveSnapshots(snapshots, testType, isMultiPhase, phaseNumber);
                    agent.ClearSnapshots();
                }

                var statuses = await agent.WaitForProbesStatuses(probeData.Length);

                Assert.Equal(probeData.Length, statuses?.Length);
                await ApproveStatuses(statuses, testType, isMultiPhase, phaseNumber);
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
    private void SkipOverTestIfNeeded(Type testType)
    {
        if (testType == typeof(AsyncInstanceMethod) && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
        }
    }

    private async Task RunSingleTestWithApprovals(Type testType, bool isMultiPhase, int expectedNumberOfSnapshots, params SnapshotProbe[] probes)
    {
        using var agent = EnvironmentHelper.GetMockAgent();

        SetDebuggerEnvironment(agent);
        using var sample = DebuggerTestHelper.StartSample(this, agent, testType.FullName);
        try
        {
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{sample.Process.ProcessName}*");

            SetProbeConfiguration(agent, probes);
            await logEntryWatcher.WaitForLogEntry(ProbesInstrumentedLogEntry);

            await sample.RunCodeSample();

            var snapshots = await agent.WaitForSnapshots(expectedNumberOfSnapshots);
            Assert.Equal(expectedNumberOfSnapshots, snapshots?.Length);
            await ApproveSnapshots(snapshots, testType, isMultiPhase: true, phaseNumber: 1);
            agent.ClearSnapshots();

            var statuses = await agent.WaitForProbesStatuses(probes.Length);
            Assert.Equal(probes.Length, statuses?.Length);
            await ApproveStatuses(statuses, testType, isMultiPhase: true, phaseNumber: 1);
            agent.ClearProbeStatuses();

            SetProbeConfiguration(agent, Array.Empty<SnapshotProbe>());
            await logEntryWatcher.WaitForLogEntry(ProbesInstrumentedLogEntry);
            Assert.True(await agent.WaitForNoSnapshots(6000), $"Expected 0 snapshots. Actual: {agent.Snapshots.Count}.");
        }
        finally
        {
            await sample.StopSample();
        }
    }

    private async Task ApproveSnapshots(string[] snapshots, Type testType, bool isMultiPhase, int phaseNumber)
    {
        await ApproveOnDisk(snapshots, testType, isMultiPhase, phaseNumber, "snapshots");
    }

    private async Task ApproveStatuses(string[] statuses, Type testType, bool isMultiPhase, int phaseNumber)
    {
        await ApproveOnDisk(statuses, testType, isMultiPhase, phaseNumber, "statuses");
    }

    private async Task ApproveOnDisk(string[] dataToApprove, Type testType, bool isMultiPhase, int phaseNumber, string path)
    {
        if (dataToApprove.Length > 1)
        {
            // Order the snapshots alphabetically so we'll be able to create deterministic approvals
            dataToApprove = dataToApprove.OrderBy(snapshot => snapshot).ToArray();
        }

        var settings = new VerifySettings();

        var testName = isMultiPhase ? $"{testType.Name}_#{phaseNumber}." : testType.Name;
        settings.UseFileName($"{nameof(ProbeTests)}.{testName}");
        settings.DisableRequireUniquePrefix();

        settings.ScrubEmptyLines();
        settings.AddScrubber(ScrubSnapshotJson);

        VerifierSettings.DerivePathInfo(
            (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", path)));

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

    private (ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)[] GetProbeConfiguration(Type testType, bool unlisted, DeterministicGuidGenerator guidGenerator)
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
        SetProbeConfiguration(agent, Array.Empty<SnapshotProbe>());
    }

    private void SetProbeConfiguration(MockTracerAgent agent, SnapshotProbe[] snapshotProbes)
    {
        var probeConfiguration = new ProbeConfiguration { Id = Guid.Empty.ToString(), SnapshotProbes = snapshotProbes };
        var configurations = new List<(object Config, string Id)>
        {
            new(probeConfiguration, EnvironmentHelper.SampleName.ToUUID())
        };

        agent.SetupRcm(Output, configurations, LiveDebuggerProduct.ProductName);
    }
}
