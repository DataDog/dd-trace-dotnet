// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"

CallstackPool::CallstackPool(std::size_t nbCallstacks) :
    _nbCallstacks{nbCallstacks},
    _callstacks{std::make_unique<std::uint8_t[]>(nbCallstacks * ComputeAlignedSize<CallstackLayout>())},
    _current{0}
{
}

CallstackPool::CallstackPool(CallstackPool&& other) noexcept :
    _nbCallstacks{0},
    _callstacks{nullptr},
    _current{0}
{
    *this = std::move(other);
}

CallstackPool& CallstackPool::operator=(CallstackPool&& other) noexcept
{
    if (this == &other)
        return *this;

    std::swap(other._nbCallstacks, _nbCallstacks);
    std::swap(_callstacks, other._callstacks);
    // weird but there is no swap for atomic objects
    _current.exchange(other._current.exchange(_current));

    return *this;
}

Callstack CallstackPool::Get()
{
    return Callstack(this);
}

shared::span<std::uintptr_t> CallstackPool::Acquire()
{
    constexpr auto callstackAlignedSize = ComputeAlignedSize<CallstackLayout>();
    constexpr auto headerAlignedSize = ComputeAlignedSize<CallstackHeader>();

    for (auto i = 0; i < MaxRetry; i++)
    {
        auto v = _current.fetch_add(1);
        auto idx = v % _nbCallstacks;
        auto offset = idx * callstackAlignedSize;
        auto* callstack = reinterpret_cast<CallstackLayout*>(_callstacks.get() + offset);

        if (callstack->_header._lock.exchange(1, std::memory_order_seq_cst) == 0)
        {
            // move past the header
            auto* data = reinterpret_cast<std::uintptr_t*>(reinterpret_cast<std::uint8_t*>(callstack) + headerAlignedSize);
            return {data, Callstack::MaxFrames};
        }
    }
    return {};
}

void CallstackPool::Release(shared::span<std::uintptr_t> buffer)
{
    // if called by a non-usable/viable callstack
    if (buffer.data() == nullptr)
        return;

    auto* header = reinterpret_cast<CallstackHeader*>(reinterpret_cast<std::uint8_t*>(buffer.data()) - ComputeAlignedSize<CallstackHeader>());
    header->_lock.exchange(0, std::memory_order_seq_cst);
}
