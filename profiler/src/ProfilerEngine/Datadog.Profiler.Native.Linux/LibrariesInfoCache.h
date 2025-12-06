// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include "DlPhdrInfoWrapper.h"

#include "AutoResetEvent.h"
#include "MemoryResourceManager.h"
#include "ServiceBase.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <atomic>
#include <libunwind.h>
#include <link.h>
#include <memory>
#include <shared_mutex>
#include <thread>
#include <unordered_map>
#include <vector>
#include <string>

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

    static LibrariesInfoCache* GetInstance();
    bool IsAddressInManagedRegion(uintptr_t address);
    void UpdateManagedRegionsIfNeeded();

protected:
    bool StartImpl() final override;
    bool StopImpl() final override;

private:
    static int DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data);
    static void NotifyCacheUpdate();

    void UpdateCache();
    int DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data);
#ifdef DD_TEST
public:
#endif
    void NotifyCacheUpdateImpl();
    static bool ShouldTreatAsManagedMapping(const std::string& pathname, unsigned long inode);
#ifdef DD_TEST
private:
#endif
    void Work(std::shared_ptr<AutoResetEvent> startEvent);

    static LibrariesInfoCache* s_instance;

    std::shared_mutex _cacheLock;
    std::vector<DlPhdrInfoWrapper> _librariesInfo;

    // Signal-safe managed regions data structure
    struct ManagedRegion
    {
        uintptr_t start;
        uintptr_t end;
        uintptr_t mappingId; // Unique identifier for the mapping
    };
    
    using ManagedRegionAllocator = shared::pmr::polymorphic_allocator<ManagedRegion>;
    using ManagedRegionVector = std::vector<ManagedRegion, ManagedRegionAllocator>;

    ManagedRegionVector _managedRegions;
    std::atomic<size_t> _managedRegionCount;
    
    // Flag to indicate that we've encountered unknown mappings
    // This forces a full rescan on next update
    std::atomic<bool> _hasMissingMappings;

    std::thread _worker;
    std::atomic<bool> _stopRequested;
    AutoResetEvent _event;
    shared::pmr::memory_resource* _wrappersAllocator;
};
