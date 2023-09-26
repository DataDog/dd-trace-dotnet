// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <iostream>
#include <memory>
#include <string>
#include <Windows.h>

inline uint32_t ShowLastError(const char* message, uint32_t lastError = ::GetLastError())
{
#ifdef _DEBUG
    std::cout << message << " (" << lastError << ")\n";
#endif

    return lastError;
}
