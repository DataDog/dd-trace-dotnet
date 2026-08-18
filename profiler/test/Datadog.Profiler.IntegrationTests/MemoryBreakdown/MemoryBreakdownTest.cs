// <copyright file="MemoryBreakdownTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.MemoryBreakdown
{
    public class MemoryBreakdownTest
    {
        private const string Scenario = "--scenario 13";

        private const string MemoryBreakdownSampleType = "memory-breakdown";
        private const string MemorySourceLabel = "memory_source";
        private const string RegionKindLabel = "region_kind";
        private const string RegionGroupLabel = "region_group";
        private const string ModuleLabel = "module";
        private const string GcGenerationLabel = "gc generation";
        private const string AppDomainNameLabel = "appdomain name";
        private const string AppDomainProcessIdLabel = "appdomain process id";

        private readonly ITestOutputHelper _output;

        public MemoryBreakdownTest(ITestOutputHelper output)
        {
            _output = output;
        }

        // .NET 6/8/10 go through the legacy DAC backend; .NET 11 exercises the cDAC backend.
        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0", "net11.0" })]
        public void CheckMemoryBreakdownOnWindows(string appName, string framework, string appAssembly)
        {
            if (!EnvironmentHelper.IsRunningOnWindows())
            {
                return;
            }

            var pprofDir = RunScenario(appName, framework, appAssembly);

            // On Windows memory-breakdown contains MEM_COMMIT bytes.
            AssertMemoryValueTypesDeclared(pprofDir);
            AssertValueTypePopulated(pprofDir, MemoryBreakdownSampleType);
            CheckMemoryBreakdown(pprofDir);

            // At least one CLR module and the app assembly must show up as image samples.
            AssertModulePresent(pprofDir, m => m.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase) || m.Equals("clr.dll", StringComparison.OrdinalIgnoreCase));
        }

        // .NET 6/8/10 go through the legacy DAC backend; .NET 11 exercises the cDAC backend.
        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0", "net11.0" })]
        public void CheckMemoryBreakdownOnLinux(string appName, string framework, string appAssembly)
        {
            if (EnvironmentHelper.IsRunningOnWindows())
            {
                return;
            }

            var pprofDir = RunScenario(appName, framework, appAssembly);

            // On Linux memory-breakdown contains RSS bytes from /proc/self/smaps.
            AssertMemoryValueTypesDeclared(pprofDir);
            AssertValueTypePopulated(pprofDir, MemoryBreakdownSampleType);
            CheckMemoryBreakdown(pprofDir);

            // The CLR shared object must show up as an image sample.
            AssertModulePresent(pprofDir, m => m.Equals("libcoreclr.so", StringComparison.OrdinalIgnoreCase));
        }

        private string RunScenario(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario);
            runner.Environment.SetVariable(EnvironmentVariables.MemoryBreakdownEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0, "No profile was sent to the agent");
            return runner.Environment.PprofDir;
        }

        private static void AssertMemoryValueTypesDeclared(string pprofDir)
        {
            AssertValueTypeDeclared(pprofDir, MemoryBreakdownSampleType, "bytes");
            AssertValueTypeNotDeclared(pprofDir, "committed");
            AssertValueTypeNotDeclared(pprofDir, "rss");
        }

        private static void AssertValueTypeDeclared(string pprofDir, string valueType, string unit)
        {
            bool present = SamplesHelper.GetProfiles(pprofDir)
                                        .Any(p => p.SampleTypesWithUnits().Any(t => t.Type == valueType && t.Unit == unit));
            Assert.True(present, $"No profile declares the '{valueType}/{unit}' sample value type");
        }

        private static void AssertValueTypeNotDeclared(string pprofDir, string valueType)
        {
            bool present = SamplesHelper.GetProfiles(pprofDir)
                                        .Any(p => p.SampleTypesWithUnits().Any(t => t.Type == valueType));
            Assert.False(present, $"A profile unexpectedly declares the legacy '{valueType}' sample value type");
        }

        private static void AssertValueTypePopulated(string pprofDir, string valueType)
        {
            var samples = SamplesHelper.GetSamples(pprofDir, valueType).ToList();
            Assert.True(samples.Count > 0, $"No sample carries a non-zero '{valueType}' value");
        }

        // Shared group/kind/generation assertions for the platform-specific memory value.
        private void CheckMemoryBreakdown(string pprofDir)
        {
            var samples = SamplesHelper.GetSamples(pprofDir, MemoryBreakdownSampleType).ToList();
            Assert.True(samples.Count > 0, $"No '{MemoryBreakdownSampleType}' sample found");

            var groupFunctions = new HashSet<string>();
            var memorySources = new HashSet<string>();
            bool sawManagedGenerationLeaf = false;
            bool sawClrNativeGroup = false;

            var expectedGenerations = new HashSet<string> { "0", "1", "2", "3", "4" };
            var expectedClrGroups = new HashSet<string> { "Code", "Loader", "Virtual Stub Dispatch" };

            foreach (var (stack, labels, _) in samples)
            {
                // every memory sample is rooted at "Process Memory"
                var functions = Enumerable.Range(0, stack.FramesCount).Select(i => stack[i].Function).ToList();
                Assert.Contains("Process Memory", functions);

                foreach (var fn in functions)
                {
                    groupFunctions.Add(fn);
                }

                var source = GetLabel(labels, MemorySourceLabel);
                Assert.False(string.IsNullOrEmpty(source), "memory sample is missing the memory_source label");
                memorySources.Add(source);

                // protection must never leak onto a sample (would re-fragment collapsed modules).
                Assert.DoesNotContain(labels, l => l.Name == "protection");
                Assert.DoesNotContain(labels, l => l.Name == AppDomainNameLabel || l.Name == AppDomainProcessIdLabel);

                if (source == "managed")
                {
                    var generation = GetLabel(labels, GcGenerationLabel);
                    if (!string.IsNullOrEmpty(generation))
                    {
                        Assert.Contains(generation, expectedGenerations);
                    }

                    if (functions.Any(f => f == "gen0" || f == "gen1" || f == "gen2" || f == "LOH" || f == "POH"))
                    {
                        sawManagedGenerationLeaf = true;
                    }
                }

                if (source == "clr-native")
                {
                    var group = GetLabel(labels, RegionGroupLabel);
                    if (!string.IsNullOrEmpty(group) && expectedClrGroups.Contains(group))
                    {
                        sawClrNativeGroup = true;
                    }

                    Assert.False(string.IsNullOrEmpty(GetLabel(labels, RegionKindLabel)), "clr-native sample is missing region_kind");
                }
            }

            // Expected top-level groups.
            Assert.Contains("Managed Heap (GC)", groupFunctions);
            Assert.Contains("CLR Native", groupFunctions);
            Assert.Contains("Modules (Images)", groupFunctions);

            // Managed detail present.
            Assert.True(sawManagedGenerationLeaf, "No managed heap generation (gen0/1/2/LOH/POH) leaf frame found");
            Assert.True(sawClrNativeGroup, "No CLR Native sample carried an expected region_group");

            // Reconciliation sanity: both CLR and OS halves are attributed.
            Assert.True(
                memorySources.Overlaps(new[] { "managed", "clr-native" }),
                "No CLR-attributed (managed/clr-native) memory sample found");
            Assert.True(
                memorySources.Overlaps(new[] { "image", "private", "stack", "mapped-file", "reserved" }),
                "No OS-attributed memory sample found");
        }

        private void AssertModulePresent(string pprofDir, Func<string, bool> predicate)
        {
            var modules = new List<string>();
            foreach (var profile in SamplesHelper.GetProfiles(pprofDir))
            {
                var sampleTypeIndex = Array.IndexOf(profile.SampleType(), MemoryBreakdownSampleType);
                if (sampleTypeIndex == -1)
                {
                    continue;
                }

                var profileModules = profile.Sample
                                            .Where(s => s.Value[sampleTypeIndex] != 0)
                                            .Select(s => GetLabel(s.Labels(profile).ToArray(), ModuleLabel))
                                            .Where(m => !string.IsNullOrEmpty(m))
                                            .ToList();
                modules.AddRange(profileModules);

                // Each module value must appear on exactly one sample per profile (protection runs collapsed).
                foreach (var group in profileModules.GroupBy(m => m))
                {
                    Assert.True(group.Count() == 1, $"module '{group.Key}' appears on {group.Count()} samples in one profile (should be collapsed to one)");
                }
            }

            Assert.True(modules.Any(predicate), "No expected module was found among the image samples");
        }

        private static string GetLabel(PprofHelper.Label[] labels, string name)
        {
            foreach (var label in labels)
            {
                if (label.Name == name)
                {
                    return label.Value;
                }
            }

            return null;
        }
    }
}
