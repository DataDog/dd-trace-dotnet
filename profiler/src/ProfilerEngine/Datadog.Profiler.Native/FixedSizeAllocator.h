// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <atomic>

class FixedSizeAllocator final : public shared::pmr::memory_resource
{
public:

    FixedSizeAllocator(std::size_t chunckSize, std::size_t nbBlocks, shared::pmr::memory_resource* main_resource = shared::pmr::get_default_resource());
    ~FixedSizeAllocator();

private:
    static std::size_t ComputeAlignedSize(std::size_t x, std::size_t alignment = Alignment);

    // Inherited via memory_resource
    void* do_allocate(size_t _Bytes, size_t _Align) override;
    void do_deallocate(void* _Ptr, size_t _Bytes, size_t _Align) override;
    bool do_is_equal(const memory_resource& _That) const noexcept override;

    std::size_t GetBlockAlignedSize() const;
    std::size_t GetBufferAlignedSize() const;

    static constexpr std::uint8_t MaxRetry = 3;
    static constexpr std::size_t Alignment = alignof(max_align_t);

    shared::pmr::memory_resource* _upstreamResource;
    void* _buffer;
    std::atomic<std::uint64_t> _currentBlock;
    std::size_t _nbBlocks;
    std::size_t _blockSize;
};
