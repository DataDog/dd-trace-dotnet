// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "HResultConverter.h"

#define CALL(x)                                                                                               \
    {                                                                                                         \
        static bool dd_already_logged = false;                                                                     \
        HRESULT hr = x;                                                                                       \
        if (FAILED(hr))                                                                                       \
        {                                                                                                     \
        if (dd_already_logged) \
           return; \
            dd_already_logged = true;                                                                         \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return;                                                                                           \
        }                                                                                                     \
    }

#define INVOKE(x)                                                                                             \
    {                                                                                                         \
        static bool dd_already_logged = false;                                                                     \
        HRESULT hr = x;                                                                                       \
        if (FAILED(hr))                                                                                       \
        {                                                                                                     \
        if (dd_already_logged) \
           return false; \
            dd_already_logged = true;                                                                         \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return false;                                                                                     \
        }                                                                                                     \
    }
