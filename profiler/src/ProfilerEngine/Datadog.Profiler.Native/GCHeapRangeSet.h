// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <vector>

// Coarse "is this address plausibly inside the GC heap" filter.
//
// This is deliberately NOT an exact membership test. GetGenerationBounds cannot be
// called during a heap dump (a GC is in progress), so the ranges seeded from it are
// captured just before the dump GC and may be slightly stale; they are widened
// outward to BlockSize granularity and grown from addresses the runtime itself hands
// us during the dump (validated root addresses).
//
// An empty set accepts everything, so every failure path (bounds unavailable, alloc
// failure, unexpected layout) degrades to the previous behaviour. A false accept
// costs at most one recovered memory access fault; a false reject silently drops a
// real edge, so the filter is intentionally biased toward accepting.
//
// Not thread-safe: all mutation and all queries happen on the single GC-callback
// thread during a dump.
class GCHeapRangeSet
{
public:
    // Ranges are rounded outward to this granularity before being merged, so a
    // region the GC committed during the dump GC is likely already covered by a
    // neighbouring block. Must be a power of two.
    static constexpr uintptr_t BlockSize = 64 * 1024 * 1024;

    void Clear()
    {
        _ranges.clear();
        _dirty = false;
    }

    // Add [start, start + length), rounded outward to BlockSize. Overflow-safe.
    void AddRange(uintptr_t start, size_t length)
    {
        if (length == 0)
        {
            return;
        }

        uintptr_t end = start + static_cast<uintptr_t>(length);
        if (end < start)
        {
            // Length wrapped the address space: clamp to the top.
            end = UINTPTR_MAX;
        }

        uintptr_t roundedStart = start & ~(BlockSize - 1);

        uintptr_t roundedEnd;
        if (end > UINTPTR_MAX - (BlockSize - 1))
        {
            roundedEnd = UINTPTR_MAX;
        }
        else
        {
            roundedEnd = (end + (BlockSize - 1)) & ~(BlockSize - 1);
        }

        _ranges.push_back({roundedStart, roundedEnd});
        _dirty = true;
    }

    void AddAddress(uintptr_t address)
    {
        // Skip the append (and the re-sort it forces) when the address is already
        // covered, which after the first few roots is almost always the case.
        if (Contains(address))
        {
            return;
        }
        AddRange(address, 1);
    }

    // Sort and merge overlapping/adjacent ranges. Safe to call repeatedly; a no-op
    // when nothing changed since the last finalize.
    void Finalize() const
    {
        if (!_dirty)
        {
            return;
        }

        std::sort(_ranges.begin(), _ranges.end(),
                  [](const Range& a, const Range& b) { return a.start < b.start; });

        size_t write = 0;
        for (size_t read = 0; read < _ranges.size(); read++)
        {
            if (write == 0 || _ranges[read].start > _ranges[write - 1].end)
            {
                _ranges[write++] = _ranges[read];
            }
            else if (_ranges[read].end > _ranges[write - 1].end)
            {
                // Overlapping or adjacent (start <= previous end): extend.
                _ranges[write - 1].end = _ranges[read].end;
            }
        }
        _ranges.resize(write);
        _dirty = false;
    }

    // True when the set is empty (accept-everything) or the address falls inside a
    // known block. Lazily finalizes so callers never have to remember to.
    bool Contains(uintptr_t address) const
    {
        if (_ranges.empty())
        {
            return true;
        }

        Finalize();

        // Largest range whose start is <= address, then a single bound check.
        auto it = std::upper_bound(_ranges.begin(), _ranges.end(), address,
                                   [](uintptr_t addr, const Range& r) { return addr < r.start; });
        if (it == _ranges.begin())
        {
            return false;
        }
        --it;
        return address < it->end;
    }

    bool IsEmpty() const
    {
        return _ranges.empty();
    }

    size_t GetRangeCount() const
    {
        Finalize();
        return _ranges.size();
    }

    size_t GetMemorySize() const
    {
        return sizeof(GCHeapRangeSet) + _ranges.capacity() * sizeof(Range);
    }

private:
    struct Range
    {
        uintptr_t start; // inclusive
        uintptr_t end;   // exclusive
    };

    // Mutable so the const query path (IsValidObjectAddress is const) can lazily
    // finalize. Single-threaded, so no synchronization is required.
    mutable std::vector<Range> _ranges;
    mutable bool _dirty = false;
};
