// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#define UNW_LOCAL_ONLY
#include <libunwind.h>

#include "DlPhdrInfoWrapper.h"

#include "AutoResetEvent.h"
#include "ServiceBase.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <atomic>
#include <link.h>
#include <memory>
#include <shared_mutex>
#include <thread>
#include <vector>

#ifdef ARM64
struct FuncEntry
{
    uint32_t offset; // relative to ModuleRegion::addr_low
    uint32_t size;   // function size (end_ip - start_ip)
};

struct ModuleRegion
{
    unw_word_t addr_low;
    unw_word_t addr_high;
    uint32_t sym_offset;
    uint32_t sym_count;
};
#endif

class IConfiguration;
class MetricsRegistry;
struct FootprintTracker;

class LibrariesInfoCache : public ServiceBase
{
public:
    LibrariesInfoCache(IConfiguration* configuration, shared::pmr::memory_resource* resource, MetricsRegistry& metricsRegistry);
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
    void BuildSymbolCache(std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>>& phdrCache,
                          std::vector<ModuleRegion, shared::pmr::polymorphic_allocator<ModuleRegion>>& outRegions,
                          std::vector<FuncEntry, shared::pmr::polymorphic_allocator<FuncEntry>>& outSymbols);
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

    static std::atomic<LibrariesInfoCache*> s_instance;

    std::unique_ptr<FootprintTracker> _tracker;
    shared::pmr::memory_resource* _wrappersAllocator;

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>> _librariesInfo;
    std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>> _newCache;

#ifdef ARM64
#ifdef DD_TEST
public:
#endif
    std::vector<ModuleRegion, shared::pmr::polymorphic_allocator<ModuleRegion>> _moduleRegions;
    std::vector<FuncEntry, shared::pmr::polymorphic_allocator<FuncEntry>> _symbols;
    std::vector<ModuleRegion, shared::pmr::polymorphic_allocator<ModuleRegion>> _newRegions;
    std::vector<FuncEntry, shared::pmr::polymorphic_allocator<FuncEntry>> _newSymbols;
#ifdef DD_TEST
private:
#endif

    using GetProcNameFn = int (*)(unw_addr_space_t, unw_word_t, char*, size_t, unw_word_t*, void*);
    GetProcNameFn _originalGetProcName = nullptr;
#endif

    std::thread _worker;
    std::atomic<bool> _stopRequested;
    AutoResetEvent _event;
};
