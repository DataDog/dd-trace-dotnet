// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "Log.h"
#include "OpSysTools.h"

#include <unistd.h>

using namespace std::chrono_literals;

std::atomic<LibrariesInfoCache*> LibrariesInfoCache::s_instance{nullptr};

extern "C" void (*volatile dd_notify_libraries_cache_update)() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache(shared::pmr::memory_resource* resource) :
    _stopRequested{false},
    _event(true), // set the event to force updating the cache the first time Wait is called
    _wrappersAllocator{resource}
{
    // Register the custom function with libunwind
    // IMPORTANT: Registering the function in the constructor prevents from memory coherency issues:
    // When CorProfilerCallback is initialized, we create a LibrariesInfoCache instance.
    // With stable config, profiler is not yet started but we listen to the threds callback to
    // update the managed threads list. In ThreadAssignedToOSThread, we call unw_backtrace to initialize
    // thread local variables in libunwind. unw_local_addr_space will be initialized once and it will
    // use the libc dl_iterate_phdr function to iterate over the libraries.
    // When the profiler is started, all services are started and LibrariesInfoCache starts and
    // registers the custom function with libunwind.
    // At that point, threads are unwound but some of them calls libc dl_iterate_phdr function instead of the custom one.
    // We may end up blocking the customer application.
    // Instead, we register the custom function in the constructor to ensure that the custom function
    // is used by all threads.
    unw_set_iterate_phdr_function(unw_local_addr_space, LibrariesInfoCache::DlIteratePhdr);
}

LibrariesInfoCache::~LibrariesInfoCache()
{
    unw_set_iterate_phdr_function(unw_local_addr_space, dl_iterate_phdr);
}

const char* LibrariesInfoCache::GetName()
{
    return "Libraries Info Cache";
}

bool LibrariesInfoCache::StartImpl()
{
    _librariesInfo.reserve(100);

    // CRITICAL ORDERING:
    // 1. Start the worker thread to populate the cache
    // 2. Wait for the first cache update to complete
    // 3. Set s_instance ONLY after cache is populated
    //
    // This ensures s_instance is NEVER set with an empty cache, and when the
    // custom function is registered and could be called from signal handlers,
    // both s_instance is set AND the cache is fully populated.

    auto startEvent = std::make_shared<AutoResetEvent>(false);
    _worker = std::thread(&LibrariesInfoCache::Work, this, startEvent);

    // Wait for the thread to be fully started and the cache populated
    // before setting s_instance and registering with libunwind. 2s timeout for CI.
    if (!startEvent->Wait(2s))
    {
        Log::Error("Failed to populate LibrariesInfoCache within timeout. "
                   "Not registering custom iterate_phdr_function with libunwind.");
        return false;
    }

    // Now that the cache is populated, set the instance pointer
    s_instance.store(this, std::memory_order_release);

    Log::Info("LibrariesInfoCache: Registered custom iterate_phdr_function with libunwind.");

    return true;
}

bool LibrariesInfoCache::StopImpl()
{
    s_instance.store(nullptr, std::memory_order_release);

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
    // The Cache Thread be blocked here if the sampled thread is interrrupted while holding the malloc lock.
    // But it's ok, Cache Thread does not hold the cache lock, and the sampled thread
    // won't be blocked during the unwinding.
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

    newCache.clear();
}

int LibrariesInfoCache::DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data)
{
    auto* instance = s_instance.load(std::memory_order_acquire);
    if (instance == nullptr)
    {
        return 0;
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
    auto* instance = s_instance.load(std::memory_order_acquire);
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
