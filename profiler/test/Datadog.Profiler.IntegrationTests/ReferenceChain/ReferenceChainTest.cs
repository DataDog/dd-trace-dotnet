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

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckSimpleChainScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 1: Simple Chain (~1K objects)
            // Static Dictionary -> Order -> Customer -> Address
            //                            -> OrderItem[] -> Product
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

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

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected reference chains and Stack root
            Assert.True(
                trees.Any(tree =>
                    HasRootOfCategory(tree, "K") &&
                    HasAncestorDescendantChain(tree, "Order", "Customer") &&
                    HasAncestorDescendantChain(tree, "Customer", "Address") &&
                    HasAncestorDescendantChain(tree, "Order", "Product")),
                "Expected at least one snapshot to contain Stack root and Order->Customer->Address and Order->Product chains");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckCyclesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 3: Cycles - Parent -> Child -> Parent (bidirectional tree)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 3");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            // Verify reference tree JSON files were created (cycle detection should prevent crashes)
            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for cycle scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected self-referencing chain
            Assert.True(
                trees.Any(tree =>
                    HasSelfReferencingChain(tree, "TreeNode") &&
                    GetMaxTreeDepth(tree) < 200),
                "Expected at least one snapshot to contain TreeNode self-referencing chain with finite depth");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckDeepHierarchyScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 4: Deep Hierarchy - Root -> Level0 -> Level1 -> ... -> Level9
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 4");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for deep hierarchy scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the full deep hierarchy chain
            Assert.True(
                trees.Any(tree =>
                    Enumerable.Range(0, 9).All(i =>
                        HasAncestorDescendantChain(tree, $"Level{i}", $"Level{i + 1}"))),
                "Expected at least one snapshot to contain the full Level0->Level1->...->Level9 chain");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckMultipleRootsScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 2: Multiple Roots
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 2");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for multiple roots scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected chains
            Assert.True(
                trees.Any(tree =>
                    tree.Roots.Count > 0 &&
                    HasAncestorDescendantChain(tree, "Order", "Customer") &&
                    TypeExistsInTree(tree, "Product")),
                "Expected at least one snapshot to contain Order->Customer chain and Product type");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckWideTreeScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 5: Wide Tree - 100 branches x 50 leaves
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 5");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for wide tree scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected chain
            Assert.True(
                trees.Any(tree => HasAncestorDescendantChain(tree, "WideBranch", "Leaf")),
                "Expected at least one snapshot to contain WideBranch->Leaf chain");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckMixedStructuresScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 6: Mixed Structures - arrays of arrays, dictionaries, byte[] (value-type arrays skipped)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 6");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for mixed structures scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected chains
            Assert.True(
                trees.Any(tree =>
                    HasAncestorDescendantChain(tree, "Container", "Payload") &&
                    HasAncestorDescendantChain(tree, "Payload", "Metadata") &&
                    HasAncestorDescendantChain(tree, "Container", "Leaf")),
                "Expected at least one snapshot to contain Container->Payload->Metadata and Container->Leaf chains");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckSharedReferencesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 8: Shared References - multiple holders reference the same payload
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 8");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for shared references scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected chain with valid instance counts
            Assert.True(
                trees.Any(tree =>
                    HasAncestorDescendantChain(tree, "SharedHolder", "SharedPayload") &&
                    FindNodesOfType(tree, "SharedPayload").Any(n => n.InstanceCount > 0)),
                "Expected at least one snapshot to contain SharedHolder->SharedPayload chain with InstanceCount > 0");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckLinkedListScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 9: Linked List - self-referencing type chain (LinkedNode -> LinkedNode -> ...)
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 9");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for linked list scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected self-referencing chain
            Assert.True(
                trees.Any(tree =>
                    HasSelfReferencingChain(tree, "LinkedNode") &&
                    GetSelfReferencingDepth(tree, "LinkedNode") >= 2),
                "Expected at least one snapshot to contain LinkedNode self-referencing chain with depth >= 2");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckNullFieldsScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 10: Null Fields - objects with most reference fields intentionally null
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 10");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for null fields scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected chain with correct null field behavior
            Assert.True(
                trees.Any(tree =>
                {
                    if (!HasAncestorDescendantChain(tree, "SparseObject", "Customer"))
                    {
                        return false;
                    }

                    var sparseNodes = FindNodesOfType(tree, "SparseObject");
                    return sparseNodes.All(sparseNode =>
                    {
                        var directChildTypeNames = sparseNode.Children
                            .Select(c => tree.GetShortTypeName(c.TypeIndex))
                            .ToHashSet();
                        return !directChildTypeNames.Contains("Product") && !directChildTypeNames.Contains("Order");
                    });
                }),
                "Expected at least one snapshot to contain SparseObject->Customer without Product/Order as direct children");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckStructWithReferencesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 11: Value type array with embedded reference fields
            // StructWithReferences[] -> Customer -> Address
            //                        -> Product
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 11");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for struct with references scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected chains through value type array elements
            Assert.True(
                trees.Any(tree =>
                    HasAncestorDescendantChain(tree, "StructWithReferences", "Customer") &&
                    HasAncestorDescendantChain(tree, "Customer", "Address") &&
                    HasAncestorDescendantChain(tree, "StructWithReferences", "Product")),
                "Expected at least one snapshot to contain StructWithReferences[]->Customer->Address and StructWithReferences[]->Product chains");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckStaticRootScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 12: Same simple chain as Scenario 1 but held by a static field.
            // The GC reports static roots via GCBulkRootStaticVar, bypassing stack root handling.
            // Static List<Order> -> Order -> Customer -> Address
            //                             -> OrderItem[] -> Product
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 12");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for static root scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // At least one snapshot must contain the expected reference chains and static root with field name
            Assert.True(
                trees.Any(tree =>
                    HasRootOfCategory(tree, "S") &&
                    tree.Roots.Any(r => r.CategoryCode == "S" && !string.IsNullOrEmpty(r.FieldName)) &&
                    HasAncestorDescendantChain(tree, "Order", "Customer") &&
                    HasAncestorDescendantChain(tree, "Customer", "Address") &&
                    HasAncestorDescendantChain(tree, "Order", "Product")),
                "Expected at least one snapshot to contain StaticVariable root with field name and Order->Customer->Address and Order->Product chains");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckEventHandlerLeakScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 13: Event handler leak - publisher holds subscribers via event delegate
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 13");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for event handler leak scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    HasAncestorDescendantChain(tree, "EventSubscriber", "LeakedPayload") &&
                    TypeExistsInTree(tree, "EventPublisher")),
                "Expected at least one snapshot to contain EventSubscriber->LeakedPayload chain and EventPublisher type");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckClosureLeakScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 14: Closure / captured variable leak - lambdas capturing expensive objects
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 14");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for closure leak scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree => HasAncestorDescendantChain(tree, "ClosureHolder", "ExpensiveResource")),
                "Expected at least one snapshot to contain ClosureHolder->ExpensiveResource chain");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckTimerLeakScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 15: Timer callback leak - Timer keeping callback targets alive
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 15");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for timer leak scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    HasAncestorDescendantChain(tree, "MonitoredService", "ServiceMetrics") &&
                    TypeExistsInTree(tree, "TimerOwner")),
                "Expected at least one snapshot to contain MonitoredService->ServiceMetrics chain and TimerOwner type");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckGCHandleLeakScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 16: Strong GCHandle leak - tests Handle root category
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 16");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for GCHandle leak scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    HasRootOfCategory(tree, "H") &&
                    HasAncestorDescendantChain(tree, "HandleTarget", "InteropPayload")),
                "Expected at least one snapshot to contain Handle root and HandleTarget->InteropPayload chain");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckPinnedLeakScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 18: Pinned handle - tests Pinning root category
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 18");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for pinned leak scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree => HasRootOfCategory(tree, "P")),
                "Expected at least one snapshot to contain Pinning root");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckAsyncLeakScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 19: Async state machine leak - never-completing Task capturing HeavyContext
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 19");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for async leak scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "AsyncLeakSource") &&
                    TypeExistsInTree(tree, "HeavyContext")),
                "Expected at least one snapshot to contain AsyncLeakSource and HeavyContext types");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckNestedValueTypeScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 20: Nested inline value types - OuterHolder<InnerStruct> where InnerStruct
            // contains a reference (NestedVtTarget) and a nested struct (NestedInnerStruct) that
            // itself contains a reference (DeepVtTarget). Tests recursive inline VT traversal.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 20");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for nested value type scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // The inline VT traversal should find both:
            // - NestedVtTarget (referenced by InnerStruct.ShallowRef, 1 level deep)
            // - DeepVtTarget (referenced by NestedInnerStruct.DeepRef inside InnerStruct.Nested, 2 levels deep)
            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "OuterHolder") &&
                    TypeExistsInTree(tree, "NestedVtTarget") &&
                    TypeExistsInTree(tree, "DeepVtTarget")),
                "Expected at least one snapshot to contain OuterHolder, NestedVtTarget, and DeepVtTarget types");
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
        /// Load all reference tree JSON files, validate their structure, and return the parsed trees.
        /// Structural validation runs on every file; chain validation is left to the caller.
        /// </summary>
        private List<ReferenceTree> LoadAndValidateAllTrees(string[] referenceTreeFiles)
        {
            var trees = new List<ReferenceTree>();
            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                var jsonContent = File.ReadAllText(referenceTreeFile);
                _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, System.Math.Min(2000, jsonContent.Length))}...");

                ValidateReferenceTreeJsonStructure(jsonContent);
                trees.Add(ReferenceTreeLoader.Load(jsonContent));
            }

            return trees;
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
            Assert.Equal(1, version.GetInt32());

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
        /// Check if any root in the tree has the given category code.
        /// Category codes: "K" (Stack), "S" (StaticVariable), "F" (Finalizer), "H" (Handle), "P" (Pinning), "O" (Other), etc.
        /// </summary>
        private static bool HasRootOfCategory(ReferenceTree tree, string categoryCode)
        {
            return tree.Roots.Any(r => r.CategoryCode == categoryCode);
        }

        /// <summary>
        /// Check if any root has the given category code and type name.
        /// </summary>
        private static bool HasRootOfCategoryAndType(ReferenceTree tree, string categoryCode, string typeName)
        {
            return tree.Roots.Any(r =>
                r.CategoryCode == categoryCode &&
                TypeNameMatches(tree.GetTypeName(r.TypeIndex), typeName));
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
