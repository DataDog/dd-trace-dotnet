// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#define UNW_LOCAL_ONLY
#include <libunwind.h>

#include "DlPhdrInfoWrapper.h"
#include "StackDeltaMap.h"

#include "AutoResetEvent.h"
#include "MemoryResourceManager.h"
#include "ServiceBase.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <atomic>
#include <link.h>
#include <memory>
#include <shared_mutex>
#include <thread>
#include <vector>

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

    // Returns the current stack delta map. The returned pointer is valid until
    // the next UpdateCache() call swaps it out. Signal-safe: the map is
    // immutable once published.
    const StackDeltaMap* GetDeltaMap() const
    {
        return _activeDeltaMap.load(std::memory_order_acquire);
    }

    // Returns a pointer to the internal atomic that holds the active delta map.
    // HybridUnwinder stores this pointer and does atomic loads from the
    // signal handler, avoiding any locking.
    const std::atomic<StackDeltaMap*>* GetDeltaMapAtomicPtr() const
    {
        return &_activeDeltaMap;
    }

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
    void NotifyCacheUpdateImpl();
#ifdef DD_TEST
private:
#endif
    static void NotifyCacheUpdate();

    void UpdateCache();
    void BuildDeltaMap();
    int DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data);
    void Work(std::shared_ptr<AutoResetEvent> startEvent);

    static std::atomic<LibrariesInfoCache*> s_instance;

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper> _librariesInfo;

    // Double-buffered delta maps for lock-free signal-handler reads.
    // The background thread builds into the staging map and atomically
    // publishes it, then the previously active map becomes the next
    // staging buffer.
    std::unique_ptr<StackDeltaMap> _deltaMapA = std::make_unique<StackDeltaMap>();
    std::unique_ptr<StackDeltaMap> _deltaMapB = std::make_unique<StackDeltaMap>();
    std::atomic<StackDeltaMap*> _activeDeltaMap{nullptr};

    std::thread _worker;
    std::atomic<bool> _stopRequested;
    AutoResetEvent _event;
    shared::pmr::memory_resource* _wrappersAllocator;
};
