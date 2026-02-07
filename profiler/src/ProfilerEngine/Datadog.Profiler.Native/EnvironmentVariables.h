// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/string.h"

class EnvironmentVariables final
{
public:
    inline static const shared::WSTRING ProfilerEnabled             = WStr("DD_PROFILING_ENABLED");
    inline static const shared::WSTRING DebugLogEnabled             = WStr("DD_TRACE_DEBUG");
    inline static const shared::WSTRING LogPath                     = WStr("DD_PROFILING_LOG_PATH");
    inline static const shared::WSTRING LogDirectory                = WStr("DD_TRACE_LOG_DIRECTORY");
    inline static const shared::WSTRING DeprecatedLogDirectory      = WStr("DD_PROFILING_LOG_DIR");
    inline static const shared::WSTRING OperationalMetricsEnabled   = WStr("DD_INTERNAL_OPERATIONAL_METRICS_ENABLED");
    inline static const shared::WSTRING Version                     = WStr("DD_VERSION");
    inline static const shared::WSTRING ServiceName                 = WStr("DD_SERVICE");
    inline static const shared::WSTRING Environment                 = WStr("DD_ENV");
    inline static const shared::WSTRING Site                        = WStr("DD_SITE");
    inline static const shared::WSTRING UploadInterval              = WStr("DD_PROFILING_UPLOAD_PERIOD");
    inline static const shared::WSTRING AgentUrl                    = WStr("DD_TRACE_AGENT_URL");
    inline static const shared::WSTRING AgentHost                   = WStr("DD_AGENT_HOST");
    inline static const shared::WSTRING AgentPort                   = WStr("DD_TRACE_AGENT_PORT");
    inline static const shared::WSTRING NamedPipeName               = WStr("DD_TRACE_PIPE_NAME");
    inline static const shared::WSTRING ApiKey                      = WStr("DD_API_KEY");
    inline static const shared::WSTRING Hostname                    = WStr("DD_HOSTNAME");
    inline static const shared::WSTRING Tags                        = WStr("DD_TAGS");
    inline static const shared::WSTRING GitRepositoryUrl            = WStr("DD_GIT_REPOSITORY_URL");
    inline static const shared::WSTRING GitCommitSha                = WStr("DD_GIT_COMMIT_SHA");
    inline static const shared::WSTRING NativeFramesEnabled         = WStr("DD_PROFILING_FRAMES_NATIVE_ENABLED");
    inline static const shared::WSTRING CpuProfilingEnabled         = WStr("DD_PROFILING_CPU_ENABLED");
    inline static const shared::WSTRING WallTimeProfilingEnabled    = WStr("DD_PROFILING_WALLTIME_ENABLED");
    inline static const shared::WSTRING ExceptionProfilingEnabled   = WStr("DD_PROFILING_EXCEPTION_ENABLED");

    // only available on .NET 5+
    inline static const shared::WSTRING AllocationProfilingEnabled      = WStr("DD_PROFILING_ALLOCATION_ENABLED");
    inline static const shared::WSTRING DeprecatedContentionProfilingEnabled = WStr("DD_PROFILING_CONTENTION_ENABLED");  // should be deprecated (only used in 2.18)
    inline static const shared::WSTRING LockContentionProfilingEnabled  = WStr("DD_PROFILING_LOCK_ENABLED");
    inline static const shared::WSTRING GCProfilingEnabled              = WStr("DD_PROFILING_GC_ENABLED");
    inline static const shared::WSTRING HeapProfilingEnabled            = WStr("DD_PROFILING_HEAP_ENABLED");

    inline static const shared::WSTRING HeapHandleLimit                 = WStr("DD_INTERNAL_PROFILING_HEAP_HANDLE_LIMIT");
    inline static const shared::WSTRING ExceptionSampleLimit            = WStr("DD_INTERNAL_PROFILING_EXCEPTION_SAMPLE_LIMIT");
    inline static const shared::WSTRING AllocationSampleLimit           = WStr("DD_INTERNAL_PROFILING_ALLOCATION_SAMPLE_LIMIT");
    inline static const shared::WSTRING ContentionSampleLimit           = WStr("DD_INTERNAL_PROFILING_CONTENTION_SAMPLE_LIMIT");
    inline static const shared::WSTRING ContentionDurationThreshold     = WStr("DD_INTERNAL_PROFILING_CONTENTION_DURATION_THRESHOLD");
    inline static const shared::WSTRING CpuWallTimeSamplingRate         = WStr("DD_INTERNAL_PROFILING_SAMPLING_RATE");
    inline static const shared::WSTRING WalltimeThreadsThreshold        = WStr("DD_INTERNAL_PROFILING_WALLTIME_THREADS_THRESHOLD");
    inline static const shared::WSTRING CpuTimeThreadsThreshold         = WStr("DD_INTERNAL_PROFILING_CPUTIME_THREADS_THRESHOLD");
    inline static const shared::WSTRING CodeHotspotsThreadsThreshold    = WStr("DD_INTERNAL_PROFILING_CODEHOTSPOTS_THREADS_THRESHOLD");
    inline static const shared::WSTRING TimestampsAsLabelEnabled        = WStr("DD_INTERNAL_PROFILING_TIMESTAMPS_AS_LABEL_ENABLED");
    inline static const shared::WSTRING ProfilesOutputDir               = WStr("DD_INTERNAL_PROFILING_OUTPUT_DIR");
    inline static const shared::WSTRING DevelopmentConfiguration        = WStr("DD_INTERNAL_USE_DEVELOPMENT_CONFIGURATION");
    inline static const shared::WSTRING Agentless                       = WStr("DD_PROFILING_AGENTLESS");
    inline static const shared::WSTRING CoreMinimumOverride             = WStr("DD_PROFILING_MIN_CORES_THRESHOLD");
    inline static const shared::WSTRING AllocationRecorderEnabled       = WStr("DD_INTERNAL_PROFILING_ALLOCATION_RECORDER_ENABLED");
    inline static const shared::WSTRING DebugInfoEnabled                = WStr("DD_INTERNAL_PROFILING_DEBUG_INFO_ENABLED");
    inline static const shared::WSTRING GcThreadsCpuTimeInternalEnabled = WStr("DD_INTERNAL_GC_THREADS_CPUTIME_ENABLED");
    inline static const shared::WSTRING GcThreadsCpuTimeEnabled         = WStr("DD_GC_THREADS_CPUTIME_ENABLED");
    inline static const shared::WSTRING InternalMetricsEnabled          = WStr("DD_INTERNAL_METRICS_ENABLED");
    inline static const shared::WSTRING ThreadLifetimeInternalEnabled   = WStr("DD_INTERNAL_THREAD_LIFETIME_ENABLED");
    inline static const shared::WSTRING ThreadLifetimeEnabled           = WStr("DD_THREAD_LIFETIME_ENABLED");
    inline static const shared::WSTRING SystemCallsShieldEnabled        = WStr("DD_INTERNAL_SYSTEM_CALLS_SHIELD_ENABLED");
    inline static const shared::WSTRING EtwEnabled                      = WStr("DD_INTERNAL_PROFILING_ETW_ENABLED");
    inline static const shared::WSTRING ManagedActivationEnabled        = WStr("DD_PROFILING_MANAGED_ACTIVATION_ENABLED");
    inline static const shared::WSTRING SsiDeployed                     = WStr("DD_INJECTION_ENABLED");
    inline static const shared::WSTRING EtwLoggingEnabled               = WStr("DD_INTERNAL_ETW_LOGGING_ENABLED");
    inline static const shared::WSTRING EtwReplayEndpoint               = WStr("DD_INTERNAL_ETW_REPLAY_ENDPOINT");
    inline static const shared::WSTRING CpuProfilerType                 = WStr("DD_INTERNAL_CPU_PROFILER_TYPE");
    inline static const shared::WSTRING CpuProfilingInterval            = WStr("DD_INTERNAL_CPU_PROFILING_INTERVAL");
    inline static const shared::WSTRING SsiLongLivedThreshold           = WStr("DD_INTERNAL_PROFILING_LONG_LIVED_THRESHOLD");
    inline static const shared::WSTRING HttpProfilingInternalEnabled    = WStr("DD_INTERNAL_PROFILING_HTTP_ENABLED");
    inline static const shared::WSTRING HttpProfilingEnabled            = WStr("DD_PROFILING_HTTP_ENABLED");
    inline static const shared::WSTRING HttpRequestDurationThreshold    = WStr("DD_INTERNAL_PROFILING_HTTP_REQUEST_DURATION_THRESHOLD");
    inline static const shared::WSTRING HeapSnapshotEnabled             = WStr("DD_PROFILING_HEAPSNAPSHOT_ENABLED");
    inline static const shared::WSTRING HeapSnapshotInterval            = WStr("DD_INTERNAL_PROFILING_HEAPSNAPSHOT_INTERVAL");
    inline static const shared::WSTRING HeapSnapshotCheckInterval       = WStr("DD_INTERNAL_PROFILING_HEAPSNAPSHOT_CHECK_INTERVAL");
    inline static const shared::WSTRING HeapSnapshotMemoryPressureThreshold = WStr("DD_INTERNAL_PROFILING_HEAPSNAPSHOT_MEMORY_PRESSURE_THRESHOLD");

    // used for tests only
    inline static const shared::WSTRING ForceHttpSampling           = WStr("DD_INTERNAL_PROFILING_FORCE_HTTP_SAMPLING");

    inline static const shared::WSTRING CIVisibilityEnabled         = WStr("DD_CIVISIBILITY_ENABLED");
    inline static const shared::WSTRING InternalCIVisibilitySpanId  = WStr("DD_INTERNAL_CIVISIBILITY_SPANID");
    inline static const shared::WSTRING WaitHandleProfilingEnabled  = WStr("DD_INTERNAL_PROFILING_WAITHANDLE_ENABLED");
    inline static const shared::WSTRING UseManagedCodeCache = WStr("DD_INTERNAL_USE_MANAGED_CODE_CACHE");
};
