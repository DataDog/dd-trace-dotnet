// <copyright file="AllocationsProfilerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Allocations
{
    public class AllocationsProfilerTest
    {
        private const string ScenarioGenerics = "--scenario 9";
        private const string ScenarioMeasureAllocation = "--scenario 16";
        private const string ScenarioWithoutGC = "--scenario 25 --threads 2 --param 1000";

        private readonly ITestOutputHelper _output;

        public AllocationsProfilerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ShouldGetAllocationSamples(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            EnvironmentHelper.DisableDefaultProfilers(runner);

            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "1");

            // only allocation profiler enabled so should only see the 2 related values per sample
            CheckAllocationProfiles(runner);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ShouldAllocationProfilerBeDisabledByDefault(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);
            EnvironmentHelper.DisableDefaultProfilers(runner);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // On alpine, this check is flaky.
            // Disable it on alpine for now
            if (!EnvironmentHelper.IsAlpine)
            {
                Assert.True(agent.NbCallsOnProfilingEndpoint == 0);
            }

            // no profiler enabled so should not see any sample
            Assert.Equal(0, SamplesHelper.GetSamplesCount(runner.Environment.PprofDir));
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void ExplicitlyDisableAllocationProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioGenerics);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            // only walltime profiler enabled so should see 1 value per sample
            SamplesHelper.CheckSamplesValueCount(runner.Environment.PprofDir, 1);
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void MeasureAllocations(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioMeasureAllocation);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var allocationSamples = ExtractAllocationSamples(runner.Environment.PprofDir).ToArray();
            allocationSamples.Should().NotBeEmpty();

            // this test always succeeds: it is used to display the differences between sampled and real allocations
            Dictionary<string, AllocStats> profiledAllocations = GetProfiledAllocations(allocationSamples);
            Dictionary<string, AllocStats> realAllocations = GetRealAllocations(runner.ProcessOutput);

            runner.XUnitLogger.WriteLine("Comparing allocations");
            runner.XUnitLogger.WriteLine("-------------------------------------------------------");
            runner.XUnitLogger.WriteLine("      Count          Size Type");
            runner.XUnitLogger.WriteLine("-------------------------------------------------------");
            foreach (var allocation in profiledAllocations)
            {
                var allocStats = allocation.Value;
                var type = allocation.Key;
                int pos = type.LastIndexOf('.');
                if (pos != -1)
                {
                    type = type.Substring(pos + 1);
                }

                // TODO: dump real and profiled count/size
                if (!realAllocations.TryGetValue(type, out var stats))
                {
                    continue;
                }

                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"{allocStats.Count,11} {allocStats.Size,13} {type}");
                builder.AppendLine($"{stats.Count,11} {stats.Size,13}");
                runner.XUnitLogger.WriteLine(builder.ToString());
            }
        }

        [TestAppFact("Samples.Computer01", new[] { "net462" })]
        public void ShouldGetAllocationSamplesViaEtw(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioWithoutGC);

            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.AllocationProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.EtwEnabled, "1");
            Guid guid = Guid.NewGuid();
            runner.Environment.SetVariable(EnvironmentVariables.EtwReplayEndpoint, "\\\\.\\pipe\\DD_ETW_TEST_AGENT-" + guid);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            if (IntPtr.Size == 4)
            {
                // 32-bit
                agent.StartEtwProxy(runner.XUnitLogger, "DD_ETW_TEST_AGENT-" + guid, "Allocations\\allocations-32.bevents");
            }
            else
            {
                // 64-bit
                agent.StartEtwProxy(runner.XUnitLogger, "DD_ETW_TEST_AGENT-" + guid, "Allocations\\allocations-64.bevents");
            }

            int eventsCount = 0;
            agent.EventsSent += (sender, e) => eventsCount = e.Value;
            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var allocationSamples = ExtractAllocationSamples(runner.Environment.PprofDir).ToArray();
            allocationSamples.Should().NotBeEmpty();
        }

        private static Dictionary<string, AllocStats> GetProfiledAllocations(IEnumerable<(string Type, long Count, long Size, StackTrace Stacktrace)> allocations)
        {
            var profiledAllocations = new Dictionary<string, AllocStats>();

            foreach (var allocation in allocations)
            {
                if (!profiledAllocations.TryGetValue(allocation.Type, out var stats))
                {
                    stats = new AllocStats()
                    {
                        Count = 0,
                        Size = 0
                    };
                    profiledAllocations.Add(allocation.Type, stats);
                }

                stats.Count += (int)allocation.Count;
                stats.Size += allocation.Size;
            }

            return profiledAllocations;
        }

        private static Dictionary<string, AllocStats> GetRealAllocations(string output)
        {
            const string startToken = "Allocations start";
            const string endToken = "Allocations end";

            var realAllocations = new Dictionary<string, AllocStats>();
            if (output == null)
            {
                return realAllocations;
            }

            // look for the following sections with type=count,size
            /*
                Allocations start
                Object2=100000,200000
                Object4=100000,400000
                Object8=100000,800000
                Object16=100000,1600000
                Object32=100000,3200000
                Object64=100000,6400000
                Object128=100000,12800000
                Allocations end
            */
            int pos = 0;
            int next = 0;
            int end = 0;
            while (true)
            {
                // look for an allocation block
                next = output.IndexOf(startToken, pos);
                if (next == -1)
                {
                    break;
                }

                next += startToken.Length;
                next = GotoEoL(output, next);
                if (next == -1)
                {
                    break;
                }

                pos = next + 1; // point to the beginning of the first allocation stats

                // look for the end of the allocations block
                end = output.IndexOf(endToken, pos);
                if (end == -1)
                {
                    break;
                }

                // extract line by line the allocation stats from this block
                int eol = 0;
                while (true)
                {
                    next = GotoEoL(output, pos);
                    if (next == -1)
                    {
                        break;
                    }

                    // handle Windows (\r\n) and Linux (\n) cases
                    if (output[next - 1] == '\r')
                    {
                        eol = next - 1;
                    }
                    else
                    {
                        eol = next;
                    }

                    // extract type, count and size
                    //   Object2=13627,27254
                    var line = output.AsSpan(pos, eol - pos);

                    // get type name
                    var current = output.IndexOf('=', pos);
                    if (current == -1)
                    {
                        next = -1;
                        break;
                    }

                    var name = output.Substring(pos, current - pos);
                    pos = current + 1;
                    if (pos >= end)
                    {
                        next = -1;
                        break;
                    }

                    // get count
                    current = output.IndexOf(',', pos);
                    if (current == -1)
                    {
                        next = -1;
                        break;
                    }

                    var text = output.AsSpan(pos, current - pos);
                    var count = int.Parse(text);

                    pos = current + 1;
                    if (pos >= end)
                    {
                        next = -1;
                        break;
                    }

                    // get size
                    text = output.AsSpan(pos, eol - pos);
                    var size = int.Parse(text);

                    // add the stats
                    AllocStats stats = null;
                    if (!realAllocations.TryGetValue(name, out stats))
                    {
                        stats = new AllocStats();
                        stats.Count = 0;
                        stats.Size = 0;
                        realAllocations.Add(name, stats);
                    }

                    stats.Count += count;
                    stats.Size += size;

                    // goto the next line (or the end token)
                    pos = next + 1;

                    // check for the last stats
                    if (next == (end - 1))
                    {
                        break;
                    }
                }

                if (next == -1)
                {
                    break;
                }
            }

            return realAllocations;
        }

        private static int GotoEoL(string text, int pos)
        {
            var next = text.IndexOf('\n', pos);
            return next;
        }

        private static IEnumerable<(string Type, long Count, long Size, StackTrace Stacktrace)> ExtractAllocationSamples(string directory)
        {
            static IEnumerable<(string Type, long Count, long Size, StackTrace Stacktrace, long Time)> GetAllocationSamples(string directory)
            {
                foreach (var profile in SamplesHelper.GetProfiles(directory))
                {
                    foreach (var sample in profile.Sample)
                    {
                        var count = sample.Value[0];
                        if (count == 0)
                        {
                            continue;
                        }

                        long size = 0;
                        // no size available for .NET Framework
                        if (sample.Value.Count > 1)
                        {
                           size = sample.Value[1];
                        }

                        var labels = sample.Labels(profile).ToArray();

                        // from Sample.cpp
                        var type = labels.Single(l => l.Name == "allocation class").Value;

                        yield return (type, count, size, sample.StackTrace(profile), profile.TimeNanos);
                    }
                }
            }

            return GetAllocationSamples(directory)
                .OrderBy(s => s.Time)
                .Select(s => (s.Type, s.Count, s.Size, s.Stacktrace));
        }

        private void CheckAllocationProfiles(TestApplicationRunner runner)
        {
            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var allocationSamples = ExtractAllocationSamples(runner.Environment.PprofDir).ToArray();
            allocationSamples.Should().NotBeEmpty();

            // expected allocations:
            //  System.Byte[][,]
            //  System.Byte[][]
            //  System.Byte[]
            //  Generic<System.Int32>[,]
            //  Generic<System.Int32>[]
            //  Generic<System.Int32>
            //
            bool matrixOfArrayOfBytesFound = false;
            bool jaggedArrayOfBytesFound = false;
            bool arrayOfBytesFound = false;
            bool matrixOfGenericsFound = false;
            bool arrayOfGenericsFound = false;
            bool genericElementFound = false;

            foreach (var sample in allocationSamples)
            {
                if (sample.Type.CompareTo("System.Byte[][,]") == 0)
                {
                    matrixOfArrayOfBytesFound = true;
                }
                else
                if (sample.Type.CompareTo("System.Byte[][]") == 0)
                {
                    jaggedArrayOfBytesFound = true;
                }
                else
                if (sample.Type.CompareTo("System.Byte[]") == 0)
                {
                    arrayOfBytesFound = true;
                }
                else
                if (sample.Type.CompareTo("Samples.Computer01.Generic<System.Int32>[,]") == 0)
                {
                    matrixOfGenericsFound = true;
                }
                else
                if (sample.Type.CompareTo("Samples.Computer01.Generic<System.Int32>[]") == 0)
                {
                    arrayOfGenericsFound = true;
                }
                else
                if (sample.Type.CompareTo("Samples.Computer01.Generic<System.Int32>") == 0)
                {
                    genericElementFound = true;
                }
            }

            Assert.True(matrixOfArrayOfBytesFound);
            Assert.True(jaggedArrayOfBytesFound);
            Assert.True(arrayOfBytesFound);
            Assert.True(genericElementFound);
            Assert.True(arrayOfGenericsFound);
            Assert.True(matrixOfGenericsFound);
        }

        internal class AllocStats
        {
            public int Count { get; set; }
            public long Size { get; set; }
        }
    }
}
