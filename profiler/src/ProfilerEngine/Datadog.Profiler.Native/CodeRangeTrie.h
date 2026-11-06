// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EpochReclaimer.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <mutex>
#include <optional>
#include <type_traits>

// Represents a single contiguous code range
struct CodeRange
{
    std::uintptr_t startAddress;
    std::uintptr_t endAddress; // Inclusive
    std::uintptr_t functionId;
    // true when the range was registered from DynamicMethodJITCompilationFinished
    // (IL stubs, DynamicMethod/LCG)
    bool isDynamic = false;

    bool contains(std::uintptr_t ip) const
    {
        return ip >= startAddress && ip <= endAddress;
    }
};

struct ModuleCodeRange
{
    std::uintptr_t startAddress;
    std::uintptr_t endAddress; // Inclusive

    bool contains(std::uintptr_t ip) const
    {
        return ip >= startAddress && ip <= endAddress;
    }
};

static_assert(std::is_trivially_copyable_v<CodeRange>, "CodeRange must be trivially copyable for signal-safe access");
static_assert(std::is_trivially_copyable_v<ModuleCodeRange>, "ModuleCodeRange must be trivially copyable for signal-safe access");

// CodeRangeTrie
// =============
//
// Lock-free (for readers) index of JIT-compiled code ranges, keyed by address.
// See profiler/docs/ManagedCodeCache.md for the full design and benchmarks.
//
// The address space is partitioned in 64KB pages. The page number (ip >> 16) is
// the key of a 4-level radix trie (4KB nodes); each leaf slot points to a block of
// the code ranges that intersect the page (RangeBlock), sorted by start address, or
// holds a tag meaning "page fully covered by an R2R module".
//
//   ip:  | 63..52 | 51..43 | 42..34 | 33..25 | 24..16 | 15..0  |
//        |  = 0   | root   | level1 | level2 | leaf   | offset |
//
// - Readers (possibly in a signal handler) only perform atomic loads, a binary
//   search and the two reader-counter RMWs of the EpochReclaimer. They never block
//   and never allocate.
// - Writers are serialized by _writeMutex (never taken by readers). Interior nodes
//   are never freed while the trie is alive. A RangeBlock is either appended in
//   place (the new range sorts last and there is spare capacity: write the entry,
//   then publish the new count) or replaced by a new copy (copy-on-write) whose old
//   version is retired to the EpochReclaimer.
// - Only addresses below 2^52 are indexed. User-space code is always below that
//   limit (x86_64: 2^47, arm64: 2^48, or 2^52 with LVA): anything above is not
//   managed code (kernel address, PAC-signed return address, garbage IP).
class CodeRangeTrie
{
public:
    static constexpr unsigned PageShift = 16;
    static constexpr unsigned MaxAddressBits = 52;
    // Radix trie levels (root first): number of page number bits resolved by each
    // level. The root is stored inline, the other nodes are allocated on demand.
    static constexpr unsigned Levels = 4;
    static constexpr unsigned LevelBits[Levels] = {9, 9, 9, 9};

    explicit CodeRangeTrie(EpochReclaimer& reclaimer);
    ~CodeRangeTrie();

    CodeRangeTrie(const CodeRangeTrie&) = delete;
    CodeRangeTrie& operator=(const CodeRangeTrie&) = delete;

    // Signal-safe. The caller must hold a read guard on the reclaimer passed to the
    // constructor for as long as it uses the result.
    std::optional<CodeRange> Find(std::uintptr_t ip, const EpochReclaimer::ReadGuard&) const noexcept;
    // Signal-safe. Same as Find(ip).has_value(), but only reads the compact keys.
    bool Contains(std::uintptr_t ip, const EpochReclaimer::ReadGuard&) const noexcept;

    // Not signal-safe. Returns false (and does nothing) if the range is not indexable.
    bool Insert(const CodeRange& range);

    // Not signal-safe. Marks (or unmarks) the pages fully covered by an R2R module
    // executable section: Contains returns true for any IP in them without having to
    // look into the ModuleRangeSet. Pages already containing JIT-compiled code (never
    // expected: JIT code is not allocated inside a mapped image) are left untouched.
    void MarkModulePages(const ModuleCodeRange& range, bool isLoaded);

    // Bytes currently allocated by the trie (nodes + live blocks)
    std::size_t MemoryUsage() const noexcept
    {
        return _memoryUsage.load(std::memory_order_relaxed);
    }

    static bool IsIndexable(std::uintptr_t address) noexcept
    {
        return (static_cast<std::uint64_t>(address) >> MaxAddressBits) == 0;
    }

#ifdef DD_TEST
    std::unique_lock<std::mutex> LockWriterForTest()
    {
        return std::unique_lock<std::mutex>(_writeMutex);
    }
#endif

private:
    // Page-relative bounds of a range: Start is the offset of the first byte in the
    // 64KB page (0 when the range starts in a previous page), End the offset of the
    // last byte (0xFFFF when the range ends in a following page).
    struct Key
    {
        std::uint16_t Start;
        std::uint16_t End;
    };

    // Compact storage of a CodeRange (24 bytes instead of 32 on 64-bit)
    struct StoredRange
    {
        std::uintptr_t StartAddress;
        std::uintptr_t FunctionId;
        std::uint32_t LastOffset : 31; // endAddress - startAddress
        std::uint32_t IsDynamic : 1;

        static StoredRange From(const CodeRange& range) noexcept
        {
            return {range.startAddress, range.functionId,
                    static_cast<std::uint32_t>(range.endAddress - range.startAddress), range.isDynamic ? 1u : 0u};
        }

        CodeRange ToCodeRange() const noexcept
        {
            return {StartAddress, StartAddress + LastOffset, FunctionId, IsDynamic != 0};
        }
    };
    static constexpr std::uintptr_t MaxRangeLastOffset = (std::uintptr_t{1} << 31) - 1;

    // Layout: | header | Keys[Capacity] (padded) | Ranges[Capacity] |
    // The binary search runs on the compact keys (16 per cache line instead of 2
    // StoredRange). IsManaged is answered from the keys only; the StoredRange is
    // only read to get the function id.
    struct alignas(alignof(StoredRange)) RangeBlock
    {
        std::uint32_t Capacity;
        std::atomic<std::uint32_t> Count;

        Key* Keys() noexcept
        {
            return reinterpret_cast<Key*>(this + 1);
        }

        const Key* Keys() const noexcept
        {
            return reinterpret_cast<const Key*>(this + 1);
        }

        StoredRange* Ranges() noexcept
        {
            return reinterpret_cast<StoredRange*>(reinterpret_cast<char*>(this + 1) + KeysSize(Capacity));
        }

        const StoredRange* Ranges() const noexcept
        {
            return reinterpret_cast<const StoredRange*>(reinterpret_cast<const char*>(this + 1) + KeysSize(Capacity));
        }

        static std::size_t KeysSize(std::uint32_t capacity) noexcept
        {
            return (capacity * sizeof(Key) + alignof(StoredRange) - 1) & ~(alignof(StoredRange) - 1);
        }

        static std::size_t SizeFor(std::uint32_t capacity) noexcept
        {
            return sizeof(RangeBlock) + KeysSize(capacity) + capacity * sizeof(StoredRange);
        }
    };
    static_assert(sizeof(RangeBlock) % alignof(StoredRange) == 0, "keys and ranges must be aligned after the header");

    // A node is an array of slots: interior slots point to the next level's node,
    // leaf slots point to a RangeBlock (or hold ModulePageTag()).
    using Slot = std::atomic<void*>;

    static constexpr unsigned LevelShift(unsigned level)
    {
        unsigned shift = 0;
        for (unsigned i = level + 1; i < Levels; i++)
        {
            shift += LevelBits[i];
        }
        return shift;
    }

    static constexpr std::size_t LevelIndex(std::uint64_t page, unsigned level)
    {
        return static_cast<std::size_t>((page >> LevelShift(level)) & ((1ull << LevelBits[level]) - 1));
    }

    static constexpr unsigned TotalLevelBits()
    {
        unsigned total = 0;
        for (unsigned i = 0; i < Levels; i++)
        {
            total += LevelBits[i];
        }
        return total;
    }

    static constexpr std::uint32_t MinBlockCapacity = 4;

    // Leaf slot value of a page fully covered by an R2R module (never dereferenced)
    static RangeBlock* ModulePageTag() noexcept
    {
        return reinterpret_cast<RangeBlock*>(std::uintptr_t{1});
    }
    static constexpr std::uint64_t PageMask = (1ull << PageShift) - 1;

    Slot* GetOrCreateSlot(std::uint64_t page);
    Slot* FindSlot(std::uint64_t page) const noexcept;
    void InsertIntoSlot(Slot* slot, std::uint64_t page, const CodeRange& range);
    void FreeNode(Slot* node, unsigned level);

    static Key KeyFor(const CodeRange& range, std::uint64_t page) noexcept
    {
        auto pageStart = page << PageShift;
        auto pageEnd = pageStart + PageMask;
        return {
            range.startAddress <= pageStart ? std::uint16_t{0} : static_cast<std::uint16_t>(range.startAddress - pageStart),
            range.endAddress >= pageEnd ? static_cast<std::uint16_t>(PageMask) : static_cast<std::uint16_t>(range.endAddress - pageStart)};
    }

    // Returns the block of the page containing ip and the index of the last range
    // starting before ip in it, ModulePageTag() or nullptr
    const RangeBlock* FindCandidate(std::uintptr_t ip, std::uint32_t& index) const noexcept;

    static RangeBlock* AllocateBlock(std::uint32_t capacity);
    static void FreeBlock(void* block);

    EpochReclaimer& _reclaimer;
    std::mutex _writeMutex;
    std::atomic<std::size_t> _memoryUsage{0};
    Slot _root[1u << LevelBits[0]];

    static_assert(Slot::is_always_lock_free, "the read side must be lock-free to be signal-safe");
    static_assert(std::atomic<std::uint32_t>::is_always_lock_free, "the read side must be lock-free to be signal-safe");
};

// ModuleRangeSet
// ==============
//
// Lock-free (for readers) set of the executable sections of the loaded R2R/NGEN
// modules. Modules are loaded/unloaded rarely, so the whole sorted array is
// copy-on-write: writers publish a new block and retire the old one to the
// EpochReclaimer.
class ModuleRangeSet
{
public:
    explicit ModuleRangeSet(EpochReclaimer& reclaimer);
    ~ModuleRangeSet();

    ModuleRangeSet(const ModuleRangeSet&) = delete;
    ModuleRangeSet& operator=(const ModuleRangeSet&) = delete;

    // Signal-safe. The caller must hold a read guard on the reclaimer.
    bool Contains(std::uintptr_t ip, const EpochReclaimer::ReadGuard&) const noexcept;

    // Not signal-safe
    void Add(const ModuleCodeRange* ranges, std::size_t count);
    void Remove(const ModuleCodeRange* ranges, std::size_t count);

    std::size_t MemoryUsage() const noexcept
    {
        return _memoryUsage.load(std::memory_order_relaxed);
    }

#ifdef DD_TEST
    std::unique_lock<std::mutex> LockWriterForTest()
    {
        return std::unique_lock<std::mutex>(_writeMutex);
    }
#endif

private:
    struct alignas(alignof(ModuleCodeRange)) ModuleBlock
    {
        std::uint32_t Count;

        ModuleCodeRange* Ranges() noexcept
        {
            return reinterpret_cast<ModuleCodeRange*>(this + 1);
        }

        const ModuleCodeRange* Ranges() const noexcept
        {
            return reinterpret_cast<const ModuleCodeRange*>(this + 1);
        }

        static std::size_t SizeFor(std::uint32_t count) noexcept
        {
            return sizeof(ModuleBlock) + count * sizeof(ModuleCodeRange);
        }
    };

    static ModuleBlock* AllocateBlock(std::uint32_t count);
    static void FreeBlock(void* block);
    void Publish(ModuleBlock* newBlock);

    EpochReclaimer& _reclaimer;
    std::mutex _writeMutex;
    std::atomic<ModuleBlock*> _block{nullptr};
    std::atomic<std::size_t> _memoryUsage{0};

    static_assert(std::atomic<ModuleBlock*>::is_always_lock_free, "the read side must be lock-free to be signal-safe");
};
