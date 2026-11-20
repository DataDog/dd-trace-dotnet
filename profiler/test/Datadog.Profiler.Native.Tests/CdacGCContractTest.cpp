// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CdacGCContract.h"
#include "CdacTarget.h"
#include "ClrNativeHeapInfo.h"
#include "EEHeapTestHelpers.h"
#include "LogicalDescriptor.h"

#include <string>
#include <vector>

#ifdef _WINDOWS
#include <windows.h>
#else
#include <sys/mman.h>
#include <unistd.h>
#endif

using namespace cdac;

namespace
{
// Generation table base + a single HeapSegment instance for a workstation GC.
constexpr uintptr_t GenTableBase = 0x700000; // 7340032
constexpr uintptr_t Seg0 = 0x710000;
constexpr uintptr_t SegMem = 0x800000;
constexpr uintptr_t SegCommitted = 0x804000; // committed - mem = 0x4000
constexpr uintptr_t SegReserved = 0x808000;  // reserved  - mem = 0x8000

Target BuildWorkstationGcTarget(FakeMemoryReader& reader)
{
    // Workstation GC descriptor: Generation (size 16, StartSegment@0), HeapSegment (Mem@0,
    // Committed@16, Reserved@24, Next@32), TotalGenerationCount=3, and the WKS generation-table
    // global pointing at GenTableBase.
    const std::string json = R"({
        "types": {
            "Generation": { "StartSegment": 0, "!": 16 },
            "HeapSegment": { "Mem": 0, "Allocated": 8, "Committed": 16, "Reserved": 24, "Next": 32, "!": 40 }
        },
        "globals": {
            "GCIdentifiers": "workstation",
            "TotalGenerationCount": 3,
            "GCHeapGenerationTable": 7340032
        },
        "contracts": { "GC": 1 }
    })";

    uintptr_t root = InstallDescriptor(reader, json, /*pointerData*/ {});

    LogicalDescriptor descriptor;
    descriptor.Build(reader, root);
    return Target(reader, std::move(descriptor));
}

// Same as BuildWorkstationGcTarget but the HeapSegment type carries a Flags field, so a read-only
// (frozen / non-GC) segment can be distinguished.
Target BuildWorkstationGcTargetWithFlags(FakeMemoryReader& reader)
{
    const std::string json = R"({
        "types": {
            "Generation": { "StartSegment": 0, "!": 16 },
            "HeapSegment": { "Mem": 0, "Allocated": 8, "Committed": 16, "Reserved": 24, "Next": 32, "Flags": 40, "!": 48 }
        },
        "globals": {
            "GCIdentifiers": "workstation",
            "TotalGenerationCount": 3,
            "GCHeapGenerationTable": 7340032
        },
        "contracts": { "GC": 1 }
    })";

    uintptr_t root = InstallDescriptor(reader, json, /*pointerData*/ {});

    LogicalDescriptor descriptor;
    descriptor.Build(reader, root);
    return Target(reader, std::move(descriptor));
}

// --- Real OS reserve/commit helpers, so the bookkeeping committed size goes through QueryCommittedBytes
// against an actual region map (the card-table base must be a real reservation). ---
size_t OsPageSize()
{
#ifdef _WINDOWS
    SYSTEM_INFO info{};
    GetSystemInfo(&info);
    return info.dwPageSize != 0 ? static_cast<size_t>(info.dwPageSize) : 4096;
#else
    long pageSize = sysconf(_SC_PAGESIZE);
    return pageSize > 0 ? static_cast<size_t>(pageSize) : 4096;
#endif
}

uintptr_t OsReserve(size_t size)
{
#ifdef _WINDOWS
    return reinterpret_cast<uintptr_t>(VirtualAlloc(nullptr, size, MEM_RESERVE, PAGE_READWRITE));
#else
    void* p = mmap(nullptr, size, PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    return p == MAP_FAILED ? 0 : reinterpret_cast<uintptr_t>(p);
#endif
}

bool OsCommit(uintptr_t base, size_t offset, size_t length)
{
#ifdef _WINDOWS
    return VirtualAlloc(reinterpret_cast<LPVOID>(base + offset), length, MEM_COMMIT, PAGE_READWRITE) != nullptr;
#else
    return mprotect(reinterpret_cast<void*>(base + offset), length, PROT_READ | PROT_WRITE) == 0;
#endif
}

void OsRelease(uintptr_t base, size_t size)
{
#ifdef _WINDOWS
    (void)size;
    VirtualFree(reinterpret_cast<LPVOID>(base), 0, MEM_RELEASE);
#else
    munmap(reinterpret_cast<void*>(base), size);
#endif
}

// Builds a target whose CardTableInfo (Recount@0, Size@8, NextCardTable@16; size 24) is served from
// the FakeMemoryReader at `cardTableInfoAddr`, with the BookkeepingStart global pointing at it.
constexpr uintptr_t BookkeepingStartGlobalAddr = 0x900000;

Target BuildBookkeepingTarget(FakeMemoryReader& reader, uintptr_t cardTableInfoAddr, uint64_t reservedSize)
{
    const std::string json = R"({
        "types": {
            "CardTableInfo": { "Recount": 0, "Size": 8, "NextCardTable": 16, "!": 24 }
        },
        "globals": {
            "GCIdentifiers": "workstation",
            "BookkeepingStart": 9437184,
            "CardTableInfoSize": 24
        },
        "contracts": { "GC": 1 }
    })"; // BookkeepingStart == 0x900000

    // BookkeepingStart global holds the pointer to the card_table_info.
    reader.AddPointerAt(BookkeepingStartGlobalAddr, cardTableInfoAddr);

    // card_table_info: Recount=1, Size=reservedSize, NextCardTable=0.
    std::vector<uint8_t> ct(24, 0);
    ct[0] = 1; // Recount (uint32) = 1
    for (int i = 0; i < 8; i++)
    {
        ct[static_cast<size_t>(8 + i)] = static_cast<uint8_t>((reservedSize >> (8 * i)) & 0xFF);
    }
    reader.AddRegion(cardTableInfoAddr, std::move(ct));

    uintptr_t root = InstallDescriptor(reader, json, /*pointerData*/ {});
    LogicalDescriptor descriptor;
    descriptor.Build(reader, root);
    return Target(reader, std::move(descriptor));
}
} // namespace

TEST(CdacGCContractTest, GetGCHeapSegmentsWalksGenerationStartSegments)
{
    FakeMemoryReader reader(8);

    // Generation table: gen0.StartSegment -> Seg0; gen1/gen2 have no segment list.
    reader.AddPointerAt(GenTableBase + 0, Seg0);
    reader.AddPointerAt(GenTableBase + 16, 0);
    reader.AddPointerAt(GenTableBase + 32, 0);

    // Seg0 fields.
    reader.AddPointerAt(Seg0 + 0, SegMem);        // Mem
    reader.AddPointerAt(Seg0 + 16, SegCommitted); // Committed
    reader.AddPointerAt(Seg0 + 24, SegReserved);  // Reserved
    reader.AddPointerAt(Seg0 + 32, 0);            // Next

    Target target = BuildWorkstationGcTarget(reader);

    std::vector<ClrNativeHeapInfo> results;
    GCContract gc(target);
    gc.GetGCHeapSegments([&](const ClrNativeHeapInfo& info) { results.push_back(info); });

    ASSERT_EQ(results.size(), 1u);
    const ClrNativeHeapInfo& seg = results[0];
    EXPECT_EQ(seg.Kind, NativeHeapKind::GCHeapSegment);
    EXPECT_EQ(seg.Address, SegMem);
    EXPECT_EQ(seg.Size, 0x8000u);     // reserved - mem
    EXPECT_EQ(seg.Committed, 0x4000u); // committed - mem
    EXPECT_EQ(seg.GCHeap, 0);
    EXPECT_EQ(seg.Generation, 0);
}

TEST(CdacGCContractTest, GetGCHeapSegmentsDedupsRegionsSharedAcrossGenerations)
{
    FakeMemoryReader reader(8);

    // Both gen0 and gen1 point at the SAME segment; it must be attributed to gen0 only.
    reader.AddPointerAt(GenTableBase + 0, Seg0);
    reader.AddPointerAt(GenTableBase + 16, Seg0);
    reader.AddPointerAt(GenTableBase + 32, 0);

    reader.AddPointerAt(Seg0 + 0, SegMem);
    reader.AddPointerAt(Seg0 + 16, SegCommitted);
    reader.AddPointerAt(Seg0 + 24, SegReserved);
    reader.AddPointerAt(Seg0 + 32, 0);

    Target target = BuildWorkstationGcTarget(reader);

    std::vector<ClrNativeHeapInfo> results;
    GCContract gc(target);
    gc.GetGCHeapSegments([&](const ClrNativeHeapInfo& info) { results.push_back(info); });

    ASSERT_EQ(results.size(), 1u);
    EXPECT_EQ(results[0].Generation, 0);
}

TEST(CdacGCContractTest, GetGCHeapSegmentsClassifiesReadOnlySegmentAsNonGCHeap)
{
    FakeMemoryReader reader(8);

    // gen0.StartSegment -> Seg0; gen1/gen2 empty.
    reader.AddPointerAt(GenTableBase + 0, Seg0);
    reader.AddPointerAt(GenTableBase + 16, 0);
    reader.AddPointerAt(GenTableBase + 32, 0);

    reader.AddPointerAt(Seg0 + 0, SegMem);
    reader.AddPointerAt(Seg0 + 16, SegCommitted);
    reader.AddPointerAt(Seg0 + 24, SegReserved);
    reader.AddPointerAt(Seg0 + 32, 0);
    reader.AddPointerAt(Seg0 + 40, 1); // Flags: HEAP_SEGMENT_FLAGS_READONLY

    Target target = BuildWorkstationGcTargetWithFlags(reader);

    std::vector<ClrNativeHeapInfo> results;
    GCContract gc(target);
    gc.GetGCHeapSegments([&](const ClrNativeHeapInfo& info) { results.push_back(info); });

    ASSERT_EQ(results.size(), 1u);
    const ClrNativeHeapInfo& seg = results[0];
    EXPECT_EQ(seg.Kind, NativeHeapKind::NonGCHeap); // read-only -> frozen / non-GC heap
    EXPECT_EQ(seg.Address, SegMem);
    EXPECT_EQ(seg.Generation, -1); // not a real generation
}

TEST(CdacGCContractTest, GetGCBookkeepingMemoryRegionsReportsScatteredCommittedSize)
{
    const size_t page = OsPageSize();
    const uint64_t reserved = static_cast<uint64_t>(64 * page);

    // Real reservation acting as the card-table block, with two committed pages and a reserved gap.
    // Size must report the full reservation; Committed must be the gap-aware OS total (2 pages), not
    // the reserved size and not just the first committed run.
    uintptr_t cardTable = OsReserve(static_cast<size_t>(reserved));
    ASSERT_NE(cardTable, 0u);
    ASSERT_TRUE(OsCommit(cardTable, 0, page));
    ASSERT_TRUE(OsCommit(cardTable, 32 * page, page));

    FakeMemoryReader reader(8);
    Target target = BuildBookkeepingTarget(reader, cardTable, reserved);

    std::vector<ClrNativeHeapInfo> results;
    GCContract gc(target);
    gc.GetGCBookkeepingMemoryRegions([&](const ClrNativeHeapInfo& info) { results.push_back(info); });

    ASSERT_EQ(results.size(), 1u);
    const ClrNativeHeapInfo& bk = results[0];
    EXPECT_EQ(bk.Kind, NativeHeapKind::GCBookkeeping);
    EXPECT_EQ(bk.State, NativeHeapState::RegionOfRegions);
    EXPECT_EQ(bk.Address, cardTable);
    EXPECT_EQ(bk.Size, reserved);                              // reserved span
    EXPECT_EQ(bk.Committed, static_cast<uint64_t>(2 * page));  // scattered commits summed

    OsRelease(cardTable, static_cast<size_t>(reserved));
}

TEST(CdacGCContractTest, GetGCBookkeepingMemoryRegionsFallsBackToReservedWhenNotCommitted)
{
    const size_t page = OsPageSize();
    const uint64_t reserved = static_cast<uint64_t>(16 * page);

    // A reservation with nothing committed: QueryCommittedBytes returns 0, so the reporter falls back
    // to the reserved size rather than claiming 0 committed.
    uintptr_t cardTable = OsReserve(static_cast<size_t>(reserved));
    ASSERT_NE(cardTable, 0u);

    FakeMemoryReader reader(8);
    Target target = BuildBookkeepingTarget(reader, cardTable, reserved);

    std::vector<ClrNativeHeapInfo> results;
    GCContract gc(target);
    gc.GetGCBookkeepingMemoryRegions([&](const ClrNativeHeapInfo& info) { results.push_back(info); });

    ASSERT_EQ(results.size(), 1u);
    EXPECT_EQ(results[0].Size, reserved);
    EXPECT_EQ(results[0].Committed, reserved); // fallback

    OsRelease(cardTable, static_cast<size_t>(reserved));
}
