// <copyright file="EnvironmentVariables.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class EnvironmentVariables
    {
        public const string ProfilerEnabled = "DD_PROFILING_ENABLED";
        public const string ProfilingLogDir = "DD_TRACE_LOG_DIRECTORY";
        public const string ProfilingPprofDir = "DD_INTERNAL_PROFILING_OUTPUT_DIR";
        public const string CodeHotSpotsEnable = "DD_PROFILING_CODEHOTSPOTS_ENABLED";
        public const string CpuProfilerEnabled = "DD_PROFILING_CPU_ENABLED";
        public const string WallTimeProfilerEnabled = "DD_PROFILING_WALLTIME_ENABLED";
        public const string ExceptionProfilerEnabled = "DD_PROFILING_EXCEPTION_ENABLED";
        public const string ExceptionSampleLimit = "DD_INTERNAL_PROFILING_EXCEPTION_SAMPLE_LIMIT";
        public const string AllocationProfilerEnabled = "DD_PROFILING_ALLOCATION_ENABLED";
        public const string ContentionProfilerEnabled = "DD_PROFILING_LOCK_ENABLED";
        public const string LiveHeapProfilerEnabled = "DD_PROFILING_HEAP_ENABLED";
        public const string EndpointProfilerEnabled = "DD_PROFILING_ENDPOINT_COLLECTION_ENABLED";
        public const string NamedPipeName = "DD_TRACE_PIPE_NAME";
        public const string TimestampsAsLabelEnabled = "DD_INTERNAL_PROFILING_TIMESTAMPS_AS_LABEL_ENABLED";
        public const string GarbageCollectionProfilerEnabled = "DD_PROFILING_GC_ENABLED";
        public const string UseBacktrace2 = "DD_INTERNAL_USE_BACKTRACE2";
        public const string DebugInfoEnabled = "DD_INTERNAL_PROFILING_DEBUG_INFO_ENABLED";
        public const string GcThreadsCpuTimeEnabled = "DD_INTERNAL_GC_THREADS_CPUTIME_ENABLED";
        public const string ThreadLifetimeEnabled = "DD_INTERNAL_THREAD_LIFETIME_ENABLED";
        public const string SsiDeployed = "DD_INJECTION_ENABLED";
        public const string EtwEnabled = "DD_INTERNAL_PROFILING_ETW_ENABLED";
        public const string CpuProfilerType = "DD_INTERNAL_CPU_PROFILER_TYPE";
        public const string CpuProfilingInterval = "DD_INTERNAL_CPU_PROFILING_INTERVAL";
        public const string SsiShortLivedThreshold = "DD_INTERNAL_PROFILING_LONG_LIVED_THRESHOLD";
        public const string TelemetryToDiskEnabled = "DD_INTERNAL_PROFILING_TELEMETRY_TO_DISK_ENABLED";
        public const string SsiTelemetryEnabled = "DD_INTERNAL_PROFILING_SSI_TELEMETRY_ENABLED";
    }
}
