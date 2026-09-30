// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "AddressRegion.h"
#include "AddressSpaceMap.h"
#include "OsSpecificApi.h"
#ifdef _WINDOWS
#include "ScopeFinalizer.h"
#include "ScopedHandle.h"
#endif

#include <memory>
#include <vector>

#ifdef _WINDOWS
#include <windows.h>
#endif

namespace
{
// Builds a committed run.
AddressRegion Committed(uintptr_t address, uint64_t size, RegionCategory category = RegionCategory::PrivateData)
{
    AddressRegion r;
    r.Address = address;
    r.Size = size;
    r.Committed = size;
    r.Category = category;
    return r;
}

// Builds a reserved (uncommitted) run.
AddressRegion Reserved(uintptr_t address, uint64_t size)
{
    AddressRegion r;
    r.Address = address;
    r.Size = size;
    r.Committed = 0;
    r.Category = RegionCategory::Reserved;
    return r;
}
} // namespace

// --- GetCommittedBytes on a hand-built map (no syscalls) ----------------------------------------

TEST(AddressSpaceMapTest, FullyCommittedRangeIsCounted)
{
    std::vector<AddressRegion> regions{Committed(0x10000, 0x4000)};
    AddressSpaceMap map(std::move(regions), true, false);

    EXPECT_EQ(map.GetCommittedBytes(0x10000, 0x4000), 0x4000u);
}

TEST(AddressSpaceMapTest, PartialOverlapIsClippedToTheQueryWindow)
{
    std::vector<AddressRegion> regions{Committed(0x10000, 0x4000)};
    AddressSpaceMap map(std::move(regions), true, false);

    // Query only the middle 0x1000 of the committed run.
    EXPECT_EQ(map.GetCommittedBytes(0x11000, 0x1000), 0x1000u);
}

TEST(AddressSpaceMapTest, ReservedRangeReportsZeroCommitted)
{
    std::vector<AddressRegion> regions{Reserved(0x10000, 0x4000)};
    AddressSpaceMap map(std::move(regions), true, false);

    EXPECT_EQ(map.GetCommittedBytes(0x10000, 0x4000), 0u);
}

TEST(AddressSpaceMapTest, ScatteredCommitsAcrossReservedGapsAreAllCounted)
{
    // committed | reserved gap | committed  -> gap-aware sum must include both committed runs.
    std::vector<AddressRegion> regions{
        Committed(0x10000, 0x1000),
        Reserved(0x11000, 0x1E000),
        Committed(0x2F000, 0x1000),
    };
    AddressSpaceMap map(std::move(regions), true, false);

    EXPECT_EQ(map.GetCommittedBytes(0x10000, 0x20000), 0x2000u);
}

TEST(AddressSpaceMapTest, CommittedIsCappedAtQuerySize)
{
    std::vector<AddressRegion> regions{Committed(0x10000, 0x10000)};
    AddressSpaceMap map(std::move(regions), true, false);

    // Query smaller than the run: result capped at the query size.
    EXPECT_EQ(map.GetCommittedBytes(0x10000, 0x2000), 0x2000u);
}

TEST(AddressSpaceMapTest, NullOrZeroQueryReportsZero)
{
    std::vector<AddressRegion> regions{Committed(0x10000, 0x4000)};
    AddressSpaceMap map(std::move(regions), true, false);

    EXPECT_EQ(map.GetCommittedBytes(0, 0x1000), 0u);
    EXPECT_EQ(map.GetCommittedBytes(0x10000, 0), 0u);
}

// --- GetRss -------------------------------------------------------------------------------------

TEST(AddressSpaceMapTest, RssIsSummedOverOverlappingRuns)
{
    AddressRegion a;
    a.Address = 0x10000;
    a.Size = 0x2000;
    a.Rss = 0x2000;
    AddressRegion b;
    b.Address = 0x12000;
    b.Size = 0x2000;
    b.Rss = 0x1000; // partially resident

    std::vector<AddressRegion> regions{a, b};
    AddressSpaceMap map(std::move(regions), false, true);

    EXPECT_EQ(map.GetRss(0x10000, 0x4000), 0x3000u);
}

// --- TryGetRegion -------------------------------------------------------------------------------

TEST(AddressSpaceMapTest, TryGetRegionFindsTheContainingRun)
{
    std::vector<AddressRegion> regions{
        Committed(0x10000, 0x1000, RegionCategory::Image),
        Committed(0x20000, 0x1000, RegionCategory::Stack),
    };
    AddressSpaceMap map(std::move(regions), true, false);

    AddressRegion found;
    ASSERT_TRUE(map.TryGetRegion(0x20500, found));
    EXPECT_EQ(found.Address, 0x20000u);
    EXPECT_EQ(found.Category, RegionCategory::Stack);

    EXPECT_FALSE(map.TryGetRegion(0x15000, found)); // in the gap between the two runs
}

TEST(AddressSpaceMapTest, ProvidesFlagsReflectConstruction)
{
    AddressSpaceMap winLike(std::vector<AddressRegion>{Committed(0x10000, 0x1000)}, true, false);
    EXPECT_TRUE(winLike.ProvidesCommitted());
    EXPECT_FALSE(winLike.ProvidesRss());

    AddressSpaceMap linuxLike(std::vector<AddressRegion>{Committed(0x10000, 0x1000)}, false, true);
    EXPECT_FALSE(linuxLike.ProvidesCommitted());
    EXPECT_TRUE(linuxLike.ProvidesRss());
}

// --- live capture of the current process --------------------------------------------------------

TEST(AddressSpaceMapTest, CaptureCurrentProcessIsNonEmptyWithImageModules)
{
#ifdef _WINDOWS
    ScopedHandle pageFileMapping{
        ::CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, 0x1000, nullptr)};
    ASSERT_TRUE(pageFileMapping.IsValid());

    void* pageFileView = ::MapViewOfFile(pageFileMapping, FILE_MAP_WRITE, 0, 0, 0);
    ASSERT_NE(pageFileView, nullptr);
    on_leave { ::UnmapViewOfFile(pageFileView); };
#endif

    auto map = OsSpecificApi::CaptureAddressSpaceMap();
    ASSERT_NE(map, nullptr);
    ASSERT_TRUE(map->IsAvailable());
#ifdef _WINDOWS
    EXPECT_TRUE(map->ProvidesCommitted());
    EXPECT_FALSE(map->ProvidesRss());

    AddressRegion currentStackRegion;
    int currentStackMarker = 0;
    ASSERT_TRUE(map->TryGetRegion(reinterpret_cast<uintptr_t>(&currentStackMarker), currentStackRegion));
    EXPECT_EQ(currentStackRegion.Category, RegionCategory::Stack);

    MEMORY_BASIC_INFORMATION stackInformation{};
    ASSERT_EQ(
        ::VirtualQueryEx(
            ::GetCurrentProcess(),
            reinterpret_cast<LPCVOID>(&currentStackMarker),
            &stackInformation,
            sizeof(stackInformation)),
        sizeof(stackInformation));
    AddressRegion stackAllocationBaseRegion;
    ASSERT_TRUE(map->TryGetRegion(
        reinterpret_cast<uintptr_t>(stackInformation.AllocationBase), stackAllocationBaseRegion));
    EXPECT_EQ(stackAllocationBaseRegion.Category, RegionCategory::Stack);
    EXPECT_GT(stackAllocationBaseRegion.Committed, 0u);

    AddressRegion pageFileRegion;
    ASSERT_TRUE(map->TryGetRegion(reinterpret_cast<uintptr_t>(pageFileView), pageFileRegion));
    EXPECT_EQ(pageFileRegion.Category, RegionCategory::MappedFile);
    EXPECT_EQ(pageFileRegion.MappedType, MappedSectionType::PageFile);
    EXPECT_TRUE(pageFileRegion.ModuleName.empty());
#elif defined(LINUX)
    EXPECT_FALSE(map->ProvidesCommitted());
    EXPECT_TRUE(map->ProvidesRss());
#else
#error Unsupported platform
#endif

    bool sawImageWithName = false;
    uint64_t totalCommitted = 0;
    for (const auto& region : map->Regions())
    {
        totalCommitted += region.Committed;
        if (region.Category == RegionCategory::Image && !region.ModuleName.empty())
        {
            sawImageWithName = true;
        }
    }

    // The test host itself is a mapped image, so at least one named image run must be present.
    EXPECT_TRUE(sawImageWithName);
    EXPECT_GT(totalCommitted, 0u);
}
