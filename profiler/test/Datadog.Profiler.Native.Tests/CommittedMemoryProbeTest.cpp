// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CommittedMemoryProbe.h"
#include "EEHeapTestHelpers.h"

#include <vector>

namespace
{
constexpr uintptr_t Base = 0x10000000;
} // namespace

TEST(CommittedMemoryProbeTest, FullyMappedRegionReportsReservedAsCommitted)
{
    FakeMemoryReader reader(8);
    const uint64_t reserved = 64 * 1024;
    reader.AddRegion(Base, std::vector<uint8_t>(static_cast<size_t>(reserved), 0));

    // Every probed page falls inside the region, so committed == reserved regardless of page size.
    EXPECT_EQ(eeheap::ProbeCommittedBytes(reader, Base, reserved), reserved);
}

TEST(CommittedMemoryProbeTest, UnmappedRegionReportsZeroCommitted)
{
    FakeMemoryReader reader(8);
    // No region installed: the very first page probe fails -> 0 committed (and no crash).
    EXPECT_EQ(eeheap::ProbeCommittedBytes(reader, Base, 64 * 1024), 0u);
}

TEST(CommittedMemoryProbeTest, PartiallyMappedRegionReportsLessThanReserved)
{
    FakeMemoryReader reader(8);
    // Map only the first 4 KiB; reserve a much larger span. The probe stops at the first unmapped
    // page, so 0 < committed < reserved for any reasonable page size.
    reader.AddRegion(Base, std::vector<uint8_t>(4096, 0));
    const uint64_t reserved = 1ull << 30; // 1 GiB

    const uint64_t committed = eeheap::ProbeCommittedBytes(reader, Base, reserved);
    EXPECT_GT(committed, 0u);
    EXPECT_LT(committed, reserved);
}

TEST(CommittedMemoryProbeTest, ZeroSizedOrNullRegionReportsZero)
{
    FakeMemoryReader reader(8);
    reader.AddRegion(Base, std::vector<uint8_t>(4096, 0));

    EXPECT_EQ(eeheap::ProbeCommittedBytes(reader, Base, 0), 0u);
    EXPECT_EQ(eeheap::ProbeCommittedBytes(reader, 0, 4096), 0u);
}
