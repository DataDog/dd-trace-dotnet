// <copyright file="ReferenceChainTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            // check the reference_tree.json files are sent with the profiles
            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasReferenceTree = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasReferenceTree |= HasReferenceTreeFile(ctx.Value.Request, "reference_tree.json");
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
        public void CheckReferenceTreeProducedAcrossConsecutiveSnapshots(string appName, string framework, string appAssembly)
        {
            // Regression guard for fault handling: a memory access fault during one
            // snapshot's traversal must not disable reference chains for the following
            // snapshots. We run long enough for several snapshots and assert that a
            // valid reference tree is produced by each consecutive snapshot, not just
            // the first one.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 50;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "10");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated");

            // Group the emitted files by the export (snapshot) they belong to. Each
            // snapshot that ran the traversal and found a root emits exactly one tree.
            var snapshotIds = referenceTreeFiles
                .Select(f => GetSnapshotId(f, "reference_tree_"))
                .Distinct()
                .ToList();

            _output.WriteLine($"Reference trees were produced for {snapshotIds.Count} distinct snapshot(s).");

            // "A bad dump does not kill the next one": we must see trees from multiple
            // consecutive snapshots, not a single one followed by silence.
            Assert.True(
                snapshotIds.Count >= 2,
                $"Expected reference trees from at least 2 consecutive snapshots, got {snapshotIds.Count}");

            // Every emitted tree must be structurally valid and contain the scenario chain,
            // proving traversal kept working snapshot after snapshot.
            var trees = LoadAndValidateAllTrees(referenceTreeFiles);
            Assert.Equal(referenceTreeFiles.Length, trees.Count);

            Assert.True(
                trees.Any(tree =>
                    HasRootOfCategory(tree, "K") &&
                    HasAncestorDescendantChain(tree, "Order", "Customer") &&
                    HasAncestorDescendantChain(tree, "Customer", "Address")),
                "Expected at least one snapshot to still contain the Order->Customer->Address chain");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckSimpleChainBinaryFormat(string appName, string framework, string appAssembly)
        {
            // Same as CheckSimpleChainScenario but with binary serialization (format=1, the default).
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "1"); // binary

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasReferenceTree = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasReferenceTree |= HasReferenceTreeFile(ctx.Value.Request, "reference_tree.bin");
            };

            runner.Run(agent);

            Assert.True(hasReferenceTree, "No binary reference tree was sent to the agent");

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.bin");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree binary files were generated");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    HasRootOfCategory(tree, "K") &&
                    HasAncestorDescendantChain(tree, "Order", "Customer") &&
                    HasAncestorDescendantChain(tree, "Customer", "Address") &&
                    HasAncestorDescendantChain(tree, "Order", "Product")),
                "Expected at least one binary snapshot to contain Stack root and Order->Customer->Address and Order->Product chains");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckSimpleChainBothFormats(string appName, string framework, string appAssembly)
        {
            // Format=3 emits both JSON and binary files for the same tree — useful for validation.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "3"); // both

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasJsonTree = false;
            bool hasBinTree = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                var request = ctx.Value.Request;
                if (!request.ContentType.StartsWith("multipart/form-data"))
                {
                    return;
                }

                var mpReader = new MultiPartReader(request);
                if (!mpReader.Parse())
                {
                    return;
                }

                hasJsonTree |= mpReader.Files.Any(f => f.FileName == "reference_tree.json");
                hasBinTree |= mpReader.Files.Any(f => f.FileName == "reference_tree.bin");
            };

            runner.Run(agent);

            Assert.True(hasJsonTree, "No JSON reference tree was sent in both-formats mode");
            Assert.True(hasBinTree, "No binary reference tree was sent in both-formats mode");

            var jsonFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            var binFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.bin");
            Assert.True(jsonFiles.Length > 0, "No reference tree JSON files were generated in both-formats mode");
            Assert.True(binFiles.Length > 0, "No reference tree binary files were generated in both-formats mode");

            // Pair JSON and binary files from the same export cycle by matching the
            // filename stem (everything before the extension). Both formats share the
            // same generated suffix: reference_tree_<service>_<pid>_<uid>.<ext>
            var jsonByStem = jsonFiles.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => f);
            var binByStem = binFiles.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => f);
            var commonStems = jsonByStem.Keys.Intersect(binByStem.Keys).ToList();

            Assert.True(commonStems.Count > 0,
                "No matching JSON/binary file pairs found (expected same filename stem for both formats)");

            foreach (var stem in commonStems)
            {
                var jsonTree = LoadAndValidateAllTrees(new[] { jsonByStem[stem] }).Single();
                var binTree = LoadAndValidateAllTrees(new[] { binByStem[stem] }).Single();

                _output.WriteLine($"Comparing paired trees for {stem}: JSON has {jsonTree.Roots.Count} roots, binary has {binTree.Roots.Count} roots");

                Assert.Equal(jsonTree.Version, binTree.Version);
                Assert.Equal(jsonTree.TypeTable.Count, binTree.TypeTable.Count);
                Assert.Equal(jsonTree.Roots.Count, binTree.Roots.Count);

                // Verify the same type names are present (order may differ between formats)
                var jsonTypes = jsonTree.TypeTable.OrderBy(t => t).ToList();
                var binTypes = binTree.TypeTable.OrderBy(t => t).ToList();
                Assert.Equal(jsonTypes, binTypes);

                // Verify both trees have the same root categories
                var jsonCategories = jsonTree.Roots.Select(r => r.CategoryCode).OrderBy(c => c).ToList();
                var binCategories = binTree.Roots.Select(r => r.CategoryCode).OrderBy(c => c).ToList();
                Assert.Equal(jsonCategories, binCategories);

                // Verify equivalent reference chains exist in both trees
                AssertSameChains(jsonTree, binTree);
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckReferenceTreeTypesAreInClassHistogramBinary(string appName, string framework, string appAssembly)
        {
            CheckReferenceTreeTypesAreInClassHistogram(appName, framework, appAssembly, referenceTreeFormat: "1", treeFileName: "reference_tree.bin", treeExtension: "bin");
        }

        // Run against JSON as well: the two serializers build their type table independently,
        // so a type/index mismatch in one of them would not show up in the other.
        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckReferenceTreeTypesAreInClassHistogramJson(string appName, string framework, string appAssembly)
        {
            CheckReferenceTreeTypesAreInClassHistogram(appName, framework, appAssembly, referenceTreeFormat: "2", treeFileName: "reference_tree.json", treeExtension: "json");
        }

        private void CheckReferenceTreeTypesAreInClassHistogram(
            string appName,
            string framework,
            string appAssembly,
            string referenceTreeFormat,
            string treeFileName,
            string treeExtension)
        {
            // The class histogram and the reference tree are built from the same heap snapshot
            // and use the same type names, so every heap object type listed in the tree is
            // expected to appear in the histogram of that snapshot.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, referenceTreeFormat);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasBothFiles = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                var request = ctx.Value.Request;
                if (!request.ContentType.StartsWith("multipart/form-data"))
                {
                    return;
                }

                var mpReader = new MultiPartReader(request);
                if (!mpReader.Parse())
                {
                    return;
                }

                hasBothFiles |= mpReader.Files.Any(f => f.FileName == "histogram.json")
                             && mpReader.Files.Any(f => f.FileName == treeFileName);
            };

            runner.Run(agent);

            Assert.True(hasBothFiles, $"No profile was sent with both a class histogram and a {treeFileName}");

            var histogramFiles = Directory.GetFiles(runner.Environment.PprofDir, "histogram_*.json");
            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, $"reference_tree_*.{treeExtension}");
            Assert.True(histogramFiles.Length > 0, "No class histogram files were generated");
            Assert.True(referenceTreeFiles.Length > 0, $"No reference tree .{treeExtension} files were generated");

            // Pair the files coming from the same export: they share the generated
            // <service>_<pid>_<uid> suffix built from the id of the profile they belong to.
            var histogramsById = histogramFiles.ToDictionary(f => GetSnapshotId(f, "histogram_"), f => f);
            var treesById = referenceTreeFiles.ToDictionary(f => GetSnapshotId(f, "reference_tree_"), f => f);

            // A histogram without a reference tree is expected: no tree file is emitted when
            // the traversal did not find any root.
            var snapshotIds = histogramsById.Keys.Intersect(treesById.Keys).ToList();
            Assert.True(snapshotIds.Count > 0, "No histogram/reference tree pair from the same export was found");

            var typesPerSnapshot = new List<(HashSet<string> HistogramTypes, HashSet<string> TreeTypes)>();

            foreach (var snapshotId in snapshotIds)
            {
                var histogramTypes = ReadHistogramTypeNames(histogramsById[snapshotId]);
                var tree = LoadAndValidateAllTrees(new[] { treesById[snapshotId] }).Single();
                var treeTypes = CollectHeapObjectTypeNames(tree);

                // The histogram is a superset by design: the tree only contains what the
                // traversal reached (interior pointer roots are skipped, depth is capped...).
                var onlyInTree = treeTypes.Except(histogramTypes).OrderBy(t => t).ToList();
                _output.WriteLine(
                    $"{snapshotId}: {treeTypes.Count} heap object types in the tree, {histogramTypes.Count} in the histogram, " +
                    $"{histogramTypes.Except(treeTypes).Count()} only in the histogram, " +
                    $"{onlyInTree.Count} only in the tree [{string.Join(", ", onlyInTree)}]");

                typesPerSnapshot.Add((histogramTypes, treeTypes));
            }

            // Checked on at least one snapshot rather than on all of them: the two artifacts
            // describe the same GC but are produced by different mechanisms -- the histogram
            // from the EventPipe bulk node events, the tree from the live traversal -- so a
            // dropped event batch can legitimately leave a traversed type out of a histogram.
            var consistentSnapshots = typesPerSnapshot.Count(snapshot => !snapshot.TreeTypes.Except(snapshot.HistogramTypes).Any());
            Assert.True(
                consistentSnapshots > 0,
                "Expected at least one snapshot whose reference tree types are all present in the class histogram");

            // Both files must name the scenario types identically. Checked on at least one
            // snapshot because the first one can be taken before they are allocated.
            var scenarioTypes = new[]
            {
                "Samples.Computer01.Order",
                "Samples.Computer01.Customer",
                "Samples.Computer01.Address",
                "Samples.Computer01.Product",
            };

            Assert.True(
                typesPerSnapshot.Any(snapshot => scenarioTypes.All(
                    typeName => snapshot.TreeTypes.Contains(typeName) && snapshot.HistogramTypes.Contains(typeName))),
                $"Expected at least one snapshot where both files report [{string.Join(", ", scenarioTypes)}]");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckSkipTraversalProducesNoReferenceTree(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 1");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotSkipTraversal, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasReferenceTree = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasReferenceTree |= HasAnyReferenceTree(ctx.Value.Request);
            };

            runner.Run(agent);

            Assert.False(hasReferenceTree, "No reference tree should be sent when skip-traversal is enabled");
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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

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
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for nested value type scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // The inline VT traversal via GCDesc + InlineVTCache should find both:
            // - NestedVtTarget (referenced by InnerStruct.ShallowRef, 1 level deep)
            // - DeepVtTarget (referenced by NestedInnerStruct.DeepRef inside InnerStruct.Nested, 2 levels deep)
            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "OuterHolder") &&
                    TypeExistsInTree(tree, "NestedVtTarget") &&
                    TypeExistsInTree(tree, "DeepVtTarget")),
                "Expected at least one snapshot to contain OuterHolder, NestedVtTarget, and DeepVtTarget types");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckInheritanceMultiSeriesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 21: Inheritance with mixed ref/non-ref fields.
            // DerivedAfterGap inherits from BaseWithGap which has a ref + non-ref field.
            // This produces multiple GCDesc series, validating that GCDesc correctly
            // discovers refs across series without MergeParentLayout.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 21");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for inheritance multi-series scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            // Both Ref1 and Ref2 targets should appear under DerivedAfterGap,
            // even though they are in separate GCDesc series (gap from NonRef field).
            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "DerivedAfterGap") &&
                    TypeExistsInTree(tree, "RefTarget")),
                "Expected at least one snapshot to contain DerivedAfterGap and RefTarget types");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckVtArrayWithRefsScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 22: Value type array with reference fields.
            // VtArrayElement[] where each element struct has a reference field.
            // Tests GCDesc negative series count (ValSerieItem encoding).
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 22");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for VT array scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "VtArrayTarget")),
                "Expected at least one snapshot to contain VtArrayTarget type (reachable via VT array element refs)");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckInlineVtNonGenericScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 23: Non-generic inline VT with reference field.
            // HolderWithInlineVt has an embedded EmbeddedStruct (ELEMENT_TYPE_VALUETYPE)
            // that contains a reference field. Tests InlineVTCache detection of non-generic VTs.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 23");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for inline VT non-generic scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "HolderWithInlineVt") &&
                    TypeExistsInTree(tree, "InlineVtTarget")),
                "Expected at least one snapshot to contain HolderWithInlineVt and InlineVtTarget types");
        }

        [TestAppFact("Samples.Computer01", new[] { "net10.0" })]
        public void CheckInlineVtGenericWithPrimitivesScenario(string appName, string framework, string appAssembly)
        {
            // Scenario 24: Generic inline VT with primitive type arguments.
            // HolderWithGenericVt embeds GenericVtWithPrimitiveArgs<int, string> whose
            // ELEMENT_TYPE_GENERICINST signature contains primitive args (ELEMENT_TYPE_I4,
            // ELEMENT_TYPE_STRING). Tests InlineVTCache primitive type arg resolution.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: $"--scenario {ReferenceChainScenarioNumber} --param 24");
            runner.TestDurationInSeconds = 30;
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TestHeapSnapshotInterval, "15");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotReferenceTreeFormat, "2"); // JSON

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            var referenceTreeFiles = Directory.GetFiles(runner.Environment.PprofDir, "reference_tree_*.json");
            Assert.True(referenceTreeFiles.Length > 0, "No reference tree JSON files were generated for inline VT generic with primitives scenario");

            var trees = LoadAndValidateAllTrees(referenceTreeFiles);

            Assert.True(
                trees.Any(tree =>
                    TypeExistsInTree(tree, "HolderWithGenericVt") &&
                    TypeExistsInTree(tree, "GenericVtTarget")),
                "Expected at least one snapshot to contain HolderWithGenericVt and GenericVtTarget types");
        }

        // ====================================================================
        // Static helpers
        // ====================================================================

        private static bool HasReferenceTreeFile(HttpListenerRequest request, string expectedFileName)
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

            return mpReader.Files.Any(f => f.FileName == expectedFileName);
        }

        private static bool HasAnyReferenceTree(HttpListenerRequest request)
        {
            return HasReferenceTreeFile(request, "reference_tree.json")
                || HasReferenceTreeFile(request, "reference_tree.bin");
        }

        /// <summary>
        /// Get the generated suffix identifying the export a file belongs to: every file
        /// attached to a profile is named &lt;stem&gt;_&lt;service&gt;_&lt;pid&gt;_&lt;profile id&gt;.
        /// </summary>
        private static string GetSnapshotId(string filePath, string stem)
        {
            return Path.GetFileNameWithoutExtension(filePath).Substring(stem.Length);
        }

        /// <summary>
        /// Read the type names of a class histogram, an array of ["TypeName", count, size].
        /// </summary>
        private static HashSet<string> ReadHistogramTypeNames(string histogramFile)
        {
            var types = new HashSet<string>();

            using var doc = JsonDocument.Parse(File.ReadAllText(histogramFile));
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                types.Add(entry[0].GetString());
            }

            return types;
        }

        /// <summary>
        /// Collect the names of the tree types that correspond to real heap objects.
        /// Nodes without any size are inline value types: they live inside their containing
        /// object so the GC never reports them as heap objects in the class histogram.
        /// </summary>
        private static HashSet<string> CollectHeapObjectTypeNames(ReferenceTree tree)
        {
            var types = new HashSet<string>();
            foreach (var root in tree.Roots)
            {
                CollectHeapObjectTypeNamesRecursive(root, tree, types);
            }

            return types;
        }

        private static void CollectHeapObjectTypeNamesRecursive(ReferenceNode node, ReferenceTree tree, HashSet<string> types)
        {
            // AssertTypeIndicesResolve has already ruled out an out-of-range index, so "?" here
            // can only be a name the profiler failed to resolve. Such a type cannot be matched
            // against the histogram, so it is left out rather than reported as a mismatch.
            var typeName = tree.GetTypeName(node.TypeIndex);
            if (node.TotalSize > 0 && typeName != "?")
            {
                types.Add(typeName);
            }

            foreach (var child in node.Children)
            {
                CollectHeapObjectTypeNamesRecursive(child, tree, types);
            }
        }

        /// <summary>
        /// Load all reference tree files (JSON or binary), validate their structure, and return the parsed trees.
        /// Structural validation runs on JSON files; chain validation is left to the caller.
        /// </summary>
        private List<ReferenceTree> LoadAndValidateAllTrees(string[] referenceTreeFiles)
        {
            var trees = new List<ReferenceTree>();
            foreach (var referenceTreeFile in referenceTreeFiles)
            {
                if (referenceTreeFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var jsonContent = File.ReadAllText(referenceTreeFile);
                    _output.WriteLine($"Reference tree JSON ({referenceTreeFile}): {jsonContent.Substring(0, Math.Min(2000, jsonContent.Length))}...");

                    ValidateReferenceTreeJsonStructure(jsonContent);
                    trees.Add(ReferenceTreeLoader.Load(jsonContent));
                }
                else
                {
                    _output.WriteLine($"Reference tree binary ({referenceTreeFile}): {new FileInfo(referenceTreeFile).Length} bytes");
                    trees.Add(ReferenceTreeLoader.LoadFromFile(referenceTreeFile));
                }
            }

            foreach (var tree in trees)
            {
                AssertTypeIndicesResolve(tree);
            }

            return trees;
        }

        /// <summary>
        /// Every node must point at a real type table slot. Without this check an out-of-range
        /// index would be indistinguishable from a name the profiler could not resolve:
        /// <see cref="ReferenceTree.GetTypeName"/> returns "?" for both.
        /// </summary>
        private static void AssertTypeIndicesResolve(ReferenceTree tree)
        {
            foreach (var root in tree.Roots)
            {
                AssertTypeIndicesResolveRecursive(root, tree);
            }
        }

        private static void AssertTypeIndicesResolveRecursive(ReferenceNode node, ReferenceTree tree)
        {
            Assert.InRange(node.TypeIndex, 0, tree.TypeTable.Count - 1);

            foreach (var child in node.Children)
            {
                AssertTypeIndicesResolveRecursive(child, tree);
            }
        }

        /// <summary>
        /// Validate the raw JSON structure (required fields, valid format).
        /// This checks the low-level JSON format; chain validation uses the model.
        /// </summary>
        private static void ValidateReferenceTreeJsonStructure(string jsonContent)
        {
            Assert.False(string.IsNullOrEmpty(jsonContent), "Reference tree JSON is empty");
            Assert.NotEqual("{}", jsonContent);

            using var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions { MaxDepth = 256 });

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

        /// <summary>
        /// Assert that two trees (from JSON and binary serialization of the same snapshot)
        /// contain the same reference chains. Compares by collecting all ancestor-descendant
        /// type-name pairs present in each tree and verifying both sets are equal.
        /// </summary>
        private static void AssertSameChains(ReferenceTree jsonTree, ReferenceTree binTree)
        {
            var jsonEdges = CollectTypeEdges(jsonTree);
            var binEdges = CollectTypeEdges(binTree);

            var onlyInJson = jsonEdges.Except(binEdges).ToList();
            var onlyInBin = binEdges.Except(jsonEdges).ToList();

            Assert.True(
                onlyInJson.Count == 0 && onlyInBin.Count == 0,
                $"Trees differ. Edges only in JSON: [{string.Join(", ", onlyInJson)}], only in binary: [{string.Join(", ", onlyInBin)}]");
        }

        /// <summary>
        /// Collect all direct parent->child type-name edges in the tree.
        /// </summary>
        private static HashSet<string> CollectTypeEdges(ReferenceTree tree)
        {
            var edges = new HashSet<string>();
            foreach (var root in tree.Roots)
            {
                CollectTypeEdgesRecursive(root, tree, edges);
            }

            return edges;
        }

        private static void CollectTypeEdgesRecursive(ReferenceNode node, ReferenceTree tree, HashSet<string> edges)
        {
            var parentName = tree.GetTypeName(node.TypeIndex);
            foreach (var child in node.Children)
            {
                var childName = tree.GetTypeName(child.TypeIndex);
                edges.Add($"{parentName}->{childName}");
                CollectTypeEdgesRecursive(child, tree, edges);
            }
        }
    }
}
