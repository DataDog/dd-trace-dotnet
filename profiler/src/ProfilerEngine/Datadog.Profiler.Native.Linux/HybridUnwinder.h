// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IUnwinder.h"

#include <optional>

class ManagedCodeCache;
class UnwindCursor;

class HybridUnwinder: public IUnwinder
{
public:
    HybridUnwinder(ManagedCodeCache* managedCodeCache);
    ~HybridUnwinder() override = default;

    std::int32_t Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize,
                        std::uintptr_t stackBase = 0, std::uintptr_t stackEnd = 0,
                        UnwinderTracer* tracer = nullptr) const override;

private:
    std::optional<bool> IsManaged(std::uintptr_t ip) const;
    bool UnwindNativeFrames(UnwindCursor* cursor, std::uintptr_t* buffer, std::size_t bufferSize,
                        UnwinderTracer* tracer, std::size_t& i) const;
    bool UnwindManagedFrames(UnwindCursor* cursor, std::uintptr_t* buffer, std::size_t bufferSize,
                        UnwinderTracer* tracer, std::size_t& i,
                        std::uintptr_t stackBase, std::uintptr_t stackEnd) const;

    ManagedCodeCache* _codeCache;
};