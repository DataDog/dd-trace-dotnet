// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <memory>
#include <vector>

class MemoryResourceManager
{
public:
    MemoryResourceManager() = default;
    ~MemoryResourceManager() = default;

    MemoryResourceManager(MemoryResourceManager const&) = delete;
    MemoryResourceManager& operator=(MemoryResourceManager const&) = delete;

    MemoryResourceManager(MemoryResourceManager&& other) noexcept;

    MemoryResourceManager& operator=(MemoryResourceManager&& other) noexcept;

    static shared::pmr::memory_resource* GetDefault();
    shared::pmr::memory_resource* GetSynchronizedPool(std::size_t maxBlocksPerChunk, std::size_t maxBlockSize, bool useMmapAsUpstream = false);

private:
    shared::pmr::memory_resource* GetSynchronizedPool(
        shared::pmr::memory_resource* upstream,
        std::size_t maxBlocksPerChunk,
        std::size_t maxBlockSize);

#ifdef LINUX
    shared::pmr::memory_resource* GetMmapResource();
#endif

    std::vector<std::unique_ptr<shared::pmr::memory_resource>> _resources;
};
