// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CodeRangeTrie.h"

#include <algorithm>
#include <new>

namespace {

// Returns the last range whose start is <= ip (ranges sorted by startAddress)
template <typename Range>
const Range* FindLastStartingBefore(const Range* ranges, std::uint32_t count, std::uintptr_t ip) noexcept
{
    auto it = std::upper_bound(ranges, ranges + count, ip,
        [](std::uintptr_t value, const Range& range) { return value < range.startAddress; });

    if (it == ranges)
    {
        return nullptr;
    }
    return it - 1;
}

} // namespace

//
// CodeRangeTrie
//

CodeRangeTrie::CodeRangeTrie(EpochReclaimer& reclaimer) :
    _reclaimer(reclaimer),
    _root{}
{
    static_assert(TotalLevelBits() == MaxAddressBits - PageShift, "the trie levels must cover the whole page number");
    _memoryUsage.store(sizeof(_root), std::memory_order_relaxed);
}

CodeRangeTrie::~CodeRangeTrie()
{
    // No reader can be active anymore: the owner is being destroyed.
    FreeNode(_root, 0);
}

void CodeRangeTrie::FreeNode(Slot* node, unsigned level)
{
    for (std::size_t i = 0; i < (std::size_t{1} << LevelBits[level]); i++)
    {
        auto* child = node[i].load(std::memory_order_relaxed);
        if (child == nullptr)
        {
            continue;
        }

        if (level + 1 == Levels)
        {
            if (child != ModulePageTag())
            {
                FreeBlock(child);
            }
        }
        else
        {
            FreeNode(static_cast<Slot*>(child), level + 1);
            delete[] static_cast<Slot*>(child);
        }
    }
}

const CodeRangeTrie::RangeBlock* CodeRangeTrie::FindCandidate(std::uintptr_t ip, std::uint32_t& index) const noexcept
{
    if (!IsIndexable(ip))
    {
        return nullptr;
    }

    auto page = static_cast<std::uint64_t>(ip) >> PageShift;

    // Interior nodes are never freed: acquire is enough to see them fully built.
    const Slot* node = _root;
    for (unsigned level = 0; level + 1 < Levels; level++)
    {
        auto* child = node[LevelIndex(page, level)].load(std::memory_order_acquire);
        if (child == nullptr)
        {
            return nullptr;
        }
        node = static_cast<const Slot*>(child);
    }

    // seq_cst: pairs with the seq_cst reader-counter increment (see EpochReclaimer)
    auto* block = static_cast<const RangeBlock*>(node[LevelIndex(page, Levels - 1)].load(std::memory_order_seq_cst));
    if (block == nullptr || block == ModulePageTag())
    {
        return block;
    }

    // Last key whose start is <= the offset of ip in the page (branchless search)
    auto count = block->Count.load(std::memory_order_acquire);
    auto* keys = block->Keys();
    auto offset = static_cast<std::uint16_t>(ip & PageMask);
    auto it = std::upper_bound(keys, keys + count, offset,
        [](std::uint16_t value, const Key& key) { return value < key.Start; });
    if (it == keys)
    {
        return nullptr;
    }

    index = static_cast<std::uint32_t>(it - keys - 1);
    return block;
}

std::optional<CodeRange> CodeRangeTrie::Find(std::uintptr_t ip, const EpochReclaimer::ReadGuard&) const noexcept
{
    std::uint32_t index;
    auto* block = FindCandidate(ip, index);
    if (block == nullptr || block == ModulePageTag())
    {
        return std::nullopt;
    }

    auto range = block->Ranges()[index].ToCodeRange();
    if (!range.contains(ip))
    {
        return std::nullopt;
    }

    return range;
}

bool CodeRangeTrie::Contains(std::uintptr_t ip, const EpochReclaimer::ReadGuard&) const noexcept
{
    std::uint32_t index;
    auto* block = FindCandidate(ip, index);
    if (block == nullptr)
    {
        return false;
    }

    if (block == ModulePageTag())
    {
        return true;
    }

    // The start of the candidate is <= ip by construction: only the end is checked
    return static_cast<std::uint16_t>(ip & PageMask) <= block->Keys()[index].End;
}

bool CodeRangeTrie::Insert(const CodeRange& range)
{
    if (range.endAddress < range.startAddress || !IsIndexable(range.endAddress) ||
        range.endAddress - range.startAddress > MaxRangeLastOffset)
    {
        return false;
    }

    std::lock_guard<std::mutex> lock(_writeMutex);

    // A method code range can span over several pages: add it to all of them.
    auto startPage = static_cast<std::uint64_t>(range.startAddress) >> PageShift;
    auto endPage = static_cast<std::uint64_t>(range.endAddress) >> PageShift;
    for (auto page = startPage; page <= endPage; ++page)
    {
        InsertIntoSlot(GetOrCreateSlot(page), page, range);
    }

    return true;
}

void CodeRangeTrie::MarkModulePages(const ModuleCodeRange& range, bool isLoaded)
{
    if (range.endAddress < range.startAddress || !IsIndexable(range.endAddress))
    {
        return;
    }

    // Only the pages fully covered by the range
    auto firstPage = (static_cast<std::uint64_t>(range.startAddress) + PageMask) >> PageShift;
    auto endPage = (static_cast<std::uint64_t>(range.endAddress) + 1) >> PageShift; // exclusive
    if (firstPage >= endPage)
    {
        return;
    }

    std::lock_guard<std::mutex> lock(_writeMutex);
    for (auto page = firstPage; page < endPage; ++page)
    {
        // The tag is never dereferenced: no need for the reclaimer
        if (isLoaded)
        {
            auto* slot = GetOrCreateSlot(page);
            if (slot->load(std::memory_order_relaxed) == nullptr)
            {
                slot->store(ModulePageTag(), std::memory_order_release);
            }
        }
        else
        {
            auto* slot = FindSlot(page);
            if (slot != nullptr && slot->load(std::memory_order_relaxed) == ModulePageTag())
            {
                slot->store(nullptr, std::memory_order_release);
            }
        }
    }
}

CodeRangeTrie::Slot* CodeRangeTrie::GetOrCreateSlot(std::uint64_t page)
{
    // Only called under _writeMutex: relaxed loads see our own writes. Nodes are
    // fully built (zero-initialized) before being published with a release store.
    Slot* node = _root;
    for (unsigned level = 0; level + 1 < Levels; level++)
    {
        auto& slot = node[LevelIndex(page, level)];
        auto* child = static_cast<Slot*>(slot.load(std::memory_order_relaxed));
        if (child == nullptr)
        {
            auto size = std::size_t{1} << LevelBits[level + 1];
            child = new Slot[size]();
            _memoryUsage.fetch_add(size * sizeof(Slot), std::memory_order_relaxed);
            slot.store(child, std::memory_order_release);
        }
        node = child;
    }

    return &node[LevelIndex(page, Levels - 1)];
}

CodeRangeTrie::Slot* CodeRangeTrie::FindSlot(std::uint64_t page) const noexcept
{
    // Only called under _writeMutex
    auto* node = const_cast<Slot*>(_root);
    for (unsigned level = 0; level + 1 < Levels; level++)
    {
        node = static_cast<Slot*>(node[LevelIndex(page, level)].load(std::memory_order_relaxed));
        if (node == nullptr)
        {
            return nullptr;
        }
    }

    return &node[LevelIndex(page, Levels - 1)];
}

void CodeRangeTrie::InsertIntoSlot(Slot* slot, std::uint64_t page, const CodeRange& range)
{
    auto* block = static_cast<RangeBlock*>(slot->load(std::memory_order_relaxed));
    if (block == ModulePageTag())
    {
        // JIT code in a page marked as R2R: the module was unloaded without being
        // unmarked (or the tag is wrong). The ModuleRangeSet is still checked by the
        // callers, so dropping the tag is safe.
        block = nullptr;
    }
    std::uint32_t count = block == nullptr ? 0 : block->Count.load(std::memory_order_relaxed);
    auto key = KeyFor(range, page);

    // Fast path (in-place append): the JIT allocates code sequentially in its code
    // heaps, so a new range almost always sorts after the ones already in the page.
    // Entries below Count are never modified: write the new entry first, then publish
    // it by incrementing Count (release) - readers only look at [0, Count).
    if (block != nullptr && count < block->Capacity && block->Ranges()[count - 1].StartAddress <= range.startAddress)
    {
        block->Ranges()[count] = StoredRange::From(range);
        block->Keys()[count] = key;
        block->Count.store(count + 1, std::memory_order_release);
        return;
    }

    // Slow path (copy-on-write): the block is full or the range must be inserted in
    // the middle. Build a new (larger) block with the range inserted at its sorted
    // position (after the ranges with the same start, so that the most recent one
    // wins), publish it and retire the old one.
    auto capacity = std::max<std::uint32_t>(MinBlockCapacity, count + count / 4 + 1);
    auto* newBlock = AllocateBlock(capacity);
    auto* dst = newBlock->Ranges();
    auto* dstKeys = newBlock->Keys();
    std::uint32_t insertAt = 0;
    if (block != nullptr)
    {
        auto* src = block->Ranges();
        auto* srcKeys = block->Keys();
        insertAt = static_cast<std::uint32_t>(
            std::upper_bound(src, src + count, range.startAddress,
                [](std::uintptr_t value, const StoredRange& r) { return value < r.StartAddress; }) -
            src);
        std::copy(src, src + insertAt, dst);
        std::copy(src + insertAt, src + count, dst + insertAt + 1);
        std::copy(srcKeys, srcKeys + insertAt, dstKeys);
        std::copy(srcKeys + insertAt, srcKeys + count, dstKeys + insertAt + 1);
    }
    dst[insertAt] = StoredRange::From(range);
    dstKeys[insertAt] = key;
    newBlock->Count.store(count + 1, std::memory_order_relaxed);

    _memoryUsage.fetch_add(RangeBlock::SizeFor(newBlock->Capacity), std::memory_order_relaxed);

    // seq_cst: pairs with the seq_cst load in Find (see EpochReclaimer)
    slot->store(newBlock, std::memory_order_seq_cst);

    if (block != nullptr)
    {
        auto size = RangeBlock::SizeFor(block->Capacity);
        _memoryUsage.fetch_sub(size, std::memory_order_relaxed);
        _reclaimer.Retire(block, &FreeBlock, size);
        _reclaimer.TryReclaim();
    }
}

CodeRangeTrie::RangeBlock* CodeRangeTrie::AllocateBlock(std::uint32_t capacity)
{
    auto* memory = ::operator new(RangeBlock::SizeFor(capacity));
    auto* block = new (memory) RangeBlock();
    block->Capacity = capacity;
    block->Count.store(0, std::memory_order_relaxed);
    return block;
}

void CodeRangeTrie::FreeBlock(void* block)
{
    if (block == nullptr)
    {
        return;
    }

    // RangeBlock and StoredRange are trivially destructible
    ::operator delete(block);
}

//
// ModuleRangeSet
//

ModuleRangeSet::ModuleRangeSet(EpochReclaimer& reclaimer) :
    _reclaimer(reclaimer)
{
}

ModuleRangeSet::~ModuleRangeSet()
{
    FreeBlock(_block.load(std::memory_order_relaxed));
}

bool ModuleRangeSet::Contains(std::uintptr_t ip, const EpochReclaimer::ReadGuard&) const noexcept
{
    // seq_cst: pairs with the seq_cst reader-counter increment (see EpochReclaimer)
    auto* block = _block.load(std::memory_order_seq_cst);
    if (block == nullptr)
    {
        return false;
    }

    auto* range = FindLastStartingBefore(block->Ranges(), block->Count, ip);
    return range != nullptr && range->contains(ip);
}

void ModuleRangeSet::Add(const ModuleCodeRange* ranges, std::size_t count)
{
    if (count == 0)
    {
        return;
    }

    std::lock_guard<std::mutex> lock(_writeMutex);

    auto* block = _block.load(std::memory_order_relaxed);
    std::uint32_t oldCount = block == nullptr ? 0 : block->Count;

    auto* newBlock = AllocateBlock(oldCount + static_cast<std::uint32_t>(count));
    auto* dst = newBlock->Ranges();
    if (block != nullptr)
    {
        std::copy(block->Ranges(), block->Ranges() + oldCount, dst);
    }

    // Insert each new range after the ones with the same start (most recent wins)
    auto size = oldCount;
    for (std::size_t i = 0; i < count; i++)
    {
        auto pos = std::upper_bound(dst, dst + size, ranges[i].startAddress,
            [](std::uintptr_t value, const ModuleCodeRange& r) { return value < r.startAddress; });
        std::copy_backward(pos, dst + size, dst + size + 1);
        *pos = ranges[i];
        size++;
    }

    Publish(newBlock);
}

void ModuleRangeSet::Remove(const ModuleCodeRange* ranges, std::size_t count)
{
    if (count == 0)
    {
        return;
    }

    std::lock_guard<std::mutex> lock(_writeMutex);

    auto* block = _block.load(std::memory_order_relaxed);
    if (block == nullptr)
    {
        return;
    }

    auto* newBlock = AllocateBlock(block->Count);
    auto* src = block->Ranges();
    auto* dst = newBlock->Ranges();
    std::uint32_t size = 0;
    for (std::uint32_t i = 0; i < block->Count; i++)
    {
        auto isRemoved = std::any_of(ranges, ranges + count, [&](const ModuleCodeRange& r) {
            return r.startAddress == src[i].startAddress && r.endAddress == src[i].endAddress;
        });
        if (!isRemoved)
        {
            dst[size++] = src[i];
        }
    }

    if (size == block->Count)
    {
        // nothing removed
        FreeBlock(newBlock);
        return;
    }

    newBlock->Count = size;
    Publish(newBlock);
}

void ModuleRangeSet::Publish(ModuleBlock* newBlock)
{
    // seq_cst: pairs with the seq_cst load in Contains (see EpochReclaimer)
    auto* oldBlock = _block.exchange(newBlock, std::memory_order_seq_cst);
    _memoryUsage.store(ModuleBlock::SizeFor(newBlock->Count), std::memory_order_relaxed);
    if (oldBlock != nullptr)
    {
        _reclaimer.Retire(oldBlock, &FreeBlock, ModuleBlock::SizeFor(oldBlock->Count));
        _reclaimer.TryReclaim();
    }
}

ModuleRangeSet::ModuleBlock* ModuleRangeSet::AllocateBlock(std::uint32_t count)
{
    auto* memory = ::operator new(ModuleBlock::SizeFor(count));
    auto* block = new (memory) ModuleBlock();
    block->Count = count;
    return block;
}

void ModuleRangeSet::FreeBlock(void* block)
{
    if (block == nullptr)
    {
        return;
    }

    ::operator delete(block);
}
