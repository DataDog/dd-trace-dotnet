// <copyright file="EnableOnlyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class EnableOnlyTests
    {
        [Fact]
        public void EnableOnly_Cpu_SetsEveryProfilerToggle()
        {
            var environment = new EnvironmentHelper("net8.0", enableTracer: false, enableProfiler: true);
            environment.EnableOnly(ProfilerFeature.Cpu);

            var expected = new Dictionary<string, string>
            {
                [EnvironmentVariables.CpuProfilerEnabled] = "1",
                [EnvironmentVariables.WallTimeProfilerEnabled] = "0",
                [EnvironmentVariables.ExceptionProfilerEnabled] = "0",
                [EnvironmentVariables.AllocationProfilerEnabled] = "0",
                [EnvironmentVariables.ContentionProfilerEnabled] = "0",
                [EnvironmentVariables.GarbageCollectionProfilerEnabled] = "0",
                [EnvironmentVariables.LiveHeapProfilerEnabled] = "0",
                [EnvironmentVariables.ThreadLifetimeEnabled] = "0",
                [EnvironmentVariables.HttpProfilingEnabled] = "0",
                [EnvironmentVariables.CodeHotSpotsEnable] = "0",
                [EnvironmentVariables.EndpointProfilerEnabled] = "0",
                [EnvironmentVariables.TimestampsAsLabelEnabled] = "0",
                [EnvironmentVariables.DebugInfoEnabled] = "0",
                [EnvironmentVariables.GcThreadsCpuTimeEnabled] = "0",
                [EnvironmentVariables.EtwEnabled] = "0",
                [EnvironmentVariables.HeapSnapshotEnabled] = "0",
                [EnvironmentVariables.HeapSnapshotSkipTraversal] = "0",
                [EnvironmentVariables.WaitHandleContentionProfilingEnabled] = "0",
                [EnvironmentVariables.ForceHttpSamplingEnabled] = "0",
                [EnvironmentVariables.ManagedActivationEnabled] = "0",
                [EnvironmentVariables.UseManagedCodeCache] = "0",
                [EnvironmentVariables.MemoryFootprintEnabled] = "0",
            };

            Enum.GetValues(typeof(ProfilerFeature)).Length.Should().Be(expected.Count);
            foreach (var entry in expected)
            {
                environment.CustomEnvironmentVariables[entry.Key].Should().Be(entry.Value, "variable {0}", entry.Key);
            }
        }

        [Fact]
        public void EnableOnly_SeveralFeatures_EnablesOnlyThose()
        {
            var environment = new EnvironmentHelper("net8.0", enableTracer: false, enableProfiler: true);
            environment.EnableOnly(ProfilerFeature.WallTime, ProfilerFeature.Network);

            environment.CustomEnvironmentVariables[EnvironmentVariables.WallTimeProfilerEnabled].Should().Be("1");
            environment.CustomEnvironmentVariables[EnvironmentVariables.HttpProfilingEnabled].Should().Be("1");
            environment.CustomEnvironmentVariables[EnvironmentVariables.CpuProfilerEnabled].Should().Be("0");
            environment.CustomEnvironmentVariables[EnvironmentVariables.ExceptionProfilerEnabled].Should().Be("0");
            environment.CustomEnvironmentVariables[EnvironmentVariables.GarbageCollectionProfilerEnabled].Should().Be("0");
            environment.CustomEnvironmentVariables[EnvironmentVariables.ContentionProfilerEnabled].Should().Be("0");
            environment.CustomEnvironmentVariables[EnvironmentVariables.GcThreadsCpuTimeEnabled].Should().Be("0");
            environment.CustomEnvironmentVariables[EnvironmentVariables.ThreadLifetimeEnabled].Should().Be("0");
        }
    }
}
