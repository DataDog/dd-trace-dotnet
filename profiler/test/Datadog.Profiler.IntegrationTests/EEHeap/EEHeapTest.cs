// <copyright file="EEHeapTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.EEHeap
{
    public class EEHeapTest
    {
        private readonly ITestOutputHelper _output;

        public EEHeapTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // .NET 6/8/10 go through the legacy DAC (ISOSDacInterface) backend.
        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckEEHeapWithDacBackend(string appName, string framework, string appAssembly)
        {
            RunEEHeapScenario(appName, framework, appAssembly, expectedSource: "DAC");
        }

        // .NET 11+ uses the cDAC (contract descriptor) backend.
        [TestAppFact("Samples.Computer01", new[] { "net11.0" })]
        public void CheckEEHeapWithCdacBackend(string appName, string framework, string appAssembly)
        {
            RunEEHeapScenario(appName, framework, appAssembly, expectedSource: "cDAC");
        }

        private void RunEEHeapScenario(string appName, string framework, string appAssembly, string expectedSource)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 13");
            runner.Environment.SetVariable(EnvironmentVariables.EEHeapEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasEEHeap = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasEEHeap |= EEHeapHelper.HasEEHeap(ctx.Value.Request);
            };

            runner.Run(agent);

            Assert.True(hasEEHeap, "No profile request carried an eeheap.json file");

            // check the eeheap content saved locally next to the pprof files
            var eeHeapFiles = System.IO.Directory.GetFiles(runner.Environment.PprofDir, "eeheap_*.json");
            Assert.True(eeHeapFiles.Length > 0, "No eeheap_*.json file was written to the output directory");

            foreach (var eeHeapFile in eeHeapFiles)
            {
                var jsonContent = System.IO.File.ReadAllText(eeHeapFile);
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                var source = root.GetProperty("source").GetString();
                Assert.Equal(expectedSource, source, ignoreCase: true);

                var heaps = root.GetProperty("heaps");
                Assert.True(heaps.GetArrayLength() > 0, $"eeheap file {eeHeapFile} contains no heap entries");

                bool anyGenerationTagged = false;
                foreach (var heap in heaps.EnumerateArray())
                {
                    var address = heap.GetProperty("address").GetString();
                    Assert.False(string.IsNullOrEmpty(address), "heap entry has empty address");
                    Assert.StartsWith("0x", address);

                    // size must parse as an unsigned integer (throws otherwise)
                    Assert.True(heap.GetProperty("size").TryGetUInt64(out _), "heap entry size is not a valid integer");

                    // committed is always emitted and must be a valid unsigned integer <= size
                    Assert.True(heap.TryGetProperty("committed", out var committedElement), "heap entry is missing committed");
                    Assert.True(committedElement.TryGetUInt64(out var committed), "heap entry committed is not a valid integer");
                    heap.GetProperty("size").TryGetUInt64(out var size);
                    Assert.True(committed <= size, "heap entry committed exceeds its reserved size");

                    var kind = heap.GetProperty("kind").GetString();
                    Assert.False(string.IsNullOrEmpty(kind), "heap entry has empty kind");

                    // group is always emitted and derived from kind (a high-level category).
                    var group = heap.GetProperty("group").GetString();
                    Assert.False(string.IsNullOrEmpty(group), "heap entry has empty group");

                    var state = heap.GetProperty("state").GetString();
                    Assert.False(string.IsNullOrEmpty(state), "heap entry has empty state");

                    if (heap.TryGetProperty("generation", out var generationElement)
                        && generationElement.TryGetInt32(out var generation)
                        && generation >= 0)
                    {
                        anyGenerationTagged = true;
                    }
                }

                // Both backends now emit per-generation GC segments, so at least one entry carries a generation.
                Assert.True(anyGenerationTagged, $"eeheap file {eeHeapFile} has no per-generation GC segment");
            }
        }
    }
}
