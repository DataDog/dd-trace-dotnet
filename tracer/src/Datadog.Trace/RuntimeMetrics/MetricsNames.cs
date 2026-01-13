// <copyright file="MetricsNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.RuntimeMetrics
{
    internal static class MetricsNames
    {
        public const string ExceptionsCount = "runtime.dotnet.exceptions.count";

        public const string Gen0CollectionsCount = "runtime.dotnet.gc.count.gen0";
        public const string Gen1CollectionsCount = "runtime.dotnet.gc.count.gen1";
        public const string Gen2CollectionsCount = "runtime.dotnet.gc.count.gen2";

        public const string GcPauseTime = "runtime.dotnet.gc.pause_time";
        public const string GcMemoryLoad = "runtime.dotnet.gc.memory_load";

        public const string Gen0HeapSize = "runtime.dotnet.gc.size.gen0";
        public const string Gen1HeapSize = "runtime.dotnet.gc.size.gen1";
        public const string Gen2HeapSize = "runtime.dotnet.gc.size.gen2";
        public const string LohSize = "runtime.dotnet.gc.size.loh";
        public const string PohSize = "runtime.dotnet.gc.size.poh";

        public const string GcAllocatedBytes = "runtime.dotnet.gc.allocated_bytes";
        public const string GcFragmentationPercent = "runtime.dotnet.gc.fragmentation_percent";
        public const string GcTotalAvailableMemory = "runtime.dotnet.gc.total_available_memory";

        public const string ContentionTime = "runtime.dotnet.threads.contention_time";
        public const string ContentionCount = "runtime.dotnet.threads.contention_count";

        public const string ThreadPoolWorkersCount = "runtime.dotnet.threads.workers_count";

        public const string ThreadsCount = "runtime.dotnet.threads.count";

        public const string ThreadsQueueLength = "runtime.dotnet.threads.queue_length";
        public const string ThreadsAvailableWorkers = "runtime.dotnet.threads.available_worker_threads";
        public const string ThreadsAvailableCompletionPorts = "runtime.dotnet.threads.available_completion_port_threads";
        public const string ThreadsCompletedWorkItems = "runtime.dotnet.threads.completed_work_items";
        public const string ThreadsActiveTimers = "runtime.dotnet.threads.active_timers";

        public const string CommittedMemory = "runtime.dotnet.mem.committed";

        public const string CpuUserTime = "runtime.dotnet.cpu.user";
        public const string CpuSystemTime = "runtime.dotnet.cpu.system";
        public const string CpuPercentage = "runtime.dotnet.cpu.percent";

        public const string JitCompiledILBytes = "runtime.dotnet.jit.compiled_il_bytes";
        public const string JitCompiledMethods = "runtime.dotnet.jit.compiled_methods";
        public const string JitCompilationTime = "runtime.dotnet.jit.compilation_time";

        public const string AspNetCoreCurrentRequests = "runtime.dotnet.aspnetcore.requests.current";
        public const string AspNetCoreFailedRequests = "runtime.dotnet.aspnetcore.requests.failed";
        public const string AspNetCoreTotalRequests = "runtime.dotnet.aspnetcore.requests.total";
        public const string AspNetCoreRequestQueueLength = "runtime.dotnet.aspnetcore.requests.queue_length";

        public const string AspNetCoreCurrentConnections = "runtime.dotnet.aspnetcore.connections.current";
        public const string AspNetCoreConnectionQueueLength = "runtime.dotnet.aspnetcore.connections.queue_length";
        public const string AspNetCoreTotalConnections = "runtime.dotnet.aspnetcore.connections.total";
    }
}
