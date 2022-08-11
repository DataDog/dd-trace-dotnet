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
};