// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include <cstddef>
#include <cstdint>

// GCDesc reader: reads the CLR's GCDesc structure directly from the MethodTable
// to enumerate GC-traceable reference fields without metadata API calls.
//
// The GCDesc is an internal CLR data structure stored immediately BEFORE the
// MethodTable in memory. It describes which byte ranges within an object contain
// GC-traceable references. The GC reads this at every collection; the DAC, SOS,
// and ClrMD all rely on the same encoding.
//
// Since ClassID == MethodTable pointer in the profiling API, we can read the
// GCDesc directly from ClassID with a few pointer dereferences.
//
// Reference: CoreCLR gc/gc.cpp, vm/gcdesc.h, vm/methodtable.h
//            https://minidump.net/writing-a-net-gc-in-c-part-5/
namespace GCDesc
{

// MethodTable flags (offset 0, first DWORD of MethodTable).
// enum_flag_ContainsPointers = 0x01000000 in CoreCLR's vm/methodtable.h.
static constexpr uint32_t Flag_ContainsPointers = 0x01000000;

// A GCDesc series describes a contiguous range of reference fields within an
// object. Stored backwards from the MethodTable pointer.
//
// Memory layout (growing backwards from MethodTable):
//   mt[-1]          = series count (ptrdiff_t)
//   mt[-2], mt[-3]  = GCDescSeries[0] {encodedSize, offset}
//   mt[-4], mt[-5]  = GCDescSeries[1] {encodedSize, offset}
//   ...
struct GCDescSeries
{
    ptrdiff_t encodedSize;
    ptrdiff_t offset;
};

// For value type arrays (negative series count), each ValSerieItem packs two
// half-pointer values: nptrs (count of consecutive pointers) and skip (bytes
// to jump after reading those pointers).
//
// On 64-bit: lower 32 bits = nptrs, upper 32 bits = skip
// On 32-bit: lower 16 bits = nptrs, upper 16 bits = skip
struct ValSerieItem
{
    ptrdiff_t value;

    uint32_t GetNptrs() const
    {
        if constexpr (sizeof(void*) == 8)
        {
            return static_cast<uint32_t>(value & 0xFFFFFFFF);
        }
        else
        {
            return static_cast<uint32_t>(static_cast<uint16_t>(value & 0xFFFF));
        }
    }

    uint32_t GetSkip() const
    {
        if constexpr (sizeof(void*) == 8)
        {
            return static_cast<uint32_t>((static_cast<uint64_t>(value) >> 32) & 0xFFFFFFFF);
        }
        else
        {
            return static_cast<uint32_t>((static_cast<uint32_t>(value) >> 16) & 0xFFFF);
        }
    }
};

// Check whether a type contains GC-traceable pointers by reading the
// ContainsPointers flag from the first DWORD of the MethodTable.
// This MUST be checked before reading GCDesc -- types without GC pointers
// have no GCDesc structure, so reading mt[-1] would access unrelated memory.
inline bool ContainsGCPointers(ClassID classID)
{
    if (classID == 0)
    {
        return false;
    }

    auto* flags = reinterpret_cast<const uint32_t*>(classID);
    return (*flags & Flag_ContainsPointers) != 0;
}

// Read the GCDesc series count from the MethodTable.
// Positive: regular objects and reference arrays (GCDescSeries encoding).
// Negative: value type arrays (ValSerieItem encoding).
// Zero: should not occur if ContainsGCPointers returned true.
inline ptrdiff_t GetSeriesCount(ClassID classID)
{
    auto* mt = reinterpret_cast<const ptrdiff_t*>(classID);
    return mt[-1];
}

// ============================================================================
// Memory layout: MethodTable (MT) and GCDesc
// ============================================================================
//
// GCDesc is stored BEFORE the MT pointer, growing downward in memory.
// The MT pointer itself (== ClassID) sits at a fixed location; the GC reads
// backwards from it to find the reference field descriptors.
//
// ---- POSITIVE series count (regular objects & reference arrays) ----
//
//  Lower addresses
//  :                                :
//  |  GCDescSeries[N-1].encodedSize |  mt[-(2N+1)]    \
//  |  GCDescSeries[N-1].offset      |  mt[-(2N)]       |
//  :         ...                    :                  | N series
//  |  GCDescSeries[0].encodedSize   |  mt[-3]          |
//  |  GCDescSeries[0].offset        |  mt[-2]         /
//  |  seriesCount  (> 0)            |  mt[-1]   N = seriesCount
//  +--------------------------------+  <--- MT pointer (== ClassID)
//  |  flags (DWORD)                 |  mt[0]
//  |  ...rest of MethodTable...     |
//  Higher addresses
//
//  Each series: actual ref range = encodedSize + objectSize  bytes
//               starting at      objectAddress + offset
//
// ---- NEGATIVE series count (value-type arrays) ----
//
//  Lower addresses
//  :                                :
//  |  ValSerieItem[M-1].value       |   \
//  :         ...                    :    | M items
//  |  ValSerieItem[0].value         |   /
//  |  startOffset                   |  mt[-2]
//  |  seriesCount  (< 0)            |  mt[-1]   M = -seriesCount
//  +--------------------------------+  <--- MT pointer (== ClassID)
//  |  flags (DWORD)                 |  mt[0]
//  |  ...rest of MethodTable...     |
//  Higher addresses
//
//  startOffset: byte offset from the array ObjectID to the first element's
//               first reference field.
//  Each ValSerieItem packs {nptrs, skip}:
//    - read nptrs consecutive pointer slots
//    - advance skip bytes (non-ref gap to next group)
//  This pattern repeats for every element in the array.
//
// ============================================================================


// Enumerate all GC references in a non-array object (positive series count).
// objectAddress: the ObjectID (points to the MethodTable pointer at offset 0).
// objectSize: from GetObjectSize2 (needed for the size decoding).
// callback: invoked for each non-null reference found, with
// (slotAddress, refAddress, offsetFromObject).
template <typename TCallback>
inline void EnumerateObjectRefs(ClassID classID, uintptr_t objectAddress, SIZE_T objectSize, TCallback&& callback)
{
    auto* mt = reinterpret_cast<const ptrdiff_t*>(classID);
    ptrdiff_t seriesCount = mt[-1];

    if (seriesCount <= 0)
    {
        return;
    }

    auto* series = reinterpret_cast<const GCDescSeries*>(mt - 1);

    // Each series describes a contiguous range of reference fields within the object.
    for (size_t i = 1; i <= static_cast<size_t>(seriesCount); i++)
    {
        const auto& s = series[-static_cast<ptrdiff_t>(i)];
        ptrdiff_t rangeSize = s.encodedSize + static_cast<ptrdiff_t>(objectSize);
        if (rangeSize <= 0)
        {
            continue;
        }

        auto* rangeStart = reinterpret_cast<const uintptr_t*>(objectAddress + s.offset);
        size_t numRefs = static_cast<size_t>(rangeSize) / sizeof(void*);

        // Iterate over the reference fields in this series and invoke the callback for each non-null reference.
        for (size_t j = 0; j < numRefs; j++)
        {
            auto* slot = rangeStart + j;
            uintptr_t refValue = *slot;
            if (refValue != 0)
            {
                ULONG refOffset = static_cast<ULONG>(s.offset + j * sizeof(void*));
                callback(slot, refValue, refOffset);
            }
        }
    }
}

// Enumerate all GC references in a value type array (negative series count).
//   arrayClassID: the ClassID of the array itself (not the element type).
//   arrayAddress: the ObjectID of the array.
//   totalElements: number of elements in the array.
//   callback: invoked for each non-null reference found, with
//             (slotAddress, refAddress, offsetFromObject).
template <typename TCallback>
inline void EnumerateVTArrayRefs(ClassID arrayClassID, uintptr_t arrayAddress, uint64_t totalElements, TCallback&& callback)
{
    auto* mt = reinterpret_cast<const ptrdiff_t*>(arrayClassID);
    ptrdiff_t seriesCount = mt[-1];

    // sanity check but should never happen
    if (seriesCount >= 0)
    {
        return;
    }

    size_t numValSerieItems = static_cast<size_t>(-seriesCount);
    // The offset field sits between the series count and the ValSerieItems and
    // is relative to the array object's ObjectID, matching CoreCLR's scanner.
    ptrdiff_t startOffset = mt[-2];

    auto* valSeries = reinterpret_cast<const ValSerieItem*>(mt - 2) - 1;
    auto* cur = reinterpret_cast<const uintptr_t*>(arrayAddress + startOffset);

    for (uint64_t elem = 0; elem < totalElements; elem++)
    {
        for (size_t v = 0; v < numValSerieItems; v++)
        {
            const auto& item = valSeries[-static_cast<ptrdiff_t>(v)];

            // 16 bit in 32 bit and 32 bit in 64 bit but easier to use uint32_t
            uint32_t nptrs = item.GetNptrs();
            uint32_t skip = item.GetSkip();

            for (uint32_t p = 0; p < nptrs; p++)
            {
                auto* slot = cur;
                uintptr_t refValue = *cur;
                if (refValue != 0)
                {
                    ULONG refOffset = static_cast<ULONG>(
                        reinterpret_cast<uintptr_t>(slot) - arrayAddress);
                    callback(slot, refValue, refOffset);
                }
                cur++;
            }

            cur = reinterpret_cast<const uintptr_t*>(reinterpret_cast<uintptr_t>(cur) + skip);
        }
    }
}

} // namespace GCDesc
