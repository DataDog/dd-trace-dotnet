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
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json;
using Samples.Probes;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger
{
    [CollectionDefinition(nameof(ProbesTests), DisableParallelization = true)]
    [Collection(nameof(ProbesTests))]
    [UsesVerify]
    public class ProbesTests : TestHelper
    {
        private const string _probesDefinitationFileName = "probes_definition.json";

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
            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using var agent = EnvironmentHelper.GetMockAgent();
            SetEnvironmentVariable(ConfigurationKeys.Debugger.AgentPort, agent.Port.ToString());
            agent.ShouldDeserializeTraces = false;
            using var processResult = RunSampleAndWaitForExit(agent, arguments: testType.FullName);
            var snapshots = agent.WaitForSnapshots(1);
            Assert.NotNull(snapshots);
            Assert.True(snapshots.Any(), "No snapshot has been received");

            var settings = new VerifySettings();
            settings.UseParameters(testType);
            settings.ScrubLinesContaining("duration: ", "timestamp: ", "method: System.", "lineNumber: ");
            settings.ScrubLinesWithReplace(ReplacePathWithFileName);
            settings.ScrubEmptyLines();
            settings.AddScrubber(RemoveStackEntryIfNeeded);

            VerifierSettings.DerivePathInfo(
                (sourceFile, projectDirectory, type, method) =>
                {
                    return new(directory: Path.Combine(sourceFile, "..", "snapshots"));
                });

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
            // We want to remove stack entries without "method" property (which removed earlier if the method starts with 'System.')
            // If we will return the line number in the future we can use this one: https://regex101.com/r/CnpoO2/2
            const string pattern = @"({)\s+(fileName: .*|\s+)(,?)\s+(},|})";
            var value = input.ToString();
            var result = Regex.Replace(value, pattern, string.Empty);
            if (value.Equals(result, StringComparison.Ordinal))
            {
                return;
            }

            input.Clear().Append(result);
        }

        private ProbeConfiguration CreateProbeDefinition(Type type)
        {
            var probes = type.GetMethods().
                Where(m =>
                {
                    var att = m.GetCustomAttribute<MethodProbeTestDataAttribute>();
                    return att?.Skip == false && att?.SkipOnFrameworks.Contains(EnvironmentHelper.GetTargetFramework()) == false;
                }).
                Select(method => CreateSnapshotProbe(method)).
                ToArray();

            if (probes.Any())
            {
                return new ProbeConfiguration { Id = Guid.Empty.ToString(), SnapshotProbes = probes };
            }

            return null;
        }

        private SnapshotProbe CreateSnapshotProbe(MethodInfo method)
        {
            var probeTestData = method.GetCustomAttribute<MethodProbeTestDataAttribute>();
            var snapshotProbe = new SnapshotProbe();
            snapshotProbe.Id = Guid.Empty.ToString();
            snapshotProbe.Language = TracerConstants.Language;
            snapshotProbe.Active = true;
            var @where = new Where();
            where.TypeName = method.DeclaringType.FullName;
            where.MethodName = method.Name;
            var signature = probeTestData.ReturnTypeName;
            if (probeTestData.ParametersTypeName?.Any() == true)
            {
                signature += "," + string.Join(",", probeTestData.ParametersTypeName);
            }

            where.Signature = signature;
            snapshotProbe.Where = where;
            return snapshotProbe;
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
}
