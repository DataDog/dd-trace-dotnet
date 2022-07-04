// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/string.h"

class EnvironmentVariables final
{
public:
    inline static const shared::WSTRING ProfilingEnabled            = WStr("DD_PROFILING_ENABLED");
    inline static const shared::WSTRING DebugLogEnabled             = WStr("DD_TRACE_DEBUG");
    inline static const shared::WSTRING LogPath                     = WStr("DD_PROFILING_LOG_PATH");
    inline static const shared::WSTRING LogDirectory                = WStr("DD_PROFILING_LOG_DIR");
    inline static const shared::WSTRING OperationalMetricsEnabled   = WStr("DD_INTERNAL_OPERATIONAL_METRICS_ENABLED");
    inline static const shared::WSTRING Version                     = WStr("DD_VERSION");
    inline static const shared::WSTRING ServiceName                 = WStr("DD_SERVICE");
    inline static const shared::WSTRING Environment                 = WStr("DD_ENV");
    inline static const shared::WSTRING Site                        = WStr("DD_SITE");
    inline static const shared::WSTRING UploadInterval              = WStr("DD_PROFILING_UPLOAD_PERIOD");
    inline static const shared::WSTRING AgentUrl                    = WStr("DD_TRACE_AGENT_URL");
    inline static const shared::WSTRING AgentHost                   = WStr("DD_AGENT_HOST");
    inline static const shared::WSTRING AgentPort                   = WStr("DD_TRACE_AGENT_PORT");
    inline static const shared::WSTRING ApiKey                      = WStr("DD_API_KEY");
    inline static const shared::WSTRING Hostname                    = WStr("DD_HOSTNAME");
    inline static const shared::WSTRING Tags                        = WStr("DD_TAGS");
    inline static const shared::WSTRING NativeFramesEnabled         = WStr("DD_PROFILING_FRAMES_NATIVE_ENABLED");
    inline static const shared::WSTRING CpuProfilingEnabled         = WStr("DD_PROFILING_CPU_ENABLED");
    inline static const shared::WSTRING WallTimeProfilingEnabled    = WStr("DD_PROFILING_WALLTIME_ENABLED");
    inline static const shared::WSTRING ExceptionProfilingEnabled   = WStr("DD_PROFILING_EXCEPTION_ENABLED");
    inline static const shared::WSTRING ExceptionSampleLimit        = WStr("DD_PROFILING_EXCEPTION_SAMPLE_LIMIT");
    inline static const shared::WSTRING ProfilesOutputDir           = WStr("DD_INTERNAL_PROFILING_OUTPUT_DIR");
    inline static const shared::WSTRING DevelopmentConfiguration    = WStr("DD_INTERNAL_USE_DEVELOPMENT_CONFIGURATION");
    inline static const shared::WSTRING Agentless                   = WStr("DD_PROFILING_AGENTLESS");

    // feature flags
    inline static const shared::WSTRING FF_LibddprofEnabled = WStr("DD_INTERNAL_PROFILING_LIBDDPROF_ENABLED");
};
