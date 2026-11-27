// <copyright file="HeapSnapshotTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;
using System.Text.Json;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class HeapSnapshotTest
    {
        private readonly ITestOutputHelper _output;

        public HeapSnapshotTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact(TestApplication.Computer01, TestFramework.Net6, TestFramework.Net8, TestFramework.Net10, TestFramework.Net11)]
        public void CheckHeapSnapshot(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: "--scenario 13");
            // runner.Environment.SetVariable(EnvironmentVariables.ManagedActivationEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            // Use the smallest non-zero threshold to exercise first-snapshot scheduling through the memory-pressure path.
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "1");

            // check the histogram.json files are sent with the profiles
            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasHeapSnapshot = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasHeapSnapshot |= HeapSnapshotHelper.HasHeapSnapshot(ctx.Value.Request);
            };

            runner.Run(agent);

            // uncomment to debug multipart http request
            // Thread.Sleep(10000000);

            hasHeapSnapshot.Should().BeTrue();

            // check the heap snapshots content is correct
            var heapSnapshotFiles = System.IO.Directory.GetFiles(runner.Environment.PprofDir, "histogram_*.json");
            heapSnapshotFiles.Length.Should().BePositive();
            foreach (var heapSnapshotFile in heapSnapshotFiles)
            {
                var containsString = false;
                var containsRuntimeTypes = false;
                var containsOutofMemoryException = false;
                var containsStackOverflowException = false;
                var containsComputer01Scenario = false;

                // get the heap snapshot from the local json file
                var jsonContent = System.IO.File.ReadAllText(heapSnapshotFile);
                var doc = JsonDocument.Parse(jsonContent);
                _ = doc.RootElement.EnumerateArray().All(element =>
                {
                    var kvp = element.EnumerateArray().ToArray();
                    var typeName = kvp[0].ToString();
                    if (typeName == "System.String")
                    {
                        containsString = true;
                    }

                    if (typeName.StartsWith("System.Runtime"))
                    {
                        containsRuntimeTypes = true;
                    }

                    if (typeName.StartsWith("System.OutOfMemoryException"))
                    {
                        containsOutofMemoryException = true;
                    }

                    if (typeName.StartsWith("System.StackOverflowException"))
                    {
                        containsStackOverflowException = true;
                    }

                    if (typeName.StartsWith("Samples.Computer01.Scenario"))
                    {
                        containsComputer01Scenario = true;
                    }

                    return true;
                });

                containsString.Should().BeTrue("heap snapshot file {0} should contain System.String", heapSnapshotFile);
                containsRuntimeTypes.Should().BeTrue("heap snapshot file {0} should contain System.Runtime", heapSnapshotFile);
                containsStackOverflowException.Should().BeTrue("heap snapshot file {0} should contain System.StackOverflowException", heapSnapshotFile);
                containsOutofMemoryException.Should().BeTrue("heap snapshot file {0} should contain System.OutOfMemoryException", heapSnapshotFile);
                containsComputer01Scenario.Should().BeTrue("heap snapshot file {0} should contain Samples.Computer01.Scenario", heapSnapshotFile);
            }
        }
    }
}
