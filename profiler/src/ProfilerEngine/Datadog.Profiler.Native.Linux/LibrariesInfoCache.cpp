// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "Log.h"
#include "OpSysTools.h"

#include <algorithm>
#include <cstring>
#include <string>
#include <unistd.h>

using namespace std::chrono_literals;

LibrariesInfoCache* LibrariesInfoCache::s_instance = nullptr;

extern "C" void (*volatile dd_notify_libraries_cache_update)() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache(shared::pmr::memory_resource* resource) :
    _stopRequested{false},
    _event(true), // set the event to force updating the cache the first time Wait is called
    _wrappersAllocator{resource},
    _managedRegions{resource},
    _managedRegionCount(0),
    _hasMissingMappings(false)
{
}

LibrariesInfoCache::~LibrariesInfoCache() = default;

const char* LibrariesInfoCache::GetName()
{
    return "Libraries Info Cache";
}

bool LibrariesInfoCache::StartImpl()
{
    _librariesInfo.reserve(100);
    s_instance = this;
    unw_set_iterate_phdr_function(unw_local_addr_space, LibrariesInfoCache::DlIteratePhdr);
    auto startEvent = std::make_shared<AutoResetEvent>(false);
    _worker = std::thread(&LibrariesInfoCache::Work, this, startEvent);
    // We must wait for the thread to be fully started and the cache populated
    // before reporting the service start status.
    // 2s for CI
    return startEvent->Wait(2s);
}

bool LibrariesInfoCache::StopImpl()
{
    unw_set_iterate_phdr_function(unw_local_addr_space, dl_iterate_phdr);
    s_instance = nullptr;

    _stopRequested = true;
    NotifyCacheUpdateImpl();
    Log::Debug("Notification to stop the worker has been sent.");
    _worker.join();
    _librariesInfo.clear();

    return true;
}

void LibrariesInfoCache::Work(std::shared_ptr<AutoResetEvent> startEvent)
{
    OpSysTools::SetNativeThreadName(WStr("DD_LibsCache"));

    auto timeout = InfiniteTimeout;
    if (&dd_notify_libraries_cache_update != nullptr) [[likely]]
    {
        dd_notify_libraries_cache_update = LibrariesInfoCache::NotifyCacheUpdate;
    }
    else
    {
        // if this function is missing we still want the cache to update on a regular basis
        constexpr auto defaultTimeout = 1s;
        Log::Info("Wrapper library is missing. The cache will reload itself every ", defaultTimeout);
        timeout = defaultTimeout;
    }

    while (!_stopRequested)
    {
        // in the default case, notification mechanism in place, we will block until notification
        // Otherwise, we reload the cache no matter on a regular basis (defaultTimeout)
        _event.Wait(timeout);

        if (_stopRequested)
        {
            break;
        }

        UpdateCache();

        if (startEvent != nullptr)
        {
            startEvent->Set();
            startEvent.reset();
        }
    }

    Log::Debug("Stopping worker: stop request received.");

    if (&dd_notify_libraries_cache_update != nullptr) [[likely]]
    {
        dd_notify_libraries_cache_update = nullptr;
    }
}

void LibrariesInfoCache::UpdateCache()
{
    // We *MUST* not allocate while holding the _cacheLock
    // We may end up in a deadlock situation
    // Example:
    // T1 tries to allocate and is interrupted by the sampler
    // Cache Thread, updates the cache and allocates while holding the cache lock.
    // But the Cache Thread is blocked (T1 owns the malloc lock)
    // T1 is blocked when libwunding calls DlIteratePhdr.

    // OPTIM: make it static to reuse buffer overtime
    // Safe to make it static to the function, this variable is accessed only by one thread.
    static std::vector<DlPhdrInfoWrapper> newCache;
    newCache.reserve(_librariesInfo.capacity());

    struct Data
    {
    public:
        std::vector<DlPhdrInfoWrapper>* Cache;
        shared::pmr::memory_resource* Allocator;
    };

    auto data = Data{.Cache = &newCache, .Allocator = _wrappersAllocator};

    dl_iterate_phdr(
        [](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* impl = static_cast<Data*>(data);
            impl->Cache->emplace_back(info, size, impl->Allocator);
            return 0;
        },
        &data);

    // Pre-compute managed regions for signal-safe lookup
    shared::pmr::vector<ManagedRegion> newManagedRegions{_wrappersAllocator};
    
    for (const auto& wrapper : newCache)
    {
        auto [info, size] = wrapper.Get();

        // Check each executable loadable segment
        for (int i = 0; i < info->dlpi_phnum; i++)
        {
            const ElfW(Phdr)& phdr = info->dlpi_phdr[i];

            // Only check executable loadable segments (PT_LOAD with execute permission)
            if (phdr.p_type != PT_LOAD || !(phdr.p_flags & PF_X))
                continue;

            // Anonymous mappings (no file backing) are likely JIT-compiled managed code
            const char* libName = info->dlpi_name;
            bool isAnonymous = (libName == nullptr || strlen(libName) == 0);

            if (isAnonymous)
            {
                // Calculate the segment's virtual address range
                uintptr_t segmentStart = info->dlpi_addr + phdr.p_vaddr;
                uintptr_t segmentEnd = segmentStart + phdr.p_memsz;

                newManagedRegions.push_back({segmentStart, segmentEnd, info->dlpi_addr});
            }
        }
    }

    {
        std::unique_lock l{_cacheLock};
        _librariesInfo.swap(newCache);
        _managedRegions.swap(newManagedRegions);
        _managedRegionCount.store(_managedRegions.size(), std::memory_order_release);
        
        // Reset the missing mappings flag since we just did a full update
        _hasMissingMappings.store(false, std::memory_order_release);
    }

    newCache.clear();
}

int LibrariesInfoCache::DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data)
{
    int rc = 0;
    auto* instance = s_instance;
    if (instance == nullptr)
    {
        return rc;
    }

    return instance->DlIteratePhdrImpl(callback, data);
}

int LibrariesInfoCache::DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data)
{
    std::shared_lock l(_cacheLock);

    int rc = 0;
    for (auto& wrappedInfo : _librariesInfo)
    {
        auto [info, size] = wrappedInfo.Get();
        rc = callback(info, size, data);
        if (rc != 0)
        {
            break;
        }
    }
    return rc;
}

void LibrariesInfoCache::NotifyCacheUpdate()
{
    auto* instance = s_instance;
    if (instance == nullptr)
    {
        return;
    }
    instance->NotifyCacheUpdateImpl();
}

void LibrariesInfoCache::NotifyCacheUpdateImpl()
{
    _event.Set();
}

LibrariesInfoCache* LibrariesInfoCache::GetInstance()
{
    return s_instance;
}

void LibrariesInfoCache::UpdateManagedRegionsIfNeeded()
{
    // Check if we have encountered unknown mappings (potential new JIT compilation)
    bool hasMissingMappings = _hasMissingMappings.load(std::memory_order_acquire);
    
    if (!hasMissingMappings)
        return;
    
    // Force a full cache update to discover new mappings
    UpdateCache();
    _hasMissingMappings.store(false, std::memory_order_release);
}

bool LibrariesInfoCache::IsAddressInManagedRegion(uintptr_t address)
{
    // Signal-safe lookup using pre-computed managed regions
    // No locks needed - we use atomic loads and read-only data

    size_t regionCount = _managedRegionCount.load(std::memory_order_acquire);

    // Simple linear search through managed regions
    // This is signal-safe as we're only reading pre-computed data
    for (size_t i = 0; i < regionCount; i++)
    {
        const ManagedRegion& region = _managedRegions[i];
        if (address >= region.start && address < region.end)
        {
            return true;
        }
    }

    // Address not found in any known managed region
    // This could mean:
    // 1. It's in a native library (normal case)
    // 2. It's in a new JIT-compiled region we haven't discovered yet
    // Set flag to trigger cache update on next safe opportunity
    _hasMissingMappings.store(true, std::memory_order_release);

    return false;
}
