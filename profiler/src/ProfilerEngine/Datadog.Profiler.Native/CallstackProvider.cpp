// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackProvider.h"

CallstackProvider::CallstackProvider() :
    CallstackProvider(nullptr)
{
}

CallstackProvider::CallstackProvider(shared::pmr::memory_resource* memoryResource) :
    _resource{memoryResource}
{
}

CallstackProvider::~CallstackProvider()
{
    _resource = nullptr;
}

CallstackProvider::CallstackProvider(CallstackProvider&& other) noexcept :
    CallstackProvider(nullptr)
{
    *this = std::move(other);
}

CallstackProvider& CallstackProvider::operator=(CallstackProvider&& other) noexcept
{
    if (this == &other)
    {
        return *this;
    }

    std::swap(_resource, other._resource);

    return *this;
}

Callstack CallstackProvider::Get()
{
    return Callstack(_resource);
}