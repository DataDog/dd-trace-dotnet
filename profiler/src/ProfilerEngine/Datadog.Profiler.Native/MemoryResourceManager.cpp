// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MemoryResourceManager.h"

#ifdef LINUX
#include "MmapMemoryResource.h"
#endif

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

shared::pmr::memory_resource* MemoryResourceManager::GetSynchronizedPool(
    std::size_t maxBlocksPerChunk,
    std::size_t maxBlockSize,
    bool useMmapAsUpstream)
{
    auto* upstream =
#ifdef _WINDOWS
        GetDefault();
#else
        useMmapAsUpstream ? GetMmapResource() : GetDefault();
#endif

    return GetSynchronizedPool(upstream, maxBlocksPerChunk, maxBlockSize);
}

shared::pmr::memory_resource* MemoryResourceManager::GetSynchronizedPool(
    shared::pmr::memory_resource* upstream,
    std::size_t maxBlocksPerChunk,
    std::size_t maxBlockSize)
{
#if defined(__cpp_lib_experimental_memory_resources) && (__cpp_lib_experimental_memory_resources <= 201402L)
#pragma GCC warning "synchronized_pool_resource does not exist. Use the default (new/delete) memory resource for now."
    return GetDefault();
#else
    _resources.push_back(std::make_unique<shared::pmr::synchronized_pool_resource>(
        shared::pmr::pool_options{.max_blocks_per_chunk = maxBlocksPerChunk, .largest_required_pool_block = maxBlockSize},
        upstream));

    return _resources.back().get();
#endif
}

#ifdef LINUX
shared::pmr::memory_resource* MemoryResourceManager::GetMmapResource()
{
    static auto resource = std::make_unique<MmapMemoryResource>();
    return resource.get();
}
#endif