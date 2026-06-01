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

// Upper bound on the number of ValSerieItems describing a single value-type
// array element. The CLR caps reference groups per value type well below this;
// it is used purely as a defensive sanity bound when reading negative GCDesc
// series counts (value-type arrays).
static constexpr size_t MaxValSerieItems = 0xFFFF;

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

// Sanity-bound a GCDesc series count before trusting it. A legitimate count is
// small: the number of distinct reference ranges (positive) or value-type array
// ValSerieItems (negative) can never exceed the number of pointer-sized slots
// that fit in the object. This turns a garbage read (e.g. if the MethodTable
// layout ever changed) into a rejected value instead of an out-of-bounds walk.
inline bool LooksLikeValidSeriesCount(ptrdiff_t count, SIZE_T objectSize)
{
    if (count == 0)
    {
        return false;
    }

    // Upper bound on plausible series: one per pointer-sized slot in the object,
    // with a small floor so tiny objects (e.g. a single reference field) pass.
    ptrdiff_t maxSeries = static_cast<ptrdiff_t>(objectSize / sizeof(void*)) + 1;

    ptrdiff_t magnitude = (count < 0) ? -count : count;
    return magnitude <= maxSeries;
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

    // Defense in depth: reject an implausible series count instead of walking
    // arbitrary memory if the GCDesc/MethodTable layout is ever unexpected.
    if (!LooksLikeValidSeriesCount(seriesCount, objectSize))
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

        // The reference range must stay within the object bounds. A series whose
        // offset is negative or whose end exceeds the object size indicates a
        // malformed/unexpected GCDesc -- skip it rather than read out of bounds.
        if (s.offset < 0 ||
            static_cast<SIZE_T>(s.offset) > objectSize ||
            static_cast<SIZE_T>(s.offset + rangeSize) > objectSize)
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

    // Defense in depth: the ValSerieItem count describes a single value-type
    // element's reference groups, so it is inherently small. Reject an absurd
    // count rather than walking arbitrary memory if the layout is unexpected.
    if (numValSerieItems > MaxValSerieItems)
    {
        return;
    }

    // The offset field sits between the series count and the ValSerieItems and
    // is relative to the array object's ObjectID, matching CoreCLR's scanner.
    ptrdiff_t startOffset = mt[-2];

    // A negative start offset would point before the array object -- malformed.
    if (startOffset < 0)
    {
        return;
    }

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

// Result of a runtime self-test of the GCDesc reader against an independent
// metadata signal. Pending means "inconclusive on this object, try another";
// only Failed indicates a clear contradiction that should disable the reader.
enum class SelfTestResult
{
    Pending,
    Passed,
    Failed
};

// Validate that the GCDesc/MethodTable layout this reader depends on is
// consistent with what the profiling API metadata reports for a given type.
//
// This is a cheap, defensive cross-check (run only on the first few scannable
// objects of a dump -- never per object) to detect the unlikely case where a
// future runtime changes the MethodTable flag bit or GCDesc layout, instead of
// blindly dereferencing garbage.
//
// Strategy: only assert on clear contradictions. We deliberately return Pending
// for ambiguous shapes so the caller can try another object before disabling.
inline SelfTestResult ValidateAgainstMetadata(ICorProfilerInfo12* pInfo, ClassID classID, SIZE_T objectSize)
{
    if (pInfo == nullptr || classID == 0)
    {
        return SelfTestResult::Pending;
    }

    // Arrays have no per-field metadata to compare against GetClassLayout, so we
    // cannot draw a conclusion from them here.
    CorElementType elementType;
    ClassID elementClassID;
    ULONG rank = 0;
    if (pInfo->IsArrayClass(classID, &elementType, &elementClassID, &rank) == S_OK)
    {
        return SelfTestResult::Pending;
    }

    bool flagSaysContainsPointers = ContainsGCPointers(classID);

    // Ask the metadata how many instance fields this type has. We use this only
    // as an independent signal, not to reconstruct the exact GCDesc.
    ULONG fieldCount = 0;
    ULONG classSize = 0;
    HRESULT hr = pInfo->GetClassLayout(classID, nullptr, 0, &fieldCount, &classSize);
    if (FAILED(hr))
    {
        // Can't get an independent signal -- inconclusive.
        return SelfTestResult::Pending;
    }

    if (flagSaysContainsPointers)
    {
        // If the flag claims this type contains GC pointers, then the GCDesc must
        // be present and well-formed: a non-zero, plausibly-sized series count.
        ptrdiff_t seriesCount = GetSeriesCount(classID);
        if (!LooksLikeValidSeriesCount(seriesCount, objectSize))
        {
            // The flag and the series count disagree in a way that indicates the
            // layout we assume is wrong.
            return SelfTestResult::Failed;
        }

        // A type that contains GC pointers must have at least one field. If the
        // metadata reports zero fields yet the flag says "has pointers", the two
        // sources contradict each other.
        if (fieldCount == 0)
        {
            return SelfTestResult::Failed;
        }

        return SelfTestResult::Passed;
    }

    // Flag says "no GC pointers". We would never have called the enumerators for
    // such a type (the traverser checks ContainsGCPointers first), so there is
    // nothing to contradict here -- treat as inconclusive and move on.
    return SelfTestResult::Pending;
}

} // namespace GCDesc
