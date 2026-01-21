// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IUnwinder.h"
#include <cstdint>

class Backtrace2Unwinder : public IUnwinder
{
public:
    Backtrace2Unwinder();
    ~Backtrace2Unwinder() override = default;

    // Returns the number of frames unwound
    std::int32_t Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize) const override;

};