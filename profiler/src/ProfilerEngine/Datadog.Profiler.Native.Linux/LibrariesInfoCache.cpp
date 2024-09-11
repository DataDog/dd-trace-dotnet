// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "Log.h"

#include <string.h>


#include <linux/futex.h>      /* Definition of FUTEX_* constants */
#include <sys/syscall.h>      /* Definition of SYS_* constants */
#include <unistd.h>

LibrariesInfoCache* LibrariesInfoCache::s_instance = nullptr;

extern "C" void (*volatile dd_notify_libraries_cache_update)() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache()
    : _stopRequested{false}
{
}

LibrariesInfoCache::~LibrariesInfoCache() = default;

const char* LibrariesInfoCache::GetName()
{
    return "Libraries Info Cache";
}

bool LibrariesInfoCache::StartImpl()
{
    LibrariesInfo.reserve(100);
    s_instance = this;
    unw_set_iterate_phdr_function(unw_local_addr_space, LibrariesInfoCache::DlIteratePhdr);
    _mre.Set(); // make sure we start by updating the cache
    _worker = std::thread(&LibrariesInfoCache::Work, this);
    return true;
}

bool LibrariesInfoCache::StopImpl()
{
    unw_set_iterate_phdr_function(unw_local_addr_space, dl_iterate_phdr);
    if (&dd_notify_libraries_cache_update != nullptr) [[likely]]
    {
        dd_notify_libraries_cache_update = nullptr;
    }

    s_instance = nullptr;

    _stopRequested = true;
    NotifyCacheUpdateImpl();
    _worker.join();
    return true;
}

struct IterationData
{
public:
    std::size_t Index;
    LibrariesInfoCache* Cache;
};

void LibrariesInfoCache::Work()
{
    if (&dd_notify_libraries_cache_update != nullptr) [[likely]]
    {
        dd_notify_libraries_cache_update = LibrariesInfoCache::NotifyCacheUpdate;
    }

    while (!_stopRequested)
    {
        _mre.Wait();
        _mre.Clear();

        if (_stopRequested)
        {
            break;
        }

        UpdateCache();
    }
}

void LibrariesInfoCache::UpdateCache()
{
    std::unique_lock l(_cacheLock);

    IterationData data = {.Index = 0, .Cache = this};
    dl_iterate_phdr(
        [](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* iterationData = static_cast<IterationData*>(data);
            auto* cache = iterationData->Cache;

            if (cache->LibrariesInfo.size() <= iterationData->Index)
            {
                cache->LibrariesInfo.push_back(DlPhdrInfoWrapper(info, size));
                iterationData->Index++;
                return 0;
            }

            auto& current = cache->LibrariesInfo[iterationData->Index];
            if (current.IsSame(info))
            {
                iterationData->Index++;
                return 0;
            }

            DlPhdrInfoWrapper wrappedInfo(info, size);
            cache->LibrariesInfo[iterationData->Index] = std::move(wrappedInfo);
            iterationData->Index++;
            return 0;
        },
        &data);

    LibrariesInfo.erase(LibrariesInfo.begin() + data.Index, LibrariesInfo.end());
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
    for (auto& wrappedInfo : LibrariesInfo)
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
    _mre.Set();
}

ManualResetEvent::ManualResetEvent()
 : _futex{0}
{
}

void ManualResetEvent::Set()
{
    _futex = 1;
    // notify
    syscall(SYS_futex, (int*)&_futex, FUTEX_WAKE | FUTEX_PRIVATE_FLAG, 1);
}

void ManualResetEvent::Wait()
{
    if (_futex == 1)
        return;

    syscall(SYS_futex, (int*)&_futex, FUTEX_WAIT | FUTEX_PRIVATE_FLAG, 0, 0, 0, 0);
}

void ManualResetEvent::Clear()
{
    _futex = 0;
}
