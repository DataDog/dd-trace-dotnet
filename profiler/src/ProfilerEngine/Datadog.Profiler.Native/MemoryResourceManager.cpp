// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MemoryResourceManager.h"

MemoryResourceManager::MemoryResourceManager(MemoryResourceManager&& other) noexcept
{
    *this = std::move(other);
}

MemoryResourceManager& MemoryResourceManager::operator=(MemoryResourceManager&& other) noexcept
{
    if (this == &other)
    {
        return *this;
    }

    _resources.swap(other._resources);
    return *this;
}

shared::pmr::memory_resource* MemoryResourceManager::GetDefault()
{
    return shared::pmr::get_default_resource();
}

shared::pmr::memory_resource* MemoryResourceManager::GetUnSynchronizedPool(std::size_t maxBlocksPerChunk, std::size_t maxBlockSize)
{
    _resources.push_back(std::make_unique<shared::pmr::unsynchronized_pool_resource>(
        shared::pmr::pool_options{.max_blocks_per_chunk = maxBlocksPerChunk, .largest_required_pool_block = maxBlockSize}));

    return _resources.back().get();
}
