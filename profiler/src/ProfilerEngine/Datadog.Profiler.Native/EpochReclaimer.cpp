// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EpochReclaimer.h"

EpochReclaimer::~EpochReclaimer()
{
    // No reader can be active anymore: the owner is being destroyed.
    FreeList(_retired[0]);
    FreeList(_retired[1]);
}

void EpochReclaimer::Retire(void* p, Deleter deleter, std::size_t size)
{
    if (p == nullptr)
    {
        return;
    }

    std::lock_guard<std::mutex> lock(_retireMutex);
    // _epoch is only modified under _retireMutex
    auto epoch = _epoch.load(std::memory_order_relaxed);
    _retired[epoch & 1].push_back({p, deleter, size});
    _pendingBytes.fetch_add(size, std::memory_order_relaxed);
}

void EpochReclaimer::TryReclaim()
{
    std::lock_guard<std::mutex> lock(_retireMutex);

    // Try to advance twice: without active readers, the items retired during the
    // current epoch are freed right away (see the header for the algorithm).
    for (int i = 0; i < 2; i++)
    {
        if (_retired[0].empty() && _retired[1].empty())
        {
            return;
        }

        auto next = _epoch.load(std::memory_order_relaxed) + 1;
        if (HasReaders(next & 1))
        {
            return;
        }

        FreeList(_retired[next & 1]);
        _epoch.store(next, std::memory_order_seq_cst);
    }
}

bool EpochReclaimer::HasReaders(uint32_t parity) const noexcept
{
    // The shards are read one after the other, not atomically as a whole. This is
    // fine: a reader that increments a shard after it was read here can only load the
    // pointers after that (seq_cst), i.e. after they were unpublished.
    for (auto const& shard : _shards)
    {
        if (shard.Count[parity].load(std::memory_order_seq_cst) != 0)
        {
            return true;
        }
    }
    return false;
}

void EpochReclaimer::FreeList(std::vector<Retired>& list)
{
    std::size_t freed = 0;
    for (auto const& item : list)
    {
        item.Delete(item.Pointer);
        freed += item.Size;
    }
    list.clear();
    _pendingBytes.fetch_sub(freed, std::memory_order_relaxed);
}
