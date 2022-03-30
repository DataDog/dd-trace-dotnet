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
    private readonly string[] _typesToScrub = { nameof(IntPtr) };

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
        var snapshots = agent.WaitForSnapshots(1);
        Assert.NotNull(snapshots);
        Assert.True(snapshots.Any(), "No snapshot has been received");

        var settings = new VerifySettings();
        settings.UseParameters(testType);
        settings.ScrubLinesContaining("duration: ", "timestamp: ", "function: System.", "lineNumber: ");
        settings.ScrubLinesWithReplace(ReplacePathWithFileName);
        settings.ScrubEmptyLines();
        settings.AddScrubber(RemoveStackEntryIfNeeded);
        settings.AddScrubber(ScrubKnownTypes);
        settings.AddScrubber(ScrubMoveNextFramesFromStacktrace);

        VerifierSettings.DerivePathInfo(
            (sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "snapshots")));

        await Verifier.VerifyJson(string.Join(Environment.NewLine, snapshots), settings);
    }

    private string ReplacePathWithFileName(string input)
    {
        const string fileNameString = "fileName: ";
        var indexOfFileName = input.IndexOf(fileNameString);
        if (indexOfFileName < 0)
        {
            return input;
        }

        var indexOfPathStart = indexOfFileName + fileNameString.Length; // fileName: path.cs -> 'p' is start index
        var length = input.Length - indexOfFileName - fileNameString.Length; // fileName: path.cs, -> "path.cs" is length
        var file = new FileInfo(input.Substring(indexOfPathStart, length));
        input = input.Remove(indexOfPathStart); // remove path
        return input + file.Name; // add only the file name to avoid conflicts between differents environment and deifferent OS
    }

    private void RemoveStackEntryIfNeeded(StringBuilder input)
    {
        // We want to remove stack entries without "function" property (which removed earlier if the method starts with 'System.')
        // If we will return the line number in the future we can use this one: https://regex101.com/r/CnpoO2/2 :
        // ({)\s+(fileName: .*|lineNumber: \d+)(,?)\s+(lineNumber: \d+,\s(},|})|},|})
        const string pattern = @"({)\s+(fileName: .*|\s+)(,?)\s+(},|})";
        var value = input.ToString();
        var result = Regex.Replace(value, pattern, string.Empty);
        if (value.Equals(result, StringComparison.Ordinal))
        {
            return;
        }

        input.Clear().Append(result);
    }

    /// <summary>
    /// Sanitizes types whose values may vary from run to run and consequently produce a different approval file.
    /// </summary>
    /// <param name="input">Snapshot</param>
    private void ScrubKnownTypes(StringBuilder input)
    {
        string value = input.ToString();
        var typesToScrub = string.Join("|", _typesToScrub);
        string pattern = @"\s+(type:\s)(" + typesToScrub + @")(,\s)\s+(value:\s).*(\s+},)";
        string replacement = "type: ScrubbedTypeName, value: \"ScrubbedValue\" },";
        string result = Regex.Replace(value, pattern, replacement);

        if (value.Equals(result, StringComparison.Ordinal))
        {
            return;
        }

        input.Clear().Append(result);
    }

    /// <summary>
    /// Scrub MoveNext methods from `stack` in the snapshot as it varies between Windows/Linux.
    /// </summary>
    /// <param name="input">Snapshot</param>
    private void ScrubMoveNextFramesFromStacktrace(StringBuilder input)
    {
        string value = input.ToString();
        string pattern = @"{(\s+)(.*)(\s+)function:\s+.*<.*.MoveNext,\s+},";
        string result = Regex.Replace(value, pattern, string.Empty);

        if (value.Equals(result, StringComparison.Ordinal))
        {
            return;
        }

        input.Clear().Append(result);
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
