// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IUnwinder.h"

class ManagedCodeCache;

class HybridUnwinder: public IUnwinder
{
public:
    HybridUnwinder(ManagedCodeCache* managedCodeCache);
    ~HybridUnwinder() override = default;

    std::int32_t Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize,
                        std::uintptr_t stackBase = 0, std::uintptr_t stackEnd = 0) const override;

private:
    ManagedCodeCache* _codeCache;
};