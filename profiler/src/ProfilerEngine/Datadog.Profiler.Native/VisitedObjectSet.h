// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <vector>
#include "cor.h"
#include "corprof.h"

// Open-addressing hash map for visited object addresses.
// Uses linear probing with a power-of-2 table and 0 as the empty sentinel
// (valid because no real object lives at address 0).
//
// Storage is split into two parallel arrays:
//  - _addresses: address-only (8 bytes/entry, 8 per 64-byte cache line)
//  - _entries:   metadata (ClassID only), accessed only after the probe resolves
// This layout keeps the hot probe loop compact in cache while metadata is
// touched only on insert or when reading cached info from a revisited object.
//
// Each entry caches the ClassID resolved during the first visit so that
// revisits (shared objects referenced by multiple parents) can record
// type-level edges without redundant GetClassFromObject calls.
// Object size is re-resolved via GetObjectSize2 on revisit to keep
// entries compact (8 bytes instead of 16).
//
// Clear() is O(entries) not O(capacity): a side vector tracks which bucket
// indices were written, so only those addresses are zeroed. Metadata is not
// cleared — TryInsert zero-initialises the metadata slot on each insertion,
// ensuring no stale data leaks across roots.
class VisitedObjectSet
{
public:
    struct VisitedEntry
    {
        ClassID classID = 0;
    };

private:
    std::vector<uintptr_t> _addresses;
    std::vector<VisitedEntry> _entries;
    std::vector<uint32_t> _dirtyIndices;
    size_t _count = 0;
    size_t _peakCount = 0;
    size_t _growCount = 0;
    size_t _mask = 0;

    static constexpr size_t DefaultCapacity = 512;

    // Max load factor ~70% — keeps the table compact (fewer cache/TLB misses
    // on the first probe) while linear probing's sequential follow-up probes
    // stay within the same cache line.
    bool NeedsGrow() const
    {
        return _count * 10 >= _addresses.size() * 7;
    }

    void Grow()
    {
        _growCount++;
        size_t newCapacity = _addresses.size() * 2;
        std::vector<uintptr_t> newAddresses(newCapacity, 0);
        std::vector<VisitedEntry> newEntries(newCapacity);
        size_t newMask = newCapacity - 1;

        _dirtyIndices.clear();
        _dirtyIndices.reserve(_count);

        for (size_t i = 0; i < _addresses.size(); i++)
        {
            if (_addresses[i] == 0)
            {
                continue;
            }

            size_t idx = HashAddress(_addresses[i]) & newMask;
            while (newAddresses[idx] != 0)
            {
                idx = (idx + 1) & newMask;
            }
            newAddresses[idx] = _addresses[i];
            newEntries[idx] = _entries[i];
            _dirtyIndices.push_back(static_cast<uint32_t>(idx));
        }

        _addresses = std::move(newAddresses);
        _entries = std::move(newEntries);
        _mask = newMask;
    }

    // Fibonacci hashing: multiply by the golden ratio constant to scatter
    // sequential heap addresses uniformly across the power-of-2 table.
    // Plain addr>>3 produces near-sequential indices that cause severe
    // primary clustering with linear probing.
    static size_t HashAddress(uintptr_t addr)
    {
        return (addr >> 3) * 0x9E3779B97F4A7C15ULL;
    }

public:
    explicit VisitedObjectSet(size_t initialCapacity = DefaultCapacity)
    {
        size_t cap = DefaultCapacity;
        while (cap < initialCapacity)
        {
            cap *= 2;
        }
        _addresses.resize(cap, 0);
        _entries.resize(cap);
        _mask = cap - 1;
    }

    bool IsVisited(uintptr_t address) const
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == address)
            {
                return true;
            }
            if (_addresses[idx] == 0)
            {
                return false;
            }
            idx = (idx + 1) & _mask;
        }
    }

    enum class InsertResult : uint8_t { Inserted, AlreadyPresent };

    // Single-probe insert: finds or creates the bucket for `address`.
    // On return, `outEntry` points to the metadata slot (valid until next mutation).
    // Callers write classID directly into the returned entry.
    InsertResult TryInsert(uintptr_t address, VisitedEntry*& outEntry)
    {
        if (NeedsGrow())
        {
            Grow();
        }

        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == address)
            {
                outEntry = &_entries[idx];
                return InsertResult::AlreadyPresent;
            }
            if (_addresses[idx] == 0)
            {
                _addresses[idx] = address;
                _entries[idx] = {};
                _dirtyIndices.push_back(static_cast<uint32_t>(idx));
                _count++;
                outEntry = &_entries[idx];
                return InsertResult::Inserted;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // Single-probe insert-and-store: inserts `address` if absent and writes
    // classID in the same probe. Used for root seeding where the
    // caller already knows the classID.
    void MarkVisitedAndStore(uintptr_t address, ClassID classID)
    {
        if (NeedsGrow())
        {
            Grow();
        }

        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == 0)
            {
                _addresses[idx] = address;
                _entries[idx].classID = classID;
                _dirtyIndices.push_back(static_cast<uint32_t>(idx));
                _count++;
                return;
            }
            if (_addresses[idx] == address)
            {
                _entries[idx].classID = classID;
                return;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // Legacy wrappers kept for tests / callers that don't need the slot pointer.
    void MarkVisited(uintptr_t address)
    {
        VisitedEntry* unused = nullptr;
        TryInsert(address, unused);
    }

    bool MarkIfAbsent(uintptr_t address)
    {
        VisitedEntry* unused = nullptr;
        return TryInsert(address, unused) == InsertResult::Inserted;
    }

    void StoreClassID(uintptr_t address, ClassID classID)
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == address)
            {
                _entries[idx].classID = classID;
                return;
            }
            if (_addresses[idx] == 0)
            {
                return;
            }
            idx = (idx + 1) & _mask;
        }
    }

    bool GetClassID(uintptr_t address, ClassID& outClassID) const
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == address)
            {
                outClassID = _entries[idx].classID;
                return true;
            }
            if (_addresses[idx] == 0)
            {
                return false;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // O(dirty-count) clear: only zero the address slots that were written.
    // Metadata is not cleared — TryInsert zero-initialises it on insertion.
    void Clear()
    {
        if (_count > _peakCount)
        {
            _peakCount = _count;
        }
        for (uint32_t idx : _dirtyIndices)
        {
            _addresses[idx] = 0;
        }
        _dirtyIndices.clear();
        _count = 0;
    }

    size_t Size() const
    {
        return _count;
    }

    size_t GetMemorySize() const
    {
        return sizeof(VisitedObjectSet)
             + _addresses.capacity() * sizeof(uintptr_t)
             + _entries.capacity() * sizeof(VisitedEntry)
             + _dirtyIndices.capacity() * sizeof(uint32_t);
    }

    size_t GetBucketCount() const
    {
        return _addresses.size();
    }

    size_t GetPeakEntryCount() const
    {
        return std::max(_peakCount, _count);
    }

    size_t GetGrowCount() const
    {
        return _growCount;
    }

    size_t GetAddressesMemorySize() const
    {
        return _addresses.capacity() * sizeof(uintptr_t);
    }

    size_t GetEntriesMemorySize() const
    {
        return _entries.capacity() * sizeof(VisitedEntry);
    }

    size_t GetDirtyIndicesMemorySize() const
    {
        return _dirtyIndices.capacity() * sizeof(uint32_t);
    }
};
