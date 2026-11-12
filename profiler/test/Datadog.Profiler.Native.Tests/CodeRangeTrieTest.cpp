// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CodeRangeTrie.h"
#include "EpochReclaimer.h"

#include <atomic>
#include <cstdint>
#include <thread>
#include <vector>

namespace {

std::optional<std::uintptr_t> FindFunction(const CodeRangeTrie& trie, const EpochReclaimer& reclaimer, std::uintptr_t ip)
{
    EpochReclaimer::ReadGuard guard(reclaimer);
    auto range = trie.Find(ip, guard);
    if (!range.has_value())
    {
        return std::nullopt;
    }
    return range->functionId;
}

bool ModuleContains(const ModuleRangeSet& modules, const EpochReclaimer& reclaimer, std::uintptr_t ip)
{
    EpochReclaimer::ReadGuard guard(reclaimer);
    return modules.Contains(ip, guard);
}

} // namespace

TEST(CodeRangeTrieTest, Find_Boundaries)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    ASSERT_TRUE(trie.Insert({0x10000, 0x100FF, 1, false}));

    EXPECT_FALSE(FindFunction(trie, reclaimer, 0xFFFF).has_value());
    EXPECT_EQ(1u, FindFunction(trie, reclaimer, 0x10000));
    EXPECT_EQ(1u, FindFunction(trie, reclaimer, 0x10080));
    EXPECT_EQ(1u, FindFunction(trie, reclaimer, 0x100FF));
    EXPECT_FALSE(FindFunction(trie, reclaimer, 0x10100).has_value());
    EXPECT_FALSE(FindFunction(trie, reclaimer, 0).has_value());
}

TEST(CodeRangeTrieTest, Find_EmptyTrie)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    EXPECT_FALSE(FindFunction(trie, reclaimer, 0).has_value());
    EXPECT_FALSE(FindFunction(trie, reclaimer, 0x7FFF12345678).has_value());
    EXPECT_FALSE(FindFunction(trie, reclaimer, UINTPTR_MAX).has_value());
}

TEST(CodeRangeTrieTest, Insert_RangeSpanningSeveralPagesAndTrieNodes)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    // Crosses a leaf node boundary (2^28) and spans 3 pages
    const std::uintptr_t start = (std::uintptr_t{1} << 28) - 0x18000;
    const std::uintptr_t end = (std::uintptr_t{1} << 28) + 0x10000;
    ASSERT_TRUE(trie.Insert({start, end, 7, true}));

    for (auto ip = start; ip <= end; ip += 0x1000)
    {
        EXPECT_EQ(7u, FindFunction(trie, reclaimer, ip)) << std::hex << ip;
    }
    EXPECT_EQ(7u, FindFunction(trie, reclaimer, end));
    EXPECT_FALSE(FindFunction(trie, reclaimer, start - 1).has_value());
    EXPECT_FALSE(FindFunction(trie, reclaimer, end + 1).has_value());

    EpochReclaimer::ReadGuard guard(reclaimer);
    EXPECT_TRUE(trie.Find(start, guard)->isDynamic);
}

TEST(CodeRangeTrieTest, Insert_OutOfOrderAndInOrder_AllFound)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    constexpr std::uintptr_t Base = 0x7F1200000000;
    constexpr int Count = 2000; // ~2 pages worth of 64-byte functions

    // Insert odd functions in order, then even functions in reverse order
    for (int i = 1; i < Count; i += 2)
    {
        ASSERT_TRUE(trie.Insert({Base + i * 64, Base + i * 64 + 47, static_cast<std::uintptr_t>(i), false}));
    }
    for (int i = Count - 2; i >= 0; i -= 2)
    {
        ASSERT_TRUE(trie.Insert({Base + i * 64, Base + i * 64 + 47, static_cast<std::uintptr_t>(i), false}));
    }

    for (int i = 0; i < Count; i++)
    {
        EXPECT_EQ(static_cast<std::uintptr_t>(i), FindFunction(trie, reclaimer, Base + i * 64 + 10));
        EXPECT_FALSE(FindFunction(trie, reclaimer, Base + i * 64 + 48).has_value());
    }
}

TEST(CodeRangeTrieTest, Insert_SameStart_MostRecentWins)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    ASSERT_TRUE(trie.Insert({0x20000, 0x200FF, 1, false}));
    ASSERT_TRUE(trie.Insert({0x20000, 0x201FF, 2, false}));

    EXPECT_EQ(2u, FindFunction(trie, reclaimer, 0x20010));
    EXPECT_EQ(2u, FindFunction(trie, reclaimer, 0x20180));
}

TEST(CodeRangeTrieTest, Insert_NotIndexable_Rejected)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    const std::uintptr_t limit = std::uintptr_t{1} << CodeRangeTrie::MaxAddressBits;

    EXPECT_FALSE(trie.Insert({limit, limit + 0x10, 1, false}));
    EXPECT_FALSE(trie.Insert({limit - 0x10, limit + 0x10, 1, false}));
    EXPECT_FALSE(trie.Insert({0x2000, 0x1000, 1, false})); // end < start
    EXPECT_TRUE(trie.Insert({limit - 0x10, limit - 1, 2, false}));

    EXPECT_EQ(2u, FindFunction(trie, reclaimer, limit - 1));
    EXPECT_FALSE(FindFunction(trie, reclaimer, limit).has_value());
    EXPECT_FALSE(FindFunction(trie, reclaimer, 0xFFFF000000001000).has_value());
}

TEST(CodeRangeTrieTest, Insert_SingleByteRange)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    ASSERT_TRUE(trie.Insert({0x30000, 0x30000, 3, false}));
    EXPECT_EQ(3u, FindFunction(trie, reclaimer, 0x30000));
    EXPECT_FALSE(FindFunction(trie, reclaimer, 0x30001).has_value());
}

TEST(CodeRangeTrieTest, Contains_ConsistentWithFind)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    // Ranges of various sizes (some crossing pages), gaps, and a few out of order
    constexpr std::uintptr_t Base = 0x7F5600000000 - 0x3000;
    std::uint64_t seed = 12345;
    auto next = [&seed]() {
        seed ^= seed << 13;
        seed ^= seed >> 7;
        seed ^= seed << 17;
        return seed;
    };

    std::uintptr_t address = Base;
    for (int i = 0; i < 3000; i++)
    {
        auto size = 16 + next() % (i % 50 == 0 ? 0x30000 : 0x400);
        ASSERT_TRUE(trie.Insert({address, address + size - 1, static_cast<std::uintptr_t>(i), false}));
        address += size + (next() % 3) * 16;
    }

    EpochReclaimer::ReadGuard guard(reclaimer);
    for (std::uintptr_t ip = Base - 0x100; ip < address + 0x100; ip += 1 + next() % 61)
    {
        ASSERT_EQ(trie.Find(ip, guard).has_value(), trie.Contains(ip, guard)) << std::hex << ip;
    }
}

TEST(CodeRangeTrieTest, MarkModulePages_OnlyFullyCoveredPages)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    // [0x7F0000008000, 0x7F000003FFFF]: page 0x7F0000000000 is partially covered,
    // pages 0x...10000 to 0x...30000 are fully covered
    ModuleCodeRange module{0x7F0000008000, 0x7F000003FFFF};
    trie.MarkModulePages(module, true);

    EpochReclaimer::ReadGuard guard(reclaimer);
    EXPECT_FALSE(trie.Contains(0x7F0000008000, guard)); // partial page: left to the ModuleRangeSet
    EXPECT_TRUE(trie.Contains(0x7F0000010000, guard));
    EXPECT_TRUE(trie.Contains(0x7F000003FFFF, guard));
    EXPECT_FALSE(trie.Contains(0x7F0000040000, guard));
    EXPECT_FALSE(trie.Find(0x7F0000010000, guard).has_value()); // no function id for R2R code

    // JIT code in a marked page replaces the mark
    ASSERT_TRUE(trie.Insert({0x7F0000020000, 0x7F00000200FF, 9, false}));
    EXPECT_TRUE(trie.Contains(0x7F0000020010, guard));
    EXPECT_FALSE(trie.Contains(0x7F0000020100, guard));
    EXPECT_EQ(9u, trie.Find(0x7F0000020010, guard)->functionId);

    trie.MarkModulePages(module, false);
    EXPECT_FALSE(trie.Contains(0x7F0000010000, guard));
    EXPECT_FALSE(trie.Contains(0x7F0000030000, guard));
    EXPECT_TRUE(trie.Contains(0x7F0000020010, guard));

    // A module smaller than a page does not mark anything
    trie.MarkModulePages({0x7F0000051000, 0x7F0000052FFF}, true);
    EXPECT_FALSE(trie.Contains(0x7F0000051000, guard));
}

TEST(EpochReclaimerTest, RetiredMemory_FreedOnlyAfterReadersLeave)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    ASSERT_TRUE(trie.Insert({0x40100, 0x401FF, 1, false}));

    {
        // A reader holding a guard pins the memory it may have loaded
        EpochReclaimer::ReadGuard guard(reclaimer);
        auto range = trie.Find(0x40100, guard);
        ASSERT_TRUE(range.has_value());

        // Out-of-order insert: the block of the page is copied and the old one retired
        ASSERT_TRUE(trie.Insert({0x40000, 0x400FF, 2, false}));
        EXPECT_GT(reclaimer.PendingBytes(), 0u);

        // More writes cannot free it while the reader is still there
        ASSERT_TRUE(trie.Insert({0x40000, 0x4000F, 3, false}));
        EXPECT_GT(reclaimer.PendingBytes(), 0u);

        EXPECT_EQ(1u, range->functionId);
    }

    // Once the reader left, the next write frees everything that was retired
    reclaimer.TryReclaim();
    EXPECT_EQ(0u, reclaimer.PendingBytes());
}

TEST(EpochReclaimerTest, ConcurrentReadersAndWriter_NoFalseNegative)
{
    EpochReclaimer reclaimer;
    CodeRangeTrie trie(reclaimer);

    constexpr std::uintptr_t Base = 0x7F3400000000;
    constexpr int Count = 20000;

    std::vector<std::atomic<bool>> added(Count);
    std::atomic<bool> stop{false};
    std::atomic<int> failures{0};

    std::vector<std::thread> readers;
    for (int t = 0; t < 4; t++)
    {
        readers.emplace_back([&, t]() {
            std::uint32_t seed = 2463534242u + t;
            while (!stop.load(std::memory_order_relaxed))
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                int i = static_cast<int>(seed % Count);
                bool wasAdded = added[i].load(std::memory_order_acquire);
                auto found = FindFunction(trie, reclaimer, Base + i * 32 + 4);
                if (wasAdded && found != static_cast<std::uintptr_t>(i))
                {
                    failures++;
                }
                if (!wasAdded && found.has_value() && found != static_cast<std::uintptr_t>(i))
                {
                    failures++;
                }
            }
        });
    }

    // Reverse order: every insert copies the page block and retires the old one
    for (int i = Count - 1; i >= 0; i--)
    {
        trie.Insert({Base + i * 32, Base + i * 32 + 15, static_cast<std::uintptr_t>(i), false});
        added[i].store(true, std::memory_order_release);
    }

    stop = true;
    for (auto& reader : readers)
    {
        reader.join();
    }

    EXPECT_EQ(0, failures.load());
    reclaimer.TryReclaim();
    EXPECT_EQ(0u, reclaimer.PendingBytes());
}

TEST(ModuleRangeSetTest, AddRemove)
{
    EpochReclaimer reclaimer;
    ModuleRangeSet modules(reclaimer);

    EXPECT_FALSE(ModuleContains(modules, reclaimer, 0x1000));

    ModuleCodeRange ranges[] = {{0x300000, 0x3FFFFF}, {0x100000, 0x1FFFFF}};
    modules.Add(ranges, 2);

    EXPECT_FALSE(ModuleContains(modules, reclaimer, 0xFFFFF));
    EXPECT_TRUE(ModuleContains(modules, reclaimer, 0x100000));
    EXPECT_TRUE(ModuleContains(modules, reclaimer, 0x1FFFFF));
    EXPECT_FALSE(ModuleContains(modules, reclaimer, 0x200000));
    EXPECT_TRUE(ModuleContains(modules, reclaimer, 0x350000));
    EXPECT_FALSE(ModuleContains(modules, reclaimer, 0x400000));

    modules.Remove(&ranges[1], 1);
    EXPECT_FALSE(ModuleContains(modules, reclaimer, 0x100000));
    EXPECT_TRUE(ModuleContains(modules, reclaimer, 0x350000));

    // Removing an unknown range is a no-op
    ModuleCodeRange unknown{0x500000, 0x5FFFFF};
    modules.Remove(&unknown, 1);
    EXPECT_TRUE(ModuleContains(modules, reclaimer, 0x350000));

    // Re-adding at the same place works
    modules.Add(&ranges[1], 1);
    EXPECT_TRUE(ModuleContains(modules, reclaimer, 0x100000));

    reclaimer.TryReclaim();
    EXPECT_EQ(0u, reclaimer.PendingBytes());
}
