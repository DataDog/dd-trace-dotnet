// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

#include "shared/src/native-src/span.hpp"

class IUnwinder
{
public:
    IUnwinder() = default;
    virtual ~IUnwinder() = default;

    virtual std::size_t Unwind(void* ctx, shared::span<std::uintptr_t> frames) = 0;
};
