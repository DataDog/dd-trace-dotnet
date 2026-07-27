// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>

// High-level classification of an OS address-space region, mirroring the categories Sysinternals
// VMMap (Windows) and /proc/self/smaps (Linux) expose. Shared by both platform enumerators so the
// reconciliation/breakdown logic stays platform-independent.
enum class RegionCategory
{
    Image,       // a mapped PE/ELF module (dll/so/exe)
    MappedFile,  // a non-image file mapping (data file / pagefile-backed section)
    PrivateData, // anonymous private committed memory (native heap, etc.)
    Stack,       // a thread stack
    Heap,        // an OS/CRT heap region
    Reserved,    // reserved-but-not-committed address space
    Free,        // free address space
    Other,
};

// A single fine-grained OS memory run: one VirtualQuery run on Windows, one VMA on Linux. Kept
// fine-grained (never coalesced by module here) so committed/rss can be summed accurately over an
// arbitrary [base, base + size) window. Display coalescing (by module leaf name) is a derived step
// performed by MemoryBreakdownProvider.
struct AddressRegion
{
    uintptr_t Address = 0;
    uint64_t Size = 0;      // reserved / virtual span of this run
    uint64_t Committed = 0; // Windows: Size when MEM_COMMIT else 0; Linux: accessible span (perms != ---p) else 0
    uint64_t Rss = 0;       // Linux: smaps Rss; Windows: working-set bytes (only when captured), else 0
    RegionCategory Category = RegionCategory::Other;
    std::string ModuleName; // dll/so leaf name for Image; full path for MappedFile; empty otherwise
    std::string Protection; // "r-x", "rw-", ... (diagnostics only; never surfaced as a sample label)

    uint64_t End() const
    {
        return static_cast<uint64_t>(Address) + Size;
    }
};

inline const char* ToString(RegionCategory category)
{
    switch (category)
    {
        case RegionCategory::Image: return "image";
        case RegionCategory::MappedFile: return "mapped-file";
        case RegionCategory::PrivateData: return "private";
        case RegionCategory::Stack: return "stack";
        case RegionCategory::Heap: return "heap";
        case RegionCategory::Reserved: return "reserved";
        case RegionCategory::Free: return "free";
        default: return "other";
    }
}
