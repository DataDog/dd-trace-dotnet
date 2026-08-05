// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "GCHeapRangeSet.h"
#include "MockProfilerInfo.h"

#include <cstdint>
#include <vector>

namespace
{
constexpr uintptr_t BS = GCHeapRangeSet::BlockSize;

// A base address that is already block-aligned so rounding math is easy to reason about.
constexpr uintptr_t Base = BS * 64; // 64 * 64MB = 4 GB

// Mock that returns a configurable set of generation ranges via the standard
// two-call GetGenerationBounds pattern (count first, then fill).
class GenerationBoundsMockProfiler : public MockProfilerInfo
{
public:
    void AddGenerationRange(COR_PRF_GC_GENERATION generation, uintptr_t start, size_t committed, size_t reserved)
    {
        COR_PRF_GC_GENERATION_RANGE range{};
        range.generation = generation;
        range.rangeStart = static_cast<ObjectID>(start);
        range.rangeLength = static_cast<UINT_PTR>(committed);
        range.rangeLengthReserved = static_cast<UINT_PTR>(reserved);
        _ranges.push_back(range);
    }

    HRESULT STDMETHODCALLTYPE GetGenerationBounds(ULONG cObjectRanges, ULONG* pcObjectRanges, COR_PRF_GC_GENERATION_RANGE ranges[]) override
    {
        if (pcObjectRanges == nullptr)
        {
            return E_POINTER;
        }

        *pcObjectRanges = static_cast<ULONG>(_ranges.size());

        if (ranges == nullptr || cObjectRanges == 0)
        {
            // Count-only probe.
            return S_OK;
        }

        ULONG toCopy = cObjectRanges < static_cast<ULONG>(_ranges.size()) ? cObjectRanges : static_cast<ULONG>(_ranges.size());
        for (ULONG i = 0; i < toCopy; i++)
        {
            ranges[i] = _ranges[i];
        }
        *pcObjectRanges = toCopy;
        return S_OK;
    }

private:
    std::vector<COR_PRF_GC_GENERATION_RANGE> _ranges;
};
} // namespace

TEST(GCHeapRangeSetTest, EmptySetAcceptsEverything)
{
    GCHeapRangeSet set;
    ASSERT_TRUE(set.IsEmpty());
    ASSERT_TRUE(set.Contains(0));
    ASSERT_TRUE(set.Contains(0x1000));
    ASSERT_TRUE(set.Contains(Base));
    ASSERT_EQ(set.GetRangeCount(), 0u);
}

TEST(GCHeapRangeSetTest, SingleRangeBoundaryContainment)
{
    GCHeapRangeSet set;
    set.AddRange(Base, 0x1000);
    set.Finalize();

    ASSERT_FALSE(set.IsEmpty());
    ASSERT_EQ(set.GetRangeCount(), 1u);

    // Rounded outward to [Base, Base + BlockSize).
    ASSERT_TRUE(set.Contains(Base));            // inclusive start
    ASSERT_TRUE(set.Contains(Base + 0x1000));   // inside the block, past requested end
    ASSERT_TRUE(set.Contains(Base + BS - 1));   // last byte of the block
    ASSERT_FALSE(set.Contains(Base + BS));      // exclusive end
    ASSERT_FALSE(set.Contains(Base - 1));       // just before start
}

TEST(GCHeapRangeSetTest, OutwardRoundingCoversPartialBlock)
{
    GCHeapRangeSet set;
    // Start slightly above a block boundary; both start and end round outward.
    set.AddRange(Base + 0x100, 0x200);
    set.Finalize();

    ASSERT_EQ(set.GetRangeCount(), 1u);
    ASSERT_TRUE(set.Contains(Base));            // rounded down below the requested start
    ASSERT_TRUE(set.Contains(Base + BS - 1));
    ASSERT_FALSE(set.Contains(Base + BS));
}

TEST(GCHeapRangeSetTest, AdjacentRangesMerge)
{
    GCHeapRangeSet set;
    set.AddRange(Base, BS);          // [Base, Base + BS)
    set.AddRange(Base + BS, BS);     // [Base + BS, Base + 2*BS) -- adjacent
    set.Finalize();

    ASSERT_EQ(set.GetRangeCount(), 1u);
    ASSERT_TRUE(set.Contains(Base));
    ASSERT_TRUE(set.Contains(Base + BS));
    ASSERT_TRUE(set.Contains(Base + 2 * BS - 1));
    ASSERT_FALSE(set.Contains(Base + 2 * BS));
}

TEST(GCHeapRangeSetTest, OverlappingRangesMerge)
{
    GCHeapRangeSet set;
    set.AddRange(Base, 2 * BS);          // [Base, Base + 2*BS)
    set.AddRange(Base + BS, 2 * BS);     // [Base + BS, Base + 3*BS) -- overlaps
    set.Finalize();

    ASSERT_EQ(set.GetRangeCount(), 1u);
    ASSERT_TRUE(set.Contains(Base + 3 * BS - 1));
    ASSERT_FALSE(set.Contains(Base + 3 * BS));
}

TEST(GCHeapRangeSetTest, DisjointRangesStaySeparate)
{
    GCHeapRangeSet set;
    set.AddRange(Base, BS);                // block A
    set.AddRange(Base + 10 * BS, BS);      // block B, far away
    set.Finalize();

    ASSERT_EQ(set.GetRangeCount(), 2u);
    ASSERT_TRUE(set.Contains(Base));
    ASSERT_FALSE(set.Contains(Base + 5 * BS)); // gap between the two blocks
    ASSERT_TRUE(set.Contains(Base + 10 * BS));
}

TEST(GCHeapRangeSetTest, BinarySearchWithManyRanges)
{
    GCHeapRangeSet set;
    // 100 disjoint blocks separated by a two-block gap each.
    for (int i = 0; i < 100; i++)
    {
        set.AddRange(Base + static_cast<uintptr_t>(i) * 3 * BS, BS);
    }
    set.Finalize();

    ASSERT_EQ(set.GetRangeCount(), 100u);

    // Inside block 50.
    ASSERT_TRUE(set.Contains(Base + 50 * 3 * BS));
    ASSERT_TRUE(set.Contains(Base + 50 * 3 * BS + BS - 1));
    // In the gap after block 50.
    ASSERT_FALSE(set.Contains(Base + 50 * 3 * BS + BS));
    // Inside the last block.
    ASSERT_TRUE(set.Contains(Base + 99 * 3 * BS));
}

TEST(GCHeapRangeSetTest, AddAddressGrowsNonEmptySet)
{
    GCHeapRangeSet set;
    set.AddRange(Base, BS);
    set.Finalize();
    ASSERT_EQ(set.GetRangeCount(), 1u);

    // An address already covered does not grow the set.
    set.AddAddress(Base + 0x10);
    ASSERT_EQ(set.GetRangeCount(), 1u);

    // A far-away address adds a new block and becomes contained.
    uintptr_t farAway = Base + 20 * BS;
    ASSERT_FALSE(set.Contains(farAway));
    set.AddAddress(farAway);
    ASSERT_TRUE(set.Contains(farAway));
    ASSERT_EQ(set.GetRangeCount(), 2u);
}

TEST(GCHeapRangeSetTest, ClearResetsToAcceptAll)
{
    GCHeapRangeSet set;
    set.AddRange(Base, BS);
    set.Finalize();
    ASSERT_FALSE(set.IsEmpty());
    ASSERT_FALSE(set.Contains(Base + 100 * BS));

    set.Clear();
    ASSERT_TRUE(set.IsEmpty());
    ASSERT_TRUE(set.Contains(Base + 100 * BS)); // empty accepts everything again
}

TEST(GCHeapRangeSetTest, MemorySizeIsSane)
{
    GCHeapRangeSet set;
    ASSERT_GE(set.GetMemorySize(), sizeof(GCHeapRangeSet));

    set.AddRange(Base, BS);
    set.Finalize();
    ASSERT_GT(set.GetMemorySize(), sizeof(GCHeapRangeSet));
}

// Exercises the exact two-call sequence HeapSnapshotManager::SeedHeapRanges uses,
// feeding a mock's generation bounds into a GCHeapRangeSet (rangeLengthReserved wins).
TEST(GCHeapRangeSetTest, SeedsFromGenerationBoundsTwoCallPattern)
{
    GenerationBoundsMockProfiler profiler;
    profiler.AddGenerationRange(COR_PRF_GC_GEN_0, Base, BS / 2, BS);
    profiler.AddGenerationRange(COR_PRF_GC_GEN_2, Base + 10 * BS, BS, 2 * BS);
    profiler.AddGenerationRange(COR_PRF_GC_LARGE_OBJECT_HEAP, Base + 30 * BS, BS, BS);

    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));

    ULONG count = 0;
    ASSERT_EQ(pInfo->GetGenerationBounds(0, &count, nullptr), S_OK);
    ASSERT_EQ(count, 3u);

    std::vector<COR_PRF_GC_GENERATION_RANGE> ranges(count);
    ULONG written = 0;
    ASSERT_EQ(pInfo->GetGenerationBounds(count, &written, ranges.data()), S_OK);
    ASSERT_EQ(written, 3u);

    GCHeapRangeSet set;
    for (ULONG i = 0; i < written; i++)
    {
        const auto& r = ranges[i];
        size_t length = static_cast<size_t>(r.rangeLengthReserved != 0 ? r.rangeLengthReserved : r.rangeLength);
        set.AddRange(static_cast<uintptr_t>(r.rangeStart), length);
    }
    set.Finalize();

    // gen0 reserved covers a full block even though only half is committed.
    ASSERT_TRUE(set.Contains(Base + BS - 1));
    // gen2 reserved spans two blocks.
    ASSERT_TRUE(set.Contains(Base + 11 * BS));
    ASSERT_FALSE(set.Contains(Base + 12 * BS));
    // LOH.
    ASSERT_TRUE(set.Contains(Base + 30 * BS));
    // A gap between generations is rejected.
    ASSERT_FALSE(set.Contains(Base + 20 * BS));
}
