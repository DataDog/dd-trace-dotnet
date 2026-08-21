// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AddressSpaceMap.h"

#include <algorithm>

namespace
{
// Portion of `metric` (committed/rss of a uniform run) attributable to `overlap` bytes of the run.
// Runs are page-uniform for committed on Windows and accessible-uniform on Linux, so this is exact
// there; for Linux RSS spanning several logical sub-regions it is the documented proportional split.
uint64_t SliceOf(uint64_t metric, uint64_t overlap, uint64_t runSize)
{
    if (metric == 0 || overlap == 0 || runSize == 0)
    {
        return 0;
    }
    if (overlap >= runSize || metric >= runSize)
    {
        // Fully overlapped, or a fully-committed/resident run: the overlap carries `min(metric, overlap)`.
        return metric < overlap ? metric : overlap;
    }
    return static_cast<uint64_t>(static_cast<double>(metric) * static_cast<double>(overlap) / static_cast<double>(runSize));
}
} // namespace

AddressSpaceMap::AddressSpaceMap(std::vector<AddressRegion> regions, bool providesCommitted, bool providesRss) :
    _regions{std::move(regions)},
    _providesCommitted{providesCommitted},
    _providesRss{providesRss}
{
    std::sort(_regions.begin(), _regions.end(), [](const AddressRegion& a, const AddressRegion& b) {
        return a.Address < b.Address;
    });
}

const std::vector<AddressRegion>& AddressSpaceMap::Regions() const
{
    return _regions;
}

size_t AddressSpaceMap::FirstRunFrom(uintptr_t address) const
{
    // Runs are sorted by start address and non-overlapping, so End() is monotonically non-decreasing.
    auto it = std::lower_bound(_regions.begin(), _regions.end(), address,
                               [](const AddressRegion& r, uintptr_t addr) { return r.End() <= addr; });
    return static_cast<size_t>(it - _regions.begin());
}

uint64_t AddressSpaceMap::GetCommittedBytes(uintptr_t base, uint64_t size) const
{
    if (base == 0 || size == 0)
    {
        return 0;
    }

    const uint64_t end = static_cast<uint64_t>(base) + size;
    uint64_t committed = 0;

    for (size_t i = FirstRunFrom(base); i < _regions.size(); ++i)
    {
        const auto& r = _regions[i];
        if (r.Address >= end)
        {
            break;
        }
        const uint64_t s = static_cast<uint64_t>(r.Address) > base ? static_cast<uint64_t>(r.Address) : base;
        const uint64_t e = r.End() < end ? r.End() : end;
        if (e > s)
        {
            committed += SliceOf(r.Committed, e - s, r.Size);
        }
    }

    return committed > size ? size : committed;
}

uint64_t AddressSpaceMap::GetRss(uintptr_t base, uint64_t size) const
{
    if (base == 0 || size == 0)
    {
        return 0;
    }

    const uint64_t end = static_cast<uint64_t>(base) + size;
    uint64_t rss = 0;

    for (size_t i = FirstRunFrom(base); i < _regions.size(); ++i)
    {
        const auto& r = _regions[i];
        if (r.Address >= end)
        {
            break;
        }
        const uint64_t s = static_cast<uint64_t>(r.Address) > base ? static_cast<uint64_t>(r.Address) : base;
        const uint64_t e = r.End() < end ? r.End() : end;
        if (e > s)
        {
            rss += SliceOf(r.Rss, e - s, r.Size);
        }
    }

    return rss > size ? size : rss;
}

bool AddressSpaceMap::TryGetRegion(uintptr_t address, AddressRegion& out) const
{
    const size_t i = FirstRunFrom(address);
    if (i < _regions.size() && _regions[i].Address <= address && address < _regions[i].End())
    {
        out = _regions[i];
        return true;
    }
    return false;
}

bool AddressSpaceMap::IsAvailable() const
{
    return !_regions.empty();
}

bool AddressSpaceMap::ProvidesCommitted() const
{
    return _providesCommitted;
}

bool AddressSpaceMap::ProvidesRss() const
{
    return _providesRss;
}
