// <copyright file="ReferenceChainTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.ReferenceChain
{
    public class ReferenceChainTest
    {
        // Scenario number for ReferenceChain in the Samples.Computer01 Scenario enum
        // Keep this in sync with the enum definition in Samples.Computer01/Program.cs
        private const int ReferenceChainScenarioNumber = 31;

        private readonly ITestOutputHelper _output;

        public ReferenceChainTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckSimpleChainScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 1: Simple Chain (~1K objects)
            // Static Dictionary -> Order -> Customer -> Address
            //                            -> OrderItem[] -> Product
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            // check the reference_tree.json files are sent with the profiles
            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasReferenceTree = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasReferenceTree |= HasReferenceTree(ctx.Value.Request);
            };

            runner.Run(agent);

            Assert.True(hasReferenceTree, "No reference tree was sent to the agent");

            // Verify reference tree JSON files were created locally
            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                ValidateReferenceTreeJson(jsonContent);

                // Check for expected types in the simple chain scenario
                Assert.Contains("Order", jsonContent);
                Assert.Contains("Customer", jsonContent);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckCyclesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 3: Cycles - Parent -> Child -> Parent (bidirectional tree)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 3");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            // Verify reference tree JSON files were created (cycle detection should prevent crashes)
            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for cycle scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                ValidateReferenceTreeJson(jsonContent);

                // Check for TreeNode type (used in cycle scenario)
                Assert.Contains("TreeNode", jsonContent);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckDeepHierarchyScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 4: Deep Hierarchy - Root -> Level0 -> Level1 -> ... -> Level9
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 4");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for deep hierarchy scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                ValidateReferenceTreeJson(jsonContent);

                // Check for Level types
                Assert.Contains("Level0", jsonContent);
                Assert.Contains("Level9", jsonContent);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckMultipleRootsScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 2: Multiple Roots
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 2");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for multiple roots scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                ValidateReferenceTreeJson(jsonContent);

                // Verify multiple roots exist
                var doc = JsonDocument.Parse(jsonContent);
                var roots = doc.RootElement.GetProperty("r");
                Assert.True(roots.GetArrayLength() > 0, "No roots found in reference tree");
            }
        }

        private static bool HasReferenceTree(HttpListenerRequest request)
        {
            if (!request.ContentType.StartsWith("multipart/form-data"))
            {
                return false;
            }

            var mpReader = new MultiPartReader(request);
            if (!mpReader.Parse())
            {
                return false;
            }

            var files = mpReader.Files;
            var referenceTreeFileInfo = files.FirstOrDefault(f => f.FileName == "reference_tree.json");
            return referenceTreeFileInfo is not null;
        }

        private void ValidateReferenceTreeJson(string jsonContent)
        {
            Assert.False(string.IsNullOrEmpty(jsonContent), "Reference tree JSON is empty");
            Assert.NotEqual("{}", jsonContent);

            // Validate it's valid JSON
            var doc = JsonDocument.Parse(jsonContent);

            // Check required top-level fields
            Assert.True(doc.RootElement.TryGetProperty("v", out var version), "Missing 'v' (version) field");
            Assert.Equal(7, version.GetInt32());

            Assert.True(doc.RootElement.TryGetProperty("tt", out var typeTable), "Missing 'tt' (type table) field");
            Assert.True(typeTable.GetArrayLength() > 0, "Type table is empty");

            Assert.True(doc.RootElement.TryGetProperty("r", out var roots), "Missing 'r' (roots) field");
            Assert.True(roots.GetArrayLength() > 0, "Roots array is empty");

            // Validate each root has required fields
            foreach (var root in roots.EnumerateArray())
            {
                Assert.True(root.TryGetProperty("t", out _), "Root missing 't' (type index) field");
                Assert.True(root.TryGetProperty("c", out _), "Root missing 'c' (category) field");
                Assert.True(root.TryGetProperty("ic", out _), "Root missing 'ic' (instance count) field");
                Assert.True(root.TryGetProperty("ts", out _), "Root missing 'ts' (total size) field");
            }

            // Validate type table entries are strings
            foreach (var type in typeTable.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.String, type.ValueKind);
                Assert.False(string.IsNullOrEmpty(type.GetString()), "Type table entry is empty");
            }
        }
    }
}
