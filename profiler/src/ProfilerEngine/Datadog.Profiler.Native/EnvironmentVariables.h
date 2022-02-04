// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/string.h"

class EnvironmentVariables final
{
public:
    inline static const shared::WSTRING ProfilingEnabled = WStr("DD_PROFILING_ENABLED");
    inline static const shared::WSTRING DebugLogEnabled = WStr("DD_TRACE_DEBUG");
    inline static const shared::WSTRING LogPath = WStr("DD_PROFILING_LOG_PATH");
    inline static const shared::WSTRING LogDirectory = WStr("DD_PROFILING_LOG_DIR");
    inline static const shared::WSTRING OperationalMetricsEnabled = WStr("DD_INTERNAL_OPERATIONAL_METRICS_ENABLED");
    inline static const shared::WSTRING Version = WStr("DD_VERSION");
    inline static const shared::WSTRING ServiceName = WStr("DD_SERVICE");
    inline static const shared::WSTRING Environment = WStr("DD_ENV");
};
