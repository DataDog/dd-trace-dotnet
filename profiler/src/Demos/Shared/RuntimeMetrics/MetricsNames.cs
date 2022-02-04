// <copyright file="MetricsNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.RuntimeMetrics
{
    // Subset from Tracer with different prefix to avoid conflict
    // when used together
    internal static class MetricsNames
    {
        public const string ExceptionsCount = "metric.runtime.dotnet.exceptions.count";

        public const string Gen0CollectionsCount = "metric.runtime.dotnet.gc.count.gen0";
        public const string Gen1CollectionsCount = "metric.runtime.dotnet.gc.count.gen1";
        public const string Gen2CollectionsCount = "metric.runtime.dotnet.gc.count.gen2";
        public const string Gen2CompactingCollectionsCount = "metric.runtime.dotnet.gc.count.compacting_gen2";

        public const string GcPauseTime = "metric.runtime.dotnet.gc.pause_time";
        public const string GcMemoryLoad = "metric.runtime.dotnet.gc.memory_load";

        public const string Gen0HeapSize = "metric.runtime.dotnet.gc.size.gen0";
        public const string Gen1HeapSize = "metric.runtime.dotnet.gc.size.gen1";
        public const string Gen2HeapSize = "metric.runtime.dotnet.gc.size.gen2";
        public const string LohSize = "metric.runtime.dotnet.gc.size.loh";

        public const string ContentionTime = "metric.runtime.dotnet.threads.contention_time";
        public const string ContentionCount = "metric.runtime.dotnet.threads.contention_count";

        public const string ProcessorTime = "metric.runtime.process.processor_time";
        public const string PrivateBytes = "metric.runtime.process.private_bytes";

        public const string AspNetCoreTotalRequests = "metric.runtime.dotnet.aspnetcore.requests.total";
    }
}
