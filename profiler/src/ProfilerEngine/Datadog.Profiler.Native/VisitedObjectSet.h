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
//  - _entries:   metadata (ClassID + size), accessed only after the probe resolves
// This layout keeps the hot probe loop compact in cache while metadata is
// touched only on insert or when reading cached info from a revisited object.
//
// Each entry caches the ClassID and object size resolved during the first visit,
// so that revisits (shared objects referenced by multiple parents) can record
// type-level edges without redundant ICorProfilerInfo calls.
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
        SIZE_T size = 0;
    };

private:
    std::vector<uintptr_t> _addresses;
    std::vector<VisitedEntry> _entries;
    std::vector<size_t> _dirtyIndices;
    size_t _count = 0;
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
            _dirtyIndices.push_back(idx);
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
    // Callers write classID/size directly into the returned entry.
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
                _dirtyIndices.push_back(idx);
                _count++;
                outEntry = &_entries[idx];
                return InsertResult::Inserted;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // Single-probe insert-and-store: inserts `address` if absent and writes
    // classID + size in the same probe. Used for root seeding where the
    // caller already knows both values.
    void MarkVisitedAndStore(uintptr_t address, ClassID classID, SIZE_T size)
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
                _entries[idx].size = size;
                _dirtyIndices.push_back(idx);
                _count++;
                return;
            }
            if (_addresses[idx] == address)
            {
                _entries[idx].classID = classID;
                _entries[idx].size = size;
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

    void StoreInfo(uintptr_t address, ClassID classID, SIZE_T size)
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == address)
            {
                _entries[idx].classID = classID;
                _entries[idx].size = size;
                return;
            }
            if (_addresses[idx] == 0)
            {
                return;
            }
            idx = (idx + 1) & _mask;
        }
    }

    bool GetInfo(uintptr_t address, ClassID& outClassID, SIZE_T& outSize) const
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            if (_addresses[idx] == address)
            {
                outClassID = _entries[idx].classID;
                outSize = _entries[idx].size;
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
        for (size_t idx : _dirtyIndices)
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
             + _dirtyIndices.capacity() * sizeof(size_t);
    }

    size_t GetBucketCount() const
    {
        return _addresses.size();
    }
};
