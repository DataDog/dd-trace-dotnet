// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <cstring>
#include <vector>

// Open-addressing hash set for visited object addresses.
// Uses linear probing with a power-of-2 table and 0 as the empty sentinel
// (valid because no real object lives at address 0).
//
// Designed to be reused across roots via Clear() to avoid re-allocating
// the bucket array for each root traversal.
class VisitedObjectSet
{
private:
    std::vector<uintptr_t> _buckets;
    size_t _count = 0;
    size_t _mask = 0;

    static constexpr size_t DefaultCapacity = 512;
    static constexpr uintptr_t EmptySentinel = 0;

    // Max load factor ~70% — keeps probe chains short while still dense.
    bool NeedsGrow() const
    {
        // _count * 10 > capacity * 7  <==>  load > 0.7
        return _count * 10 > _buckets.size() * 7;
    }

    void Grow()
    {
        size_t newCapacity = _buckets.size() * 2;
        std::vector<uintptr_t> newBuckets(newCapacity, EmptySentinel);
        size_t newMask = newCapacity - 1;

        for (uintptr_t addr : _buckets)
        {
            if (addr == EmptySentinel)
            {
                continue;
            }

            size_t idx = HashAddress(addr) & newMask;
            while (newBuckets[idx] != EmptySentinel)
            {
                idx = (idx + 1) & newMask;
            }
            newBuckets[idx] = addr;
        }

        _buckets = std::move(newBuckets);
        _mask = newMask;
    }

    // Object addresses are pointer-aligned (8-byte on x64), so the low 3 bits
    // are always zero. Shift right to use the varying bits directly.
    static size_t HashAddress(uintptr_t addr)
    {
        return addr >> 3;
    }

public:
    explicit VisitedObjectSet(size_t initialCapacity = DefaultCapacity)
    {
        // Round up to power of 2
        size_t cap = DefaultCapacity;
        while (cap < initialCapacity)
        {
            cap *= 2;
        }
        _buckets.resize(cap, EmptySentinel);
        _mask = cap - 1;
    }

    bool IsVisited(uintptr_t address) const
    {
        size_t idx = HashAddress(address) & _mask;
        while (true)
        {
            uintptr_t bucket = _buckets[idx];
            if (bucket == address)
            {
                return true;
            }
            if (bucket == EmptySentinel)
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
            uintptr_t bucket = _buckets[idx];
            if (bucket == address)
            {
                return; // already present
            }
            if (bucket == EmptySentinel)
            {
                _buckets[idx] = address;
                _count++;
                return;
            }
            idx = (idx + 1) & _mask;
        }
    }

    void Clear()
    {
        std::memset(_buckets.data(), 0, _buckets.size() * sizeof(uintptr_t));
        _count = 0;
    }

    size_t Size() const
    {
        return _count;
    }
};
