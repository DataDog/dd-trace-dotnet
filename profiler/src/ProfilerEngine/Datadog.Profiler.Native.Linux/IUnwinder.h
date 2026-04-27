#pragma once

#include <cstdint>

class UnwinderTracer;
class Callstack;

class IUnwinder
{
public:
    virtual ~IUnwinder() = default;

    // Returns the number of frames unwound
    virtual std::int32_t Unwind(void* ctx, Callstack& callstack, std::uintptr_t stackBase = 0, std::uintptr_t stackEnd = 0,
                                UnwinderTracer* tracer = nullptr) const = 0;
};
