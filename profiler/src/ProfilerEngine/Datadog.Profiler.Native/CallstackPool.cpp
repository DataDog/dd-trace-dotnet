// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"

constexpr std::size_t CallstackPool::GetPoolAlignedPoolSize()
{
    auto x = sizeof(Pool);
    return ((x - 1) | (8 - 1)) + 1;
}

CallstackPool::CallstackPool(std::size_t nbPools) :
    _nbPools{nbPools},
    _pools{std::make_unique<std::uint8_t[]>(nbPools * GetPoolAlignedPoolSize())},
    _current{0}
{
}

Callstack CallstackPool::Get()
{
    return Callstack(this);
}

shared::span<std::uintptr_t> CallstackPool::Acquire()
{
    auto alignup = [](std::size_t x) { return ((x - 1) | (8 - 1)) + 1; };

    for (auto i = 0; i < MaxRetry; i++)
    {
        auto v = _current.fetch_add(1);
        auto idx_linear = v % _nbPools;
        auto offset = idx_linear * GetPoolAlignedPoolSize();
        auto* pool = reinterpret_cast<Pool*>(_pools.get() + offset);

        if (pool->_header._lock.exchange(1, std::memory_order_seq_cst) == 0)
        {
            // move past the header
            auto* data = reinterpret_cast<std::uintptr_t*>(reinterpret_cast<std::uint8_t*>(pool) + alignup(sizeof(PoolHeader)));
            return {data, Callstack::MaxFrames};
        }
    }
    return {};
}

void CallstackPool::Release(shared::span<std::uintptr_t> buffer)
{
    auto alignup = [](std::size_t x) { return ((x - 1) | (8 - 1)) + 1; };
    auto* header = reinterpret_cast<PoolHeader*>(reinterpret_cast<std::uint8_t*>(buffer.data()) - alignup(sizeof(PoolHeader)));
    header->_lock.exchange(0, std::memory_order_seq_cst);
}
