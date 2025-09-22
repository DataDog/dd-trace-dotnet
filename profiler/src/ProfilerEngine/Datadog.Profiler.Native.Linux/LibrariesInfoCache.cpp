// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "Log.h"
#include "OpSysTools.h"

#include <unistd.h>
#include <algorithm>
#include <string>
#include <cstring>

using namespace std::chrono_literals;

LibrariesInfoCache* LibrariesInfoCache::s_instance = nullptr;

extern "C" void (*volatile dd_notify_libraries_cache_update)() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache(shared::pmr::memory_resource* resource) :
    _stopRequested{false},
    _event(true), // set the event to force updating the cache the first time Wait is called
    _wrappersAllocator{resource}
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

    {
        std::unique_lock l{_cacheLock};
        _librariesInfo.swap(newCache);
    }

    // Clear managed region cache when libraries change
    {
        std::lock_guard<std::mutex> lock(_managedCacheLock);
        _managedRegionCache.clear();
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

bool LibrariesInfoCache::IsAddressInManagedRegion(uintptr_t address)
{
    // Use page-aligned address for caching to reduce cache size
    const uintptr_t pageSize = 4096;
    uintptr_t pageAddress = address & ~(pageSize - 1);
    
    // Check cache first
    {   // todo: lock free mechanism ?
        std::lock_guard<std::mutex> lock(_managedCacheLock);
        auto it = _managedRegionCache.find(pageAddress);
        if (it != _managedRegionCache.end())
        {
            return it->second;
        }
    }
    
    // Cache miss - do the expensive lookup
    bool isManaged = false;
    bool found = false;
    {
        std::shared_lock l(_cacheLock);

        for (const auto& wrapper : _librariesInfo)
        {
            auto [info, size] = wrapper.Get();
            
            // Check if the address falls within this library's segments
            for (int i = 0; i < info->dlpi_phnum && !found; i++)
            {
                const ElfW(Phdr)& phdr = info->dlpi_phdr[i];
                
                // Only check executable loadable segments (PT_LOAD with execute permission)
                if (phdr.p_type != PT_LOAD || !(phdr.p_flags & PF_X))
                    continue;
                    
                // Calculate the segment's virtual address range
                uintptr_t segmentStart = info->dlpi_addr + phdr.p_vaddr;
                uintptr_t segmentEnd = segmentStart + phdr.p_memsz;
                
                // Check if address falls within this segment
                if (address >= segmentStart && address < segmentEnd)
                {
                    // Anonymous mappings (no file backing) are likely JIT-compiled managed code
                    const char* libName = info->dlpi_name;
                    bool isAnonymous = (libName == nullptr || strlen(libName) == 0);
                    
                    isManaged = isAnonymous;
                    found = true;
                }
            }
            
            if (found) break;
        }
        
        // If we can't find the address in any mapping, assume native (isManaged = false)
    }
    
    // Cache the result
    {
        std::lock_guard<std::mutex> lock(_managedCacheLock);
        // Limit cache size to prevent unbounded growth
        if (_managedRegionCache.size() > 1000)
        {
            _managedRegionCache.clear();
        }
        _managedRegionCache[pageAddress] = isManaged;
    }
    
    return isManaged;
}

