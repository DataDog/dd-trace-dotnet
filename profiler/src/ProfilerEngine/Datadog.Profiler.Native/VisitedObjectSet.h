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
// Each entry caches the ClassID and object size resolved during the first visit,
// so that revisits (shared objects referenced by multiple parents) can record
// type-level edges without redundant ICorProfilerInfo calls.
//
// Clear() is O(entries) not O(capacity): a side vector tracks which bucket
// indices were written, so only those are zeroed. This avoids expensive
// memset of the full table between roots while still retaining capacity
// to prevent re-growing.
class VisitedObjectSet
{
public:
    struct VisitedEntry
    {
        uintptr_t address = 0;
        ClassID classID = 0;
        SIZE_T size = 0;
    };

private:
    std::vector<VisitedEntry> _buckets;
    std::vector<size_t> _dirtyIndices;
    size_t _count = 0;
    size_t _mask = 0;

    static constexpr size_t DefaultCapacity = 512;

    // Max load factor ~70% — keeps the table compact (fewer cache/TLB misses
    // on the first probe) while linear probing's sequential follow-up probes
    // stay within the same cache line.
    bool NeedsGrow() const
    {
        return _count * 10 >= _buckets.size() * 7;
    }

    void Grow()
    {
        size_t newCapacity = _buckets.size() * 2;
        std::vector<VisitedEntry> newBuckets(newCapacity);
        size_t newMask = newCapacity - 1;

        _dirtyIndices.clear();
        _dirtyIndices.reserve(_count);

        for (auto& entry : _buckets)
        {
            if (entry.address == 0)
            {
                continue;
            }

            size_t idx = HashAddress(entry.address) & newMask;
            while (newBuckets[idx].address != 0)
            {
                idx = (idx + 1) & newMask;
            }
            newBuckets[idx] = entry;
            _dirtyIndices.push_back(idx);
        }

        _buckets = std::move(newBuckets);
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
        _buckets.resize(cap);
        _mask = cap - 1;
    }

    bool IsVisited(uintptr_t address) const
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            auto& entry = _buckets[idx];
            if (entry.address == address)
            {
                return true;
            }
            if (entry.address == 0)
            {
                return false;
            }
            idx = (idx + 1) & _mask;
        }
    }

    void MarkVisited(uintptr_t address)
    {
        if (NeedsGrow())
        {
            Grow();
        }

        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            auto& entry = _buckets[idx];
            if (entry.address == address)
            {
                return;
            }
            if (entry.address == 0)
            {
                entry.address = address;
                _dirtyIndices.push_back(idx);
                _count++;
                return;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // Combined check-and-insert in a single probe.
    // Returns true if the address was newly inserted, false if already present.
    bool MarkIfAbsent(uintptr_t address)
    {
        if (NeedsGrow())
        {
            Grow();
        }

        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            auto& entry = _buckets[idx];
            if (entry.address == address)
            {
                return false;
            }
            if (entry.address == 0)
            {
                entry.address = address;
                _dirtyIndices.push_back(idx);
                _count++;
                return true;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // Store ClassID and size for an already-inserted address.
    void StoreInfo(uintptr_t address, ClassID classID, SIZE_T size)
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            auto& entry = _buckets[idx];
            if (entry.address == address)
            {
                entry.classID = classID;
                entry.size = size;
                return;
            }
            if (entry.address == 0)
            {
                return;
            }
            idx = (idx + 1) & _mask;
        }
    }

    // Retrieve cached ClassID and size for a visited address.
    bool GetInfo(uintptr_t address, ClassID& outClassID, SIZE_T& outSize) const
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            auto& entry = _buckets[idx];
            if (entry.address == address)
            {
                outClassID = entry.classID;
                outSize = entry.size;
                return true;
            }
            if (entry.address == 0)
            {
                return false;
            }
            idx = (idx + 1) & _mask;
        }
    }

    void Clear()
    {
        for (size_t idx : _dirtyIndices)
        {
            _buckets[idx] = VisitedEntry{};
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
             + _buckets.capacity() * sizeof(VisitedEntry)
             + _dirtyIndices.capacity() * sizeof(size_t);
    }

    size_t GetBucketCount() const
    {
        return _buckets.size();
    }
};
