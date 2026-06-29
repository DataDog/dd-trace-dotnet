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
