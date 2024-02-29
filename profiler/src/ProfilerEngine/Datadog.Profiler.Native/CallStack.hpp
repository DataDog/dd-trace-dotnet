// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "allocators.h"

#include "shared/src/native-src/dd_span.hpp"

class CallStack2
{
public:
    CallStack2(pmr::memory_resource* memoryResource = allocators::get_default_stack_allocator()) noexcept:
        _memoryResource{memoryResource},
        _data{nullptr},
        _nbIps{0}
    {
    }

    ~CallStack2()
    {
        if (_data != nullptr)
        {
            _memoryResource->deallocate(_data, data_size_in_bytes);
        }
    }

    CallStack2(CallStack2 const&) = delete;
    CallStack2& operator=(CallStack2 const&) = delete;

    CallStack2(CallStack2&& other) noexcept
    {
        *this = std::move(other);
    }

    CallStack2& operator=(CallStack2&& other) noexcept
    {
        if (this == &other)
        {
            return *this;
        }

        std::swap(_memoryResource, other._memoryResource);
        std::swap(_data, other._data);
        std::swap(_nbIps, other._nbIps);

        return *this;
    }

    shared::span<std::uintptr_t> GetBuffer()
    {
        AllocateIfNeeded();

        if (_data == nullptr)
        {
            return {};
        }

        return shared::span(_data, max_ips);
    }

    std::uintptr_t const * begin() const
    {
        return _data;
    }

    std::uintptr_t const* end() const
    {
        if (_data == nullptr)
            return _data;

        return _data + _nbIps;
    }

    void SetCount(std::size_t nbIps)
    {
        _nbIps = nbIps;
    }

    std::size_t size() const
    {
        if (_data == nullptr)
        {
            return 0;
        }

        return _nbIps;
    }

    bool SameIps(CallStack2 const& other)
    {
        return _nbIps == other._nbIps && SameInstructionPointers(shared::span(other._data, other._nbIps));
    }

private:

    bool SameInstructionPointers(shared::span<std::uintptr_t> otherIps)
    {
        for (std::size_t i = 0; i < otherIps.size(); i++)
        {
            if (_data[i] != otherIps.data()[i])
                return false;
        }
        return true;
    }

    void AllocateIfNeeded()
    {
        try
        {
            _data = (std::uintptr_t*)_memoryResource->allocate(data_size_in_bytes);
        }
        catch (...)
        {
            if (_data != nullptr)
            {
                _memoryResource->deallocate(_data, data_size_in_bytes);
                _data = nullptr;
            }
        }
    }

    const std::size_t max_ips = 1024;
    const std::size_t data_size_in_bytes = max_ips * sizeof(std::uintptr_t);

    pmr::memory_resource* _memoryResource;
    std::uintptr_t* _data;
    std::size_t _nbIps;
};