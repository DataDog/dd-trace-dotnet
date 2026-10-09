// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CommittedMemoryProbe.h"
#include "EEHeapTestHelpers.h"

#include <vector>

#ifdef _WINDOWS
#include <windows.h>
#else
#include <sys/mman.h>
#include <unistd.h>
#endif

namespace
{
constexpr uintptr_t Base = 0x10000000;

// Real OS reserve/commit helpers, so QueryCommittedBytes is exercised against an actual region map.
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

// Reserves `size` bytes (no commit). Returns 0 on failure.
uintptr_t OsReserve(size_t size)
{
#ifdef _WINDOWS
    return reinterpret_cast<uintptr_t>(VirtualAlloc(nullptr, size, MEM_RESERVE, PAGE_READWRITE));
#else
    void* p = mmap(nullptr, size, PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    return p == MAP_FAILED ? 0 : reinterpret_cast<uintptr_t>(p);
#endif
}

// Commits [base+offset, base+offset+length) inside a previously reserved block.
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

TEST(QueryCommittedBytesTest, NullOrZeroRegionReportsZero)
{
    EXPECT_EQ(eeheap::QueryCommittedBytes(0, 64 * 1024), 0u);
    EXPECT_EQ(eeheap::QueryCommittedBytes(Base, 0), 0u);
}

TEST(QueryCommittedBytesTest, FullyCommittedRegionReportsReserved)
{
    const size_t page = OsPageSize();
    const size_t size = 64 * page;

    uintptr_t base = OsReserve(size);
    ASSERT_NE(base, 0u);
    ASSERT_TRUE(OsCommit(base, 0, size));

    EXPECT_EQ(eeheap::QueryCommittedBytes(base, size), static_cast<uint64_t>(size));

    OsRelease(base, size);
}

TEST(QueryCommittedBytesTest, ScatteredCommitsAreAllCounted)
{
    const size_t page = OsPageSize();
    const size_t size = 64 * page;

    uintptr_t base = OsReserve(size);
    ASSERT_NE(base, 0u);

    // Commit a page at the start and another deep inside, leaving a large reserved gap between them.
    // A prefix-style probe would stop at the gap; QueryCommittedBytes must sum BOTH committed pages.
    ASSERT_TRUE(OsCommit(base, 0, page));
    ASSERT_TRUE(OsCommit(base, 32 * page, page));

    EXPECT_EQ(eeheap::QueryCommittedBytes(base, size), static_cast<uint64_t>(2 * page));

    OsRelease(base, size);
}

TEST(QueryCommittedBytesTest, FullyReservedRegionReportsZeroCommitted)
{
    const size_t page = OsPageSize();
    const size_t size = 16 * page;

    uintptr_t base = OsReserve(size);
    ASSERT_NE(base, 0u);

    // Nothing committed -> 0, even though the whole span is reserved.
    EXPECT_EQ(eeheap::QueryCommittedBytes(base, size), 0u);

    OsRelease(base, size);
}
