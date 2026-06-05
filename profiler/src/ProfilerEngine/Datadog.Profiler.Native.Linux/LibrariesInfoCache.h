// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#define UNW_LOCAL_ONLY
#include <libunwind.h>

#include "DlPhdrInfoWrapper.h"

#include "AutoResetEvent.h"
#include "MemoryResourceManager.h"
#include "ServiceBase.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <atomic>
#include <chrono>
#include <link.h>
#include <memory>
#include <shared_mutex>
#include <thread>
#include <time.h>
#include <vector>

#ifdef ARM64
struct FuncEntry
{
    unw_word_t start_ip;
    unw_word_t end_ip;
};

struct ModuleRegion
{
    unw_word_t addr_low;
    unw_word_t addr_high;
    uint32_t sym_offset;
    uint32_t sym_count;
};
#endif

class TrackingMemoryResource : public shared::pmr::memory_resource
{
public:
    explicit TrackingMemoryResource(shared::pmr::memory_resource* upstream) :
        _upstream{upstream}
    {
    }

    std::size_t GetCurrentUsage() const { return _currentUsage.load(std::memory_order_relaxed); }
    std::size_t GetPeakUsage() const { return _peakUsage.load(std::memory_order_relaxed); }
    std::size_t GetTotalAllocated() const { return _totalAllocated.load(std::memory_order_relaxed); }
    std::size_t GetTotalDeallocated() const { return _totalDeallocated.load(std::memory_order_relaxed); }
    std::size_t GetAllocationCount() const { return _allocationCount.load(std::memory_order_relaxed); }

    void ResetPerReloadStats()
    {
        _reloadAllocations.store(0, std::memory_order_relaxed);
        _reloadBytes.store(0, std::memory_order_relaxed);
    }

    std::size_t GetReloadAllocations() const { return _reloadAllocations.load(std::memory_order_relaxed); }
    std::size_t GetReloadBytes() const { return _reloadBytes.load(std::memory_order_relaxed); }

protected:
    void* do_allocate(std::size_t bytes, std::size_t alignment) override
    {
        _reloadAllocations.fetch_add(1, std::memory_order_relaxed);
        _reloadBytes.fetch_add(bytes, std::memory_order_relaxed);
        _allocationCount.fetch_add(1, std::memory_order_relaxed);
        _totalAllocated.fetch_add(bytes, std::memory_order_relaxed);
        auto current = _currentUsage.fetch_add(bytes, std::memory_order_relaxed) + bytes;
        auto peak = _peakUsage.load(std::memory_order_relaxed);
        while (current > peak && !_peakUsage.compare_exchange_weak(peak, current, std::memory_order_relaxed))
        {
        }
        return _upstream->allocate(bytes, alignment);
    }

    void do_deallocate(void* p, std::size_t bytes, std::size_t alignment) override
    {
        _currentUsage.fetch_sub(bytes, std::memory_order_relaxed);
        _totalDeallocated.fetch_add(bytes, std::memory_order_relaxed);
        _upstream->deallocate(p, bytes, alignment);
    }

    bool do_is_equal(const memory_resource& other) const noexcept override
    {
        return this == &other;
    }

private:
    shared::pmr::memory_resource* _upstream;
    std::atomic<std::size_t> _currentUsage{0};
    std::atomic<std::size_t> _peakUsage{0};
    std::atomic<std::size_t> _totalAllocated{0};
    std::atomic<std::size_t> _totalDeallocated{0};
    std::atomic<std::size_t> _allocationCount{0};
    std::atomic<std::size_t> _reloadAllocations{0};
    std::atomic<std::size_t> _reloadBytes{0};
};

class LibrariesInfoCache : public ServiceBase
{
public:
    LibrariesInfoCache(shared::pmr::memory_resource* resource);
    ~LibrariesInfoCache();

    LibrariesInfoCache(LibrariesInfoCache const&) = delete;
    LibrariesInfoCache& operator=(LibrariesInfoCache const&) = delete;

    LibrariesInfoCache(LibrariesInfoCache&&) = delete;
    LibrariesInfoCache& operator=(LibrariesInfoCache&&) = delete;

    const char* GetName() final override;

protected:
    bool StartImpl() final override;
    bool StopImpl() final override;

#ifdef DD_TEST
public:
    static void* GetLocalAddressSpace();
#else
private:
#endif
    static int DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data);
#ifdef ARM64
    static int GetProcName(unw_addr_space_t as, unw_word_t ip,
                           char* buf, size_t buf_len,
                           unw_word_t* offp, void* arg);
#endif
    void NotifyCacheUpdateImpl();
#ifdef DD_TEST
private:
#endif
    static void NotifyCacheUpdate();

    void UpdateCache();
#ifdef ARM64
#ifdef DD_TEST
public:
#endif
    void BuildSymbolCache(std::vector<DlPhdrInfoWrapper>& phdrCache,
                          std::vector<ModuleRegion>& outRegions,
                          std::vector<FuncEntry>& outSymbols);
#ifdef DD_TEST
private:
#endif
    int FindFunctionStart(unw_word_t ip, unw_word_t* func_start) const;
    int GetProcNameImpl(unw_addr_space_t as, unw_word_t ip,
                        char* buf, size_t buf_len,
                        unw_word_t* offp, void* arg);
#endif
    int DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data);
    void Work(std::shared_ptr<AutoResetEvent> startEvent);
    void LogStats();
    void SetupCpuTimer();
    void TeardownCpuTimer();

    static std::atomic<LibrariesInfoCache*> s_instance;

    TrackingMemoryResource _trackingResource;
    shared::pmr::memory_resource* _wrappersAllocator;

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>> _librariesInfo;
    std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>> _newCache;

#ifdef ARM64
#ifdef DD_TEST
public:
#endif
    std::vector<ModuleRegion> _moduleRegions;
    std::vector<FuncEntry> _symbols;
#ifdef DD_TEST
private:
#endif

    using GetProcNameFn = int (*)(unw_addr_space_t, unw_word_t, char*, size_t, unw_word_t*, void*);
    GetProcNameFn _originalGetProcName = nullptr;
#endif

    std::thread _worker;
    std::atomic<bool> _stopRequested;
    AutoResetEvent _event;

    // Stats
    std::atomic<std::uint64_t> _cpuTicks{0};
    std::uint32_t _reloadCount{0};
    std::uint32_t _notificationCount{0};
    std::chrono::steady_clock::duration _totalReloadDuration{0};
    std::chrono::steady_clock::duration _maxReloadDuration{0};
    std::chrono::steady_clock::duration _totalLockHoldDuration{0};
    std::chrono::steady_clock::duration _maxLockHoldDuration{0};
    timer_t _cpuTimerId{};
    bool _cpuTimerCreated{false};
};
