// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FixedSizeAllocator.h"

#include <string.h>

#include <cassert>

struct BlockHeader
{
    std::atomic<std::uint8_t> _lock;
};

// Maybe passe a options struct with those info
FixedSizeAllocator::FixedSizeAllocator(std::size_t blockSize, std::size_t nbBlocks, shared::pmr::memory_resource* upstreamResource) :
    _upstreamResource{upstreamResource},
    _buffer{nullptr},
    _currentBlock(0),
    _nbBlocks(nbBlocks),
    _blockSize{blockSize}
{
    auto alignedSize = GetBufferAlignedSize();
    _buffer = _upstreamResource->allocate(alignedSize);
    // At least on linux, some utests fail if the buffer is not zero'ed
    memset(_buffer, 0, alignedSize);
}

FixedSizeAllocator::~FixedSizeAllocator()
{
    _upstreamResource->deallocate(_buffer, GetBufferAlignedSize());
}

struct BadBlockAllocationException : public std::bad_alloc
{
    const char* what() const noexcept override
    {
        return "Requested allocation size is different from the fixed size.";
    }
};

void* FixedSizeAllocator::do_allocate(size_t _Bytes, size_t _Align)
{
    if (_Bytes != _blockSize)
    {
        throw BadBlockAllocationException();
    }

    const auto headerSize = ComputeAlignedSize(sizeof(BlockHeader), _Align);
    const auto blockSize = GetBlockAlignedSize();

    for (auto i = 0; i < MaxRetry; i++)
    {
        const auto v = _currentBlock.fetch_add(1);
        const auto idx = v % _nbBlocks;
        const auto offset = idx * blockSize;
        auto* block = reinterpret_cast<std::uint8_t*>(_buffer) + offset;
        auto* header = reinterpret_cast<BlockHeader*>(block);

        if (header->_lock.exchange(1, std::memory_order_seq_cst) == 0)
        {
            // move past the header
            auto* data = block + headerSize;
            return data;
        }
    }

    return nullptr;
}

void FixedSizeAllocator::do_deallocate(void* _Ptr, size_t _Bytes, size_t _Align)
{
    // Like new, nullptr means no data to deallocate. No block was available at that time
    if (_Ptr == nullptr)
        return;

    auto* header = reinterpret_cast<BlockHeader*>(reinterpret_cast<std::uint8_t*>(_Ptr) - ComputeAlignedSize(sizeof(BlockHeader), _Align));
    header->_lock.exchange(0, std::memory_order_seq_cst);
}

bool FixedSizeAllocator::do_is_equal(const memory_resource& _That) const noexcept
{
    return this == &_That;
}

inline std::size_t FixedSizeAllocator::GetBlockAlignedSize() const
{
    static const auto alignedSize = ComputeAlignedSize(_blockSize) + ComputeAlignedSize(sizeof(BlockHeader));
    return alignedSize;
}

inline std::size_t FixedSizeAllocator::ComputeAlignedSize(std::size_t x, std::size_t alignment)
{
    return ((x - 1) | (alignment - 1)) + 1;
}

inline std::size_t FixedSizeAllocator::GetBufferAlignedSize() const
{
    return GetBlockAlignedSize() * _nbBlocks;
}
