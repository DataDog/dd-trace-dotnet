// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "HResultConverter.h"

#define CALL(x)                                                                                                   \
    {                                                                                                             \
        HRESULT hr = x;                                                                                           \
        if (FAILED(hr))                                                                                           \
        {                                                                                                         \
            LogOnce(Warn, "Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return;                                                                                               \
        }                                                                                                         \
    }

#define INVOKE(x)                                                                                                 \
    {                                                                                                             \
        HRESULT hr = x;                                                                                           \
        if (FAILED(hr))                                                                                           \
        {                                                                                                         \
            LogOnce(Warn, "Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return false;                                                                                         \
        }                                                                                                         \
    }

#define INVOKE_INFO(x)                                                                                            \
    {                                                                                                             \
        HRESULT hr = x;                                                                                           \
        if (FAILED(hr))                                                                                           \
        {                                                                                                         \
            LogOnce(Info, "Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return false;                                                                                         \
        }                                                                                                         \
    }