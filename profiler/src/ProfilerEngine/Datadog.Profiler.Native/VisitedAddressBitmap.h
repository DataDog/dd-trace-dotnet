// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <algorithm>
#include <array>
#include <cstdint>
#include <limits>
#include <memory>
#include <vector>

// Exact per-root visited-address set backed by lazily allocated bitmap pages.
//
// One bitmap bit represents one pointer-aligned heap slot. Pages and the page index
// are bounded at construction, while bitmap pages are committed only when first
// touched. Clearing for a new root increments an epoch; a page is zeroed lazily the
// next time that root touches it.
class VisitedAddressBitmap
{
public:
    enum class VisitResult : uint8_t
    {
        FirstVisit,
        AlreadyVisited,
        CapacityExceeded
    };

    static constexpr size_t HeapBytesPerPage = 1u << 20; // 1 MiB of heap address space
    static constexpr size_t SlotsPerPage = HeapBytesPerPage / sizeof(void*);
    static constexpr size_t WordsPerPage = SlotsPerPage / 64;

private:
    struct Page
    {
        uintptr_t pageId = 0;
        uint32_t epoch = 0;
        std::array<uint64_t, WordsPerPage> bits{};
    };

    struct PageIndexEntry
    {
        uintptr_t pageId = 0;
        Page* page = nullptr;
    };

public:
    static constexpr size_t PageStorageBytes = sizeof(Page);

    explicit VisitedAddressBitmap(size_t maxBytes, bool collectBenchmarkStats = false)
        : _maxPages((std::max)(size_t{1}, maxBytes / PageStorageBytes)),
          _pageIndex(GetIndexCapacity(_maxPages)),
          _collectBenchmarkStats(collectBenchmarkStats)
    {
        _pages.reserve(_maxPages);
        UpdatePeakMemorySize();
    }

    VisitResult TryMarkFirstVisit(uintptr_t address)
    {
        uintptr_t pageId = address / HeapBytesPerPage;
        Page* page = GetOrCreatePage(pageId);
        if (page == nullptr)
        {
            _capacityExceededCount++;
            return VisitResult::CapacityExceeded;
        }

        if (page->epoch != _rootEpoch)
        {
            page->bits.fill(0);
            page->epoch = _rootEpoch;
        }

        size_t pageOffset = static_cast<size_t>(address % HeapBytesPerPage);
        size_t slotIndex = pageOffset / sizeof(void*);
        size_t wordIndex = slotIndex / 64;
        uint64_t mask = uint64_t{1} << (slotIndex % 64);

        uint64_t& word = page->bits[wordIndex];
        if ((word & mask) != 0)
        {
            return VisitResult::AlreadyVisited;
        }

        word |= mask;
        _count++;
        _peakCount = (std::max)(_peakCount, _count);
        return VisitResult::FirstVisit;
    }

    void ClearForRoot()
    {
        _count = 0;
        if (_needsFullReset || _rootEpoch == (std::numeric_limits<uint32_t>::max)())
        {
            for (auto& page : _pages)
            {
                page->bits.fill(0);
                page->epoch = 0;
            }
            _rootEpoch = 1;
            _needsFullReset = false;
            return;
        }

        _rootEpoch++;
    }

    void MarkPossiblyInconsistent()
    {
        _needsFullReset = true;
    }

    size_t Size() const
    {
        return _count;
    }

    size_t GetPeakEntryCount() const
    {
        return _peakCount;
    }

    size_t GetMemorySize() const
    {
        return sizeof(VisitedAddressBitmap)
             + _pages.capacity() * sizeof(std::unique_ptr<Page>)
             + _pages.size() * sizeof(Page)
             + _pageIndex.capacity() * sizeof(PageIndexEntry);
    }

    size_t GetPeakMemorySize() const
    {
        return _peakMemorySize;
    }

    size_t GetAllocatedPageCount() const
    {
        return _pages.size();
    }

    size_t GetPageIndexCapacity() const
    {
        return _pageIndex.size();
    }

    size_t GetPageAllocationCount() const
    {
        return _pageAllocationCount;
    }

    size_t GetCapacityExceededCount() const
    {
        return _capacityExceededCount;
    }

    size_t GetLastPageHitCount() const
    {
        return _lastPageHitCount;
    }

    size_t GetPageIndexLookupCount() const
    {
        return _pageIndexLookupCount;
    }

    size_t GetPageIndexProbeCount() const
    {
        return _pageIndexProbeCount;
    }

private:
    static size_t GetIndexCapacity(size_t maxPages)
    {
        size_t target = maxPages > (std::numeric_limits<size_t>::max)() / 2
                            ? (std::numeric_limits<size_t>::max)()
                            : maxPages * 2;
        size_t capacity = 1;
        while (capacity < target && capacity <= (std::numeric_limits<size_t>::max)() / 2)
        {
            capacity *= 2;
        }
        return capacity;
    }

    static size_t HashPageId(uintptr_t pageId)
    {
        uint64_t value = static_cast<uint64_t>(pageId);
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9ULL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBULL;
        value ^= value >> 31;
        return static_cast<size_t>(value);
    }

    Page* GetOrCreatePage(uintptr_t pageId)
    {
        if (_lastPage != nullptr && _lastPageId == pageId)
        {
            if (_collectBenchmarkStats)
            {
                _lastPageHitCount++;
            }
            return _lastPage;
        }

        if (_collectBenchmarkStats)
        {
            _pageIndexLookupCount++;
        }

        size_t mask = _pageIndex.size() - 1;
        size_t index = HashPageId(pageId) & mask;

        while (true)
        {
            if (_collectBenchmarkStats)
            {
                _pageIndexProbeCount++;
            }

            PageIndexEntry& entry = _pageIndex[index];
            if (entry.page != nullptr)
            {
                if (entry.pageId == pageId)
                {
                    _lastPageId = pageId;
                    _lastPage = entry.page;
                    return entry.page;
                }
                index = (index + 1) & mask;
                continue;
            }

            if (_pages.size() >= _maxPages)
            {
                return nullptr;
            }

            auto newPage = std::make_unique<Page>();
            newPage->pageId = pageId;
            newPage->epoch = _rootEpoch;
            Page* page = newPage.get();
            _pages.push_back(std::move(newPage));

            // Publish the pointer last so a partially initialized entry is never visible.
            entry.pageId = pageId;
            entry.page = page;
            _lastPageId = pageId;
            _lastPage = page;
            _pageAllocationCount++;
            UpdatePeakMemorySize();
            return page;
        }
    }

    void UpdatePeakMemorySize()
    {
        _peakMemorySize = (std::max)(_peakMemorySize, GetMemorySize());
    }

    size_t _maxPages;
    std::vector<std::unique_ptr<Page>> _pages;
    std::vector<PageIndexEntry> _pageIndex;
    uint32_t _rootEpoch = 1;
    size_t _count = 0;
    size_t _peakCount = 0;
    size_t _peakMemorySize = 0;
    size_t _pageAllocationCount = 0;
    size_t _capacityExceededCount = 0;
    uintptr_t _lastPageId = 0;
    Page* _lastPage = nullptr;
    bool _needsFullReset = false;
    bool _collectBenchmarkStats;
    size_t _lastPageHitCount = 0;
    size_t _pageIndexLookupCount = 0;
    size_t _pageIndexProbeCount = 0;
};
