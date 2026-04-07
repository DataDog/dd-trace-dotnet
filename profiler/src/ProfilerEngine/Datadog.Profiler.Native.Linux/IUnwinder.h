#pragma once

#include <cstdint>

class IUnwinder
{
public:
    virtual ~IUnwinder() = default;

    // Returns the number of frames unwound
    virtual std::int32_t Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize) const = 0;
};