// <copyright file="ProfilerFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal enum ProfilerFeature
    {
        Cpu,
        WallTime,
        Exceptions,
        Allocations,
        LockContention,
        GarbageCollection,
        LiveHeap,
        Timeline,
        Network,
        CodeHotspots,
        Endpoint,
        TimestampsAsLabel,
        DebugInfo,
        GcThreadsCpuTime,
        Etw,
        HeapSnapshot,
        HeapSnapshotSkipTraversal,
        WaitHandleContention,
        ForceHttpSampling,
        ManagedActivation,
        ManagedCodeCache,
        MemoryFootprint,
    }
}
