// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "Log.h"

#include <string.h>

LibrariesInfoCache* LibrariesInfoCache::s_instance = nullptr;

// This function is exposed by the ld_preload wrapper library.
// This allows us to know if we have to reload the cache or not
extern "C" unsigned long long dd_nb_calls_to_dlopen_dlclose() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache()
{
    LibrariesInfo.reserve(100);
    NbCallsToDlopenDlclose = 0;
    s_instance = this;
    unw_set_iterate_phdr_function(unw_local_addr_space, LibrariesInfoCache::DlIteratePhdr);
}

LibrariesInfoCache* LibrariesInfoCache::Get()
{
    static LibrariesInfoCache Instance;
    return s_instance;
}

LibrariesInfoCache::~LibrariesInfoCache()
{
    unw_set_iterate_phdr_function(unw_local_addr_space, dl_iterate_phdr);
    s_instance = nullptr;
}

struct IterationData
{
public:
    std::size_t Index;
    LibrariesInfoCache* Cache;
};

void LibrariesInfoCache::UpdateCache()
{
    std::unique_lock l(_cacheLock);

    auto shouldReload = true;
    if (dd_nb_calls_to_dlopen_dlclose != nullptr) [[likely]]
    {
        auto previous = NbCallsToDlopenDlclose;
        NbCallsToDlopenDlclose = dd_nb_calls_to_dlopen_dlclose();
        shouldReload = previous != NbCallsToDlopenDlclose;
    }

    if (!shouldReload)
    {
        return;
    }

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
