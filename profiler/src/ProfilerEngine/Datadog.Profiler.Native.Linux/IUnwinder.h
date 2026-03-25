#pragma once

#include <cstdint>

class UnwinderTracer;

class IUnwinder
{
public:
    virtual ~IUnwinder() = default;

    // Returns the number of frames unwound
    virtual std::int32_t Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize,
                                std::uintptr_t stackBase = 0, std::uintptr_t stackEnd = 0,
                                UnwinderTracer* tracer = nullptr) const = 0;
};