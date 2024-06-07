// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "../../../shared/src/native-src/string.h"

class EnvironmentVariables final
{
public:
    inline static const shared::WSTRING LogPath = WStr("DD_TRACE_LOG_PATH");
    inline static const shared::WSTRING LogDirectory = WStr("DD_TRACE_LOG_DIRECTORY");
    inline static const shared::WSTRING DebugLogEnabled = WStr("DD_TRACE_DEBUG");
    inline static const shared::WSTRING IncludeProcessNames = WStr("DD_PROFILER_PROCESSES");
    inline static const shared::WSTRING ExcludeProcessNames = WStr("DD_PROFILER_EXCLUDE_PROCESSES");
    inline static const shared::WSTRING InternalRuntimeId = WStr("DD_INTERNAL_CIVISIBILITY_RUNTIMEID");
    // DD_INJECTION_ENABLED is non-empty when SSI is enabled
    inline static const shared::WSTRING SingleStepInstrumentationEnabled = WStr("DD_INJECTION_ENABLED");
    inline static const shared::WSTRING ForceEolInstrumentation = WStr("DD_INJECT_FORCE");
    inline static const shared::WSTRING SingleStepInstrumentationTelemetryForwarderPath = WStr("DD_TELEMETRY_FORWARDER_PATH");
};