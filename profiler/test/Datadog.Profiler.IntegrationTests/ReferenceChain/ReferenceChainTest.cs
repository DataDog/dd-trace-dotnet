// <copyright file="ReferenceChainTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Datadog.Profiler.IntegrationTests.Helpers;
using ReferenceChainModel;
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
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // Validate the reference chain: Order -> Customer -> Address
                Assert.True(
                    HasAncestorDescendantChain(tree, "Order", "Customer"),
                    "Expected Order to have Customer as descendant");
                Assert.True(
                    HasAncestorDescendantChain(tree, "Customer", "Address"),
                    "Expected Customer to have Address as descendant");

                // Validate: Order -> ... -> Product (via OrderItem array)
                Assert.True(
                    HasAncestorDescendantChain(tree, "Order", "Product"),
                    "Expected Order to have Product as descendant (via OrderItem)");
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
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // TreeNode has Parent (TreeNode) and Children (List<TreeNode>).
                // The cycle detection should stop the tree from growing infinitely.
                Assert.True(
                    HasSelfReferencingChain(tree, "TreeNode"),
                    "Expected TreeNode to have TreeNode as descendant (self-referencing via Parent/Children)");

                // The tree MUST be finite (cycle detection worked).
                int maxDepth = GetMaxTreeDepth(tree);
                Assert.True(maxDepth < 200, $"Tree depth {maxDepth} is suspiciously deep; cycle detection may have failed");
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
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // Validate the full chain: Level0 -> Level1 -> ... -> Level9
                for (int i = 0; i < 9; i++)
                {
                    string parent = $"Level{i}";
                    string child = $"Level{i + 1}";
                    Assert.True(
                        HasAncestorDescendantChain(tree, parent, child),
                        $"Expected {parent} to have {child} as descendant in the deep hierarchy chain");
                }
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
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // Verify multiple roots exist
                Assert.True(tree.Roots.Count > 0, "No roots found in reference tree");

                // Validate that Order -> Customer chain exists
                Assert.True(
                    HasAncestorDescendantChain(tree, "Order", "Customer"),
                    "Expected Order to have Customer as descendant");

                // Validate that Product appears somewhere in the tree
                Assert.True(
                    TypeExistsInTree(tree, "Product"),
                    "Expected Product to appear in the reference tree");
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckWideTreeScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 5: Wide Tree - 100 branches x 50 leaves
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 5");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for wide tree scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // Validate: WideBranch -> Leaf chain
                Assert.True(
                    HasAncestorDescendantChain(tree, "WideBranch", "Leaf"),
                    "Expected WideBranch to have Leaf as descendant");
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckMixedStructuresScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 6: Mixed Structures - arrays of arrays, dictionaries, byte[] (value-type arrays skipped)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 6");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for mixed structures scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // Validate: Container -> Payload -> Metadata chain
                Assert.True(
                    HasAncestorDescendantChain(tree, "Container", "Payload"),
                    "Expected Container to have Payload as descendant");
                Assert.True(
                    HasAncestorDescendantChain(tree, "Payload", "Metadata"),
                    "Expected Payload to have Metadata as descendant");

                // Leaf is used inside Container.Matrix (jagged array)
                Assert.True(
                    HasAncestorDescendantChain(tree, "Container", "Leaf"),
                    "Expected Container to have Leaf as descendant (via Matrix jagged array)");
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckSharedReferencesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 8: Shared References - multiple holders reference the same payload
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 8");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for shared references scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // Validate: SharedHolder -> SharedPayload chain
                Assert.True(
                    HasAncestorDescendantChain(tree, "SharedHolder", "SharedPayload"),
                    "Expected SharedHolder to have SharedPayload as descendant");

                // Validate the instance count on SharedPayload nodes.
                // Find any SharedPayload node and verify instance count > 0.
                var payloadNodes = FindNodesOfType(tree, "SharedPayload");
                Assert.True(payloadNodes.Count > 0, "Expected to find SharedPayload nodes in the tree");
                foreach (var payloadNode in payloadNodes)
                {
                    Assert.True(payloadNode.InstanceCount > 0, "SharedPayload instance count should be > 0");
                }
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckLinkedListScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 9: Linked List - self-referencing type chain (LinkedNode -> LinkedNode -> ...)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 9");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for linked list scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // LinkedNode -> LinkedNode self-referencing chain
                Assert.True(
                    HasSelfReferencingChain(tree, "LinkedNode"),
                    "Expected LinkedNode to have LinkedNode as descendant (self-referencing chain)");

                // Measure the self-referencing depth
                int selfRefDepth = GetSelfReferencingDepth(tree, "LinkedNode");
                _output.WriteLine($"LinkedNode self-referencing depth: {selfRefDepth}");
                Assert.True(selfRefDepth >= 2, $"Expected LinkedNode self-referencing depth >= 2, got {selfRefDepth}");
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckNullFieldsScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 10: Null Fields - objects with most reference fields intentionally null
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 10");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for null fields scenario");

            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);

                var tree = ReferenceTreeLoader.Load(jsonContent);

                // SparseObject has only FilledRef (Customer) set; null fields should not generate children.
                Assert.True(
                    HasAncestorDescendantChain(tree, "SparseObject", "Customer"),
                    "Expected SparseObject to have Customer as descendant (FilledRef is set)");

                // Product and Order should NOT appear as direct children of SparseObject.
                var sparseNodes = FindNodesOfType(tree, "SparseObject");
                foreach (var sparseNode in sparseNodes)
                {
                    var directChildTypeNames = sparseNode.Children
                        .Select(c => tree.GetShortTypeName(c.TypeIndex))
                        .ToHashSet();
                    Assert.DoesNotContain("Product", directChildTypeNames);
                    Assert.DoesNotContain("Order", directChildTypeNames);
                    _output.WriteLine($"SparseObject direct children: [{string.Join(", ", directChildTypeNames)}]");
                }
            }
        }

        // ====================================================================
        // Static helpers
        // ====================================================================

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

        /// <summary>
        /// Validate the raw JSON structure (required fields, valid format).
        /// This checks the low-level JSON format; chain validation uses the model.
        /// </summary>
        private static void ValidateReferenceTreeJsonStructure(string jsonContent)
        {
            Assert.False(string.IsNullOrEmpty(jsonContent), "Reference tree JSON is empty");
            Assert.NotEqual("{}", jsonContent);

            var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions { MaxDepth = 256 });

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

            // Validate all instance counts and sizes are non-negative
            ValidateNodeCountsRecursive(roots);
        }

        private static void ValidateNodeCountsRecursive(JsonElement nodesArray)
        {
            foreach (var node in nodesArray.EnumerateArray())
            {
                if (node.TryGetProperty("ic", out var ic))
                {
                    Assert.True(ic.GetInt64() >= 0, "Instance count must be non-negative");
                }

                if (node.TryGetProperty("ts", out var ts))
                {
                    Assert.True(ts.GetInt64() >= 0, "Total size must be non-negative");
                }

                if (node.TryGetProperty("ch", out var children))
                {
                    ValidateNodeCountsRecursive(children);
                }
            }
        }

        // ====================================================================
        // Model-based chain validation helpers (using ReferenceChainModel)
        // ====================================================================

        private static bool TypeNameMatches(string fullTypeName, string targetShortName)
        {
            return fullTypeName is not null && fullTypeName.Contains(targetShortName);
        }

        private static bool HasDescendant(ReferenceNode node, ReferenceTree tree, string targetTypeName)
        {
            foreach (var child in node.Children)
            {
                var childName = tree.GetTypeName(child.TypeIndex);
                if (TypeNameMatches(childName, targetTypeName))
                {
                    return true;
                }

                if (HasDescendant(child, tree, targetTypeName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verify that somewhere in the tree, there exists a node of type "ancestorType"
        /// that has a descendant (at any depth below it) of type "descendantType".
        /// </summary>
        private static bool HasAncestorDescendantChain(ReferenceTree tree, string ancestorType, string descendantType)
        {
            var ancestorNodes = FindNodesOfType(tree, ancestorType);
            return ancestorNodes.Any(node => HasDescendant(node, tree, descendantType));
        }

        private static bool TypeExistsInTree(ReferenceTree tree, string targetTypeName)
        {
            return FindNodesOfType(tree, targetTypeName).Count > 0;
        }

        /// <summary>
        /// Recursively find all nodes in the tree whose type name contains the target string.
        /// </summary>
        private static List<ReferenceNode> FindNodesOfType(ReferenceTree tree, string targetTypeName)
        {
            var results = new List<ReferenceNode>();
            foreach (var root in tree.Roots)
            {
                FindNodesOfTypeRecursive(root, tree, targetTypeName, results);
            }

            return results;
        }

        private static void FindNodesOfTypeRecursive(
            ReferenceNode node,
            ReferenceTree tree,
            string targetTypeName,
            List<ReferenceNode> results)
        {
            var nodeName = tree.GetTypeName(node.TypeIndex);
            if (TypeNameMatches(nodeName, targetTypeName))
            {
                results.Add(node);
            }

            foreach (var child in node.Children)
            {
                FindNodesOfTypeRecursive(child, tree, targetTypeName, results);
            }
        }

        /// <summary>
        /// Check if a type appears somewhere in the tree and has itself as a descendant.
        /// </summary>
        private static bool HasSelfReferencingChain(ReferenceTree tree, string typeName)
        {
            var nodesOfType = FindNodesOfType(tree, typeName);
            return nodesOfType.Any(node => HasDescendant(node, tree, typeName));
        }

        /// <summary>
        /// Starting from any node of the given type, find the maximum depth of
        /// consecutive self-referencing nesting.
        /// </summary>
        private static int GetSelfReferencingDepth(ReferenceTree tree, string typeName)
        {
            var nodesOfType = FindNodesOfType(tree, typeName);
            int maxDepth = 0;
            foreach (var node in nodesOfType)
            {
                int depth = MeasureSelfRefDepth(node, tree, typeName, 0);
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                }
            }

            return maxDepth;
        }

        private static int MeasureSelfRefDepth(ReferenceNode node, ReferenceTree tree, string typeName, int currentDepth)
        {
            int maxChildDepth = currentDepth;
            foreach (var child in node.Children)
            {
                var childName = tree.GetTypeName(child.TypeIndex);
                if (TypeNameMatches(childName, typeName))
                {
                    int childDepth = MeasureSelfRefDepth(child, tree, typeName, currentDepth + 1);
                    if (childDepth > maxChildDepth)
                    {
                        maxChildDepth = childDepth;
                    }
                }
            }

            return maxChildDepth;
        }

        /// <summary>
        /// Get the maximum depth of the entire tree.
        /// </summary>
        private static int GetMaxTreeDepth(ReferenceTree tree)
        {
            int maxDepth = 0;
            foreach (var root in tree.Roots)
            {
                int depth = MeasureDepth(root, 1);
                if (depth > maxDepth)
                {
                    maxDepth = depth;
                }
            }

            return maxDepth;
        }

        private static int MeasureDepth(ReferenceNode node, int currentDepth)
        {
            int maxChildDepth = currentDepth;
            foreach (var child in node.Children)
            {
                int childDepth = MeasureDepth(child, currentDepth + 1);
                if (childDepth > maxChildDepth)
                {
                    maxChildDepth = childDepth;
                }
            }

            return maxChildDepth;
        }
    }
}
