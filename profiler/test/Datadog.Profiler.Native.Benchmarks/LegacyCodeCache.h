// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// Frozen copy of the data structure used by ManagedCodeCache before the lock-free
// CodeRangeTrie (paged unordered_map + reader/writer spinning locks with a 500us
// timeout on the signal-handler read path). It is kept only as the baseline for the
// benchmarks: do not use it in production code.
//
// The CLR-facing parts (GetCodeInfo2, PE parsing, GetFunctionFromIP fallback) are
// stripped: ranges are given directly.

#include "CodeRangeTrie.h" // CodeRange, ModuleCodeRange
#include "ReaderWriterSpinningMutex.hpp"

#include <algorithm>
#include <chrono>
#include <mutex>
#include <optional>
#include <shared_mutex>
#include <unordered_map>
#include <vector>

#include <pthread.h>
#include <signal.h>

namespace legacy {

using CodeCacheMutex = ReaderWriterSpinningMutex;

static constexpr auto SignalLockTimeout = std::chrono::microseconds(500);

class ScopedProfilerSignalBlocker
{
public:
    ScopedProfilerSignalBlocker()
    {
        sigset_t toBlock;
        sigemptyset(&toBlock);
        sigaddset(&toBlock, SIGPROF);
        sigaddset(&toBlock, SIGUSR1);
        pthread_sigmask(SIG_BLOCK, &toBlock, &_previous);
    }

    ~ScopedProfilerSignalBlocker()
    {
        pthread_sigmask(SIG_SETMASK, &_previous, nullptr);
    }

private:
    sigset_t _previous;
};

struct LegacyModuleCodeRange
{
    std::uintptr_t startAddress;
    std::uintptr_t endAddress; // Inclusive
    bool isRemoved = false;

    bool contains(std::uintptr_t ip) const
    {
        return ip >= startAddress && ip <= endAddress;
    }
};

template <typename Container, typename Value>
std::optional<typename Container::value_type> FindRange(Container const& container, Value const& value)
{
    auto it = std::lower_bound(container.begin(), container.end(), value,
        [](const typename Container::value_type& range, const Value& value) -> bool {
            return range.startAddress <= value;
        });

    if (it == container.cbegin())
    {
        return std::nullopt;
    }

    --it;
    return it->contains(value) ? std::optional{*it} : std::nullopt;
}

class LegacyCodeCache
{
public:
    struct FunctionInfo
    {
        std::uintptr_t FunctionId;
        bool IsDynamic;
    };

    std::optional<bool> IsManaged(std::uintptr_t ip) const noexcept
    {
        uint64_t page = GetPageNumber(ip);

        {
            std::shared_lock<CodeCacheMutex> mapLock(_pagesMutex, SignalLockTimeout);
            if (!mapLock.owns_lock())
            {
                return std::nullopt;
            }
            auto pageIt = _pagesMap.find(page);
            if (pageIt != _pagesMap.end())
            {
                std::shared_lock<CodeCacheMutex> pageLock(pageIt->second.lock, SignalLockTimeout);
                if (!pageLock.owns_lock())
                {
                    return std::nullopt;
                }
                auto range = FindRange(pageIt->second.ranges, ip);
                if (range.has_value())
                {
                    return std::optional{true};
                }
            }
        }

        return IsCodeInR2RModule(ip, true);
    }

    // Lookup part of ManagedCodeCache::GetFunctionInfo (without the CLR fallback)
    std::optional<FunctionInfo> GetFunctionInfo(std::uintptr_t ip) const noexcept
    {
        uint64_t page = GetPageNumber(ip);

        std::shared_lock<CodeCacheMutex> mapLock(_pagesMutex);
        auto pageIt = _pagesMap.find(page);
        if (pageIt == _pagesMap.end())
        {
            return std::nullopt;
        }

        std::shared_lock<CodeCacheMutex> pageLock(pageIt->second.lock);
        auto range = FindRange(pageIt->second.ranges, ip);
        if (range.has_value())
        {
            return FunctionInfo{range->functionId, range->isDynamic};
        }

        return std::nullopt;
    }

    void AddFunctionRanges(const std::vector<CodeRange>& newRanges)
    {
        ScopedProfilerSignalBlocker signalBlocker;

        for (const auto& range : newRanges)
        {
            uint64_t startPage = GetPageNumber(range.startAddress);
            uint64_t endPage = GetPageNumber(range.endAddress);
            for (uint64_t page = startPage; page <= endPage; ++page)
            {
                {
                    std::shared_lock<CodeCacheMutex> mapLock(_pagesMutex);
                    auto pageIt = _pagesMap.find(page);
                    if (pageIt != _pagesMap.end())
                    {
                        InsertCodeRangeIntoPage(pageIt, range);
                        continue;
                    }
                }

                std::unique_lock<CodeCacheMutex> mapLock(_pagesMutex);
                auto [pageIt, _] = _pagesMap.try_emplace(page);
                InsertCodeRangeIntoPage(pageIt, range);
            }
        }
    }

    void AddModuleRanges(const std::vector<ModuleCodeRange>& moduleCodeRanges)
    {
        ScopedProfilerSignalBlocker signalBlocker;

        std::unique_lock<CodeCacheMutex> moduleLock(_modulesMutex);
        for (const auto& r : moduleCodeRanges)
        {
            LegacyModuleCodeRange moduleCodeRange{r.startAddress, r.endAddress};
            auto insertPos = std::upper_bound(
                _modulesCodeRanges.begin(),
                _modulesCodeRanges.end(),
                moduleCodeRange,
                [](const LegacyModuleCodeRange& range, const LegacyModuleCodeRange& other) {
                    return range.startAddress < other.startAddress;
                });
            _modulesCodeRanges.insert(insertPos, moduleCodeRange);
        }
    }

    void RemoveModuleRanges(const std::vector<ModuleCodeRange>& moduleCodeRanges)
    {
        ScopedProfilerSignalBlocker signalBlocker;
        std::unique_lock<CodeCacheMutex> moduleLock(_modulesMutex);
        for (auto const& range : moduleCodeRanges)
        {
            auto it = std::find_if(_modulesCodeRanges.begin(), _modulesCodeRanges.end(),
                [&range](const LegacyModuleCodeRange& r) {
                    return r.startAddress == range.startAddress && r.endAddress == range.endAddress;
                });
            if (it != _modulesCodeRanges.end())
            {
                it->isRemoved = true;
            }
        }
    }

    // Approximation of the heap used (unordered_map nodes + buckets + vectors)
    std::size_t MemoryUsage() const
    {
        std::shared_lock<CodeCacheMutex> mapLock(_pagesMutex);
        std::size_t total = _pagesMap.bucket_count() * sizeof(void*);
        for (auto const& [page, entry] : _pagesMap)
        {
            // node: next pointer + key + PageEntry (+ cached hash is not stored for integral keys)
            total += sizeof(void*) + sizeof(uint64_t) + sizeof(PageEntry);
            total += entry.ranges.capacity() * sizeof(CodeRange);
        }
        total += _modulesCodeRanges.capacity() * sizeof(LegacyModuleCodeRange);
        return total;
    }

    // Test hook: hold the pages lock like a writer would (to measure reader behavior)
    std::unique_lock<CodeCacheMutex> LockPagesExclusive()
    {
        return std::unique_lock<CodeCacheMutex>(_pagesMutex);
    }

private:
    struct PageEntry
    {
        std::vector<CodeRange> ranges;
        mutable CodeCacheMutex lock;

        PageEntry() = default;
        PageEntry(PageEntry&& other) noexcept :
            ranges(std::move(other.ranges))
        {
        }
        PageEntry& operator=(PageEntry&& other) noexcept
        {
            ranges = std::move(other.ranges);
            return *this;
        }
        PageEntry(const PageEntry&) = delete;
        PageEntry& operator=(const PageEntry&) = delete;
    };

    using PagesMap = std::unordered_map<uint64_t, PageEntry>;

    static constexpr size_t PAGE_SHIFT = 16;

    static uint64_t GetPageNumber(std::uintptr_t address)
    {
        return address >> PAGE_SHIFT;
    }

    void InsertCodeRangeIntoPage(PagesMap::iterator pageIt, const CodeRange& range)
    {
        std::unique_lock<CodeCacheMutex> pageLock(pageIt->second.lock);
        auto& ranges = pageIt->second.ranges;
        ranges.insert(std::upper_bound(ranges.begin(), ranges.end(), range,
                          [](const CodeRange& a, const CodeRange& b) { return a.startAddress < b.startAddress; }),
            range);
    }

    std::optional<bool> IsCodeInR2RModule(std::uintptr_t ip, bool signalSafe) const noexcept
    {
        auto moduleLock = [](CodeCacheMutex& mutex, bool signalSafe) {
            if (signalSafe)
            {
                return std::shared_lock<CodeCacheMutex>(mutex, SignalLockTimeout);
            }
            return std::shared_lock<CodeCacheMutex>(mutex);
        }(_modulesMutex, signalSafe);

        if (!moduleLock.owns_lock())
        {
            return std::nullopt;
        }

        auto moduleCodeRange = FindRange(_modulesCodeRanges, ip);
        if (!moduleCodeRange.has_value())
        {
            return {false};
        }

        if (moduleCodeRange->isRemoved)
        {
            return {false};
        }

        return {moduleCodeRange->contains(ip)};
    }

    PagesMap _pagesMap;
    std::vector<LegacyModuleCodeRange> _modulesCodeRanges;
    mutable CodeCacheMutex _modulesMutex;
    mutable CodeCacheMutex _pagesMutex;
};

} // namespace legacy
