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
    private const string _probesDefinitationFileName = "probes_definition.json";
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

    [Theory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(ProbeTests))]
    public async Task MethodProbeTest(Type testType)
    {
        var definition = CreateProbeDefinition(testType);
        if (definition == null)
        {
            return;
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

        string toVerify = string.Join(Environment.NewLine, snapshots.Select(JsonUtility.NormalizeJsonString));
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

    private ProbeConfiguration CreateProbeDefinition(Type type)
    {
        const BindingFlags allMask =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance;

        var snapshotMethodProbes = type.GetNestedTypes(allMask)
                                       .SelectMany(nestedType => nestedType.GetMethods(allMask))
                                       .Concat(type.GetMethods(allMask))
                                       .Where(
                                            m =>
                                            {
                                                var att = m.GetCustomAttribute<MethodProbeTestDataAttribute>();
                                                return att?.Skip == false && att?.SkipOnFrameworks.Contains(EnvironmentHelper.GetTargetFramework()) == false;
                                            })
                                       .Select(CreateSnapshotMethodProbe)
                                       .ToArray();

        var snapshotLineProbes = type.GetCustomAttributes<LineProbeTestDataAttribute>()
                                     .Where(att => att?.Skip == false && att?.SkipOnFrameworks.Contains(EnvironmentHelper.GetTargetFramework()) == false)
                                     .Select(att => CreateSnapshotLineProbe(type, att))
                                     .ToArray();

        var allProbes = snapshotLineProbes.Concat(snapshotMethodProbes).ToArray();
        if (allProbes.Any())
        {
            return new ProbeConfiguration { Id = Guid.Empty.ToString(), SnapshotProbes = allProbes };
        }

        return null;
    }

    private SnapshotProbe CreateSnapshotLineProbe(Type type, LineProbeTestDataAttribute line)
    {
        var where = CreateLineProbeWhere(type, line);
        return CreateSnapshotProbe(where);
    }

    private Where CreateLineProbeWhere(Type type, LineProbeTestDataAttribute line)
    {
        using var reader = DatadogPdbReader.CreatePdbReader(type.Assembly);
        var symbolMethod = reader.ReadMethodSymbolInfo(type.GetMethods().First().MetadataToken);
        var filePath = symbolMethod.SequencePoints.First().Document.URL;
        return new Where() { SourceFile = filePath, Lines = new[] { line.LineNumber.ToString() } };
    }

    private SnapshotProbe CreateSnapshotMethodProbe(MethodInfo method)
    {
        var probeTestData = method.GetCustomAttribute<MethodProbeTestDataAttribute>();
        var where = CreateMethodProbeWhere(method, probeTestData);
        return CreateSnapshotProbe(where);
    }

    private SnapshotProbe CreateSnapshotProbe(Where where)
    {
        return new SnapshotProbe { Id = Guid.Empty.ToString(), Language = TracerConstants.Language, Active = true, Where = where };
    }

    private Where CreateMethodProbeWhere(MethodInfo method, MethodProbeTestDataAttribute probeTestData)
    {
        var @where = new Where();
        @where.TypeName = method.DeclaringType.FullName;
        @where.MethodName = method.Name;
        var signature = probeTestData.ReturnTypeName;
        if (probeTestData.ParametersTypeName?.Any() == true)
        {
            signature += "," + string.Join(",", probeTestData.ParametersTypeName);
        }

        @where.Signature = signature;
        return @where;
    }

    private string WriteProbeDefinitionToFile(ProbeConfiguration probeConfiguration)
    {
        var json = JsonConvert.SerializeObject(probeConfiguration, Formatting.Indented);
        // We are not using a temp file here, but rather writing it directly to the debugger sample project,
        // so that if a test fails, we will be able to simply hit F5 to debug the same probe
        // configuration (launchsettings.json references the same file).
        var path = Path.Combine(EnvironmentHelper.GetSampleProjectDirectory(), _probesDefinitationFileName);
        File.WriteAllText(path, json);
        return path;
    }
}
