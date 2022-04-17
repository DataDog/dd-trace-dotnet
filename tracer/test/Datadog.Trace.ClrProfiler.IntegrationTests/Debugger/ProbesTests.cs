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
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.PDBs;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Samples.Probes;
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
        return typeof(IRun).Assembly.GetTypes().Where(t => t.GetInterface(nameof(IRun)) != null).Select(t => new object[] { t });
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(ProbeTests))]
    public async Task MethodProbeTest(Type testType)
    {
        var definition = DebuggerTestHelper.CreateProbeDefinition(testType, EnvironmentHelper.GetTargetFramework());
        if (definition == null)
        {
            throw new SkipException($"Definition for {testType.Name} is null, skipping.");
        }

        var path = WriteProbeDefinitionToFile(definition);
        SetEnvironmentVariable(ConfigurationKeys.Debugger.ProbeFile, path);
        SetEnvironmentVariable(ConfigurationKeys.Debugger.DebuggerEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Debugger.MaxDepthToSerialize, "3");
        int httpPort = TcpPortProvider.GetOpenPort();
        Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

        using var agent = EnvironmentHelper.GetMockAgent();
        SetEnvironmentVariable(ConfigurationKeys.AgentPort, agent.Port.ToString());
        agent.ShouldDeserializeTraces = false;
        using var processResult = RunSampleAndWaitForExit(agent, arguments: testType.FullName);
        var snapshots = agent.WaitForSnapshots(1).ToArray();
        Assert.NotNull(snapshots);
        Assert.True(snapshots.Any(), "No snapshot has been received");

        var settings = new VerifySettings();
        settings.UseParameters(testType);
        settings.ScrubEmptyLines();
        settings.AddScrubber(ScrubSnapshotJson);

        VerifierSettings.DerivePathInfo(
            (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "snapshots")));

        string toVerify = string.Join(Environment.NewLine, snapshots.Select(JsonUtility.NormalizeJson));
        await Verifier.Verify(NormalizeLineEndings(toVerify), settings);
    }

    private string NormalizeLineEndings(string text) =>
        text
           .Replace(@"\r\n", @"\n")
           .Replace(@"\n\r", @"\n")
           .Replace(@"\r", @"\n")
           .Replace(@"\n", @"\r\n");

    private void ScrubSnapshotJson(StringBuilder input)
    {
        var json = JArray.Parse(input.ToString());

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
                if (item.Key == "fileName")
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
